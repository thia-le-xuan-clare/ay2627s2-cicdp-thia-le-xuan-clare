<#
.SYNOPSIS
  Waddle QA report: lint, format, unit+integration, security, e2e.
  Prints a summary in the terminal and writes qa-report.html.

.EXAMPLE
  ./qa-report.ps1              # run everything
  ./qa-report.ps1 -SkipE2E     # skip FlaUI UI tests (e.g. no desktop session)
#>
param(
  [string]$Solution = 'Waddle.slnx',
  [string]$UnitProject = 'Waddle.Tests/Waddle.Tests.csproj',
  [string]$E2EProject = 'Waddle.UITests/Waddle.UITests.csproj',
  [string]$OutFile = 'qa-report.html',
  [switch]$SkipE2E
)

$ErrorActionPreference = 'Continue'
$results = @()
$resultsDir = Join-Path (Get-Location) 'TestResults/qa'
if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }
New-Item -ItemType Directory -Path $resultsDir | Out-Null

function Add-Result($stage, $check, $status, $summary, $details = '', $seconds = 0) {
  $script:results += [pscustomobject]@{
    Stage = $stage; Check = $check; Status = $status
    Summary = $summary; Details = $details; Seconds = [math]::Round($seconds, 1)
  }
  $color = @{ PASS = 'Green'; FAIL = 'Red'; SKIP = 'Yellow' }[$status]
  Write-Host ("  [{0}] {1} - {2}" -f $status, $check, $summary) -ForegroundColor $color
}

function Invoke-Dotnet([string[]]$dotnetArgs) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $out = & dotnet @dotnetArgs 2>&1 | Out-String
  [pscustomobject]@{ Code = $LASTEXITCODE; Output = $out; Seconds = $sw.Elapsed.TotalSeconds }
}

function Read-Trx($path) {
  if (-not (Test-Path $path)) { return $null }
  $x = [xml](Get-Content $path -Raw)
  $r = @($x.TestRun.Results.UnitTestResult)
  [pscustomobject]@{
    Total  = $r.Count
    Passed = @($r | Where-Object outcome -eq 'Passed').Count
    Failed = @($r | Where-Object outcome -eq 'Failed').Count
    Skipped = @($r | Where-Object { $_.outcome -in 'NotExecuted', 'Skipped' }).Count
    FailedNames = @($r | Where-Object outcome -eq 'Failed' | ForEach-Object { $_.testName })
  }
}

function Invoke-Tests($stage, $check, $project, $filter, $trxName, [switch]$Coverage) {
  $a = @('test', $project, '--configuration', 'Release',
    '--logger', "trx;LogFileName=$trxName.trx", '--results-directory', $resultsDir)
  if ($filter) { $a += @('--filter', $filter) }
  if ($Coverage) { $a += @('--collect:XPlat Code Coverage') }
  $r = Invoke-Dotnet $a
  $t = Read-Trx (Join-Path $resultsDir "$trxName.trx")
  if (-not $t) {
    Add-Result $stage $check 'FAIL' 'No test results produced (build or run error)' $r.Output $r.Seconds
    return
  }
  $summary = "$($t.Passed)/$($t.Total) passed, $($t.Failed) failed, $($t.Skipped) skipped"
  if ($Coverage) {
    $cov = Get-ChildItem $resultsDir -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
    if ($cov) {
      $rate = [double](([xml](Get-Content $cov.FullName -Raw)).coverage.'line-rate')
      $summary += ", line coverage {0:P1}" -f $rate
    }
  }
  $details = if ($t.Failed) { "Failed tests:`n" + ($t.FailedNames -join "`n") } else { '' }
  $ok = ($r.Code -eq 0 -and $t.Failed -eq 0 -and $t.Total -gt 0)
  Add-Result $stage $check ($(if ($ok) { 'PASS' } else { 'FAIL' })) $summary $details $r.Seconds
}

# ---------------------------------------------------------------- 1. LINT
Write-Host "`n== 1. Lint ==" -ForegroundColor Cyan
$r = Invoke-Dotnet @('build', $Solution, '--configuration', 'Debug', '--no-incremental',
  '-warnaserror', '-p:EnableNETAnalyzers=true', '-p:AnalysisLevel=latest-recommended',
  '-p:EnforceCodeStyleInBuild=true')
$diag = ($r.Output -split "`n" | Where-Object { $_ -match '\b(warning|error) [A-Z]+\d+' } |
  ForEach-Object { $_.Trim() } | Sort-Object -Unique)
$sum = if ($r.Code -eq 0) { 'No analyzer warnings or errors' } else { "$($diag.Count) distinct diagnostics" }
Add-Result 'Lint' 'Roslyn + .NET analyzers (warnings as errors)' ($(if ($r.Code -eq 0) { 'PASS' } else { 'FAIL' })) $sum ($diag -join "`n") $r.Seconds

# ---------------------------------------------------------------- 2. FORMAT
Write-Host "`n== 2. Format check ==" -ForegroundColor Cyan
$r = Invoke-Dotnet @('format', $Solution, '--verify-no-changes')
$files = ($r.Output -split "`n" | Where-Object { $_ -match '^\s*\S+\.(cs|xaml|csproj)\b' } | ForEach-Object { $_.Trim() })
$sum = if ($r.Code -eq 0) { 'All files correctly formatted' } else { "Formatting issues (run: dotnet format $Solution)" }
Add-Result 'Format' 'dotnet format --verify-no-changes' ($(if ($r.Code -eq 0) { 'PASS' } else { 'FAIL' })) $sum ($(if ($r.Code -ne 0) { $r.Output.Trim() } else { '' })) $r.Seconds

# ---------------------------------------------------------------- 3. UNIT + INTEGRATION
Write-Host "`n== 3. Unit and integration tests ==" -ForegroundColor Cyan
Invoke-Tests 'Unit & Integration' 'xUnit (excluding Security namespace)' $UnitProject 'FullyQualifiedName!~Waddle.Tests.Security' 'unit' -Coverage

# ---------------------------------------------------------------- 4. SECURITY
Write-Host "`n== 4. Security ==" -ForegroundColor Cyan
Invoke-Tests 'Security' 'Security tests (log/SQL injection, schema integrity)' $UnitProject 'FullyQualifiedName~Waddle.Tests.Security' 'security'

$sw = [Diagnostics.Stopwatch]::StartNew()
$vulnOut = ''
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {
  $vulnOut += (& dotnet list $_.FullName package --vulnerable --include-transitive 2>&1 | Out-String)
}
$vuln = $vulnOut -match 'has the following vulnerable packages'
Add-Result 'Security' 'Vulnerable NuGet packages (incl. transitive)' ($(if ($vuln) { 'FAIL' } else { 'PASS' })) `
  ($(if ($vuln) { 'Known vulnerabilities found' } else { 'None found' })) ($(if ($vuln) { $vulnOut.Trim() } else { '' })) $sw.Elapsed.TotalSeconds

# gitleaks: install automatically via winget if missing
if (-not (Get-Command gitleaks -ErrorAction SilentlyContinue) -and (Get-Command winget -ErrorAction SilentlyContinue)) {
  Write-Host '  gitleaks not found, installing via winget...' -ForegroundColor Yellow
  winget install --id Gitleaks.Gitleaks -e --silent --accept-source-agreements --accept-package-agreements | Out-Null
  $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
if (Get-Command gitleaks -ErrorAction SilentlyContinue) {
  $sw.Restart()
  $g = & gitleaks detect --no-banner --redact 2>&1 | Out-String
  $gOk = ($LASTEXITCODE -eq 0)
  Add-Result 'Security' 'Secret scan (gitleaks, full git history)' ($(if ($gOk) { 'PASS' } else { 'FAIL' })) `
    ($(if ($gOk) { 'No secrets detected' } else { 'Potential secrets found' })) ($(if (-not $gOk) { $g.Trim() } else { '' })) $sw.Elapsed.TotalSeconds
} else {
  Add-Result 'Security' 'Secret scan (gitleaks)' 'SKIP' 'gitleaks could not be installed; run: winget install Gitleaks.Gitleaks'
}

# CodeQL: read the latest open code-scanning alerts that the CI job (security.yml) uploaded to GitHub
$sw.Restart()
if (Get-Command gh -ErrorAction SilentlyContinue) {
  $alertsJson = & gh api "repos/{owner}/{repo}/code-scanning/alerts?state=open&per_page=100" 2>&1 | Out-String
  if ($LASTEXITCODE -eq 0) {
    $alerts = @($alertsJson | ConvertFrom-Json)
    $lines = $alerts | ForEach-Object { "[$($_.rule.security_severity_level)$($_.rule.severity)] $($_.rule.id): $($_.most_recent_instance.location.path):$($_.most_recent_instance.location.start_line)" }
    Add-Result 'Security' 'CodeQL (open alerts on GitHub)' ($(if ($alerts.Count) { 'FAIL' } else { 'PASS' })) `
      "$($alerts.Count) open alerts" ($lines -join "`n") $sw.Elapsed.TotalSeconds
  } else {
    Add-Result 'Security' 'CodeQL (open alerts on GitHub)' 'SKIP' 'Could not read alerts. Run: gh auth login (needs repo access and code scanning enabled)' $alertsJson
  }
} else {
  Add-Result 'Security' 'CodeQL (open alerts on GitHub)' 'SKIP' 'GitHub CLI not installed; run: winget install GitHub.cli, then gh auth login'
}

# ---------------------------------------------------------------- 5. E2E
Write-Host "`n== 5. E2E (WPF UI automation) ==" -ForegroundColor Cyan
if ($SkipE2E) {
  Add-Result 'E2E' 'FlaUI UI automation' 'SKIP' 'Skipped via -SkipE2E'
} else {
  Invoke-Tests 'E2E' 'FlaUI UI automation' $E2EProject $null 'e2e'
}

# ---------------------------------------------------------------- TERMINAL SUMMARY
$fail = @($results | Where-Object Status -eq 'FAIL').Count
$pass = @($results | Where-Object Status -eq 'PASS').Count
$skip = @($results | Where-Object Status -eq 'SKIP').Count
Write-Host "`n================ QA REPORT ================" -ForegroundColor Cyan
foreach ($g in $results | Group-Object Stage) {
  $bad = @($g.Group | Where-Object Status -eq 'FAIL').Count
  $c = if ($bad) { 'Red' } else { 'Green' }
  Write-Host ("{0,-20} {1}" -f $g.Name, $(if ($bad) { 'FAIL' } else { 'PASS' })) -ForegroundColor $c
}
Write-Host ("`nChecks: {0} passed, {1} failed, {2} skipped" -f $pass, $fail, $skip)
Write-Host ("Overall: {0}" -f $(if ($fail) { 'FAILED' } else { 'PASSED' })) -ForegroundColor $(if ($fail) { 'Red' } else { 'Green' })

# ---------------------------------------------------------------- HTML
$enc = { param($s) [System.Net.WebUtility]::HtmlEncode([string]$s) }
$rows = foreach ($r in $results) {
  $d = if ($r.Details) { "<details><summary>details</summary><pre>$(& $enc $r.Details)</pre></details>" } else { '' }
  "<tr><td>$(& $enc $r.Stage)</td><td>$(& $enc $r.Check)</td><td><span class=""b $($r.Status.ToLower())"">$($r.Status)</span></td><td>$(& $enc $r.Summary)$d</td><td class=""n"">$($r.Seconds)s</td></tr>"
}
$overall = if ($fail) { 'FAILED' } else { 'PASSED' }
$cls = if ($fail) { 'fail' } else { 'pass' }
$commit = (& git rev-parse --short HEAD 2>$null)
$html = @"
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Waddle QA Report</title>
<style>
:root{--bg:#fff;--fg:#1a1a1a;--mut:#666;--line:#e2e2e2;--pass:#1a7f37;--fail:#cf222e;--skip:#9a6700;--card:#f6f8fa}
@media (prefers-color-scheme:dark){:root{--bg:#0d1117;--fg:#e6edf3;--mut:#8b949e;--line:#30363d;--pass:#3fb950;--fail:#f85149;--skip:#d29922;--card:#161b22}}
body{font:15px/1.5 system-ui,sans-serif;background:var(--bg);color:var(--fg);max-width:960px;margin:2rem auto;padding:0 1rem}
h1{margin:0 0 .25rem}.meta{color:var(--mut);margin-bottom:1.25rem}
.sum{display:flex;gap:.75rem;flex-wrap:wrap;margin-bottom:1.5rem}
.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:.75rem 1rem;min-width:110px}
.card b{display:block;font-size:1.5rem}
.pass{color:var(--pass)}.fail{color:var(--fail)}.skip{color:var(--skip)}
.wrap{overflow-x:auto}table{border-collapse:collapse;width:100%}
th,td{text-align:left;padding:.5rem .6rem;border-bottom:1px solid var(--line);vertical-align:top}
.b{font-weight:600;font-size:.8rem;border:1px solid currentColor;border-radius:99px;padding:.1rem .55rem}
.n{color:var(--mut);white-space:nowrap}
pre{white-space:pre-wrap;background:var(--card);padding:.5rem;border-radius:6px;max-height:300px;overflow:auto;font-size:.8rem}
</style></head><body>
<h1>Waddle QA Report</h1>
<div class="meta">$(Get-Date -Format 'yyyy-MM-dd HH:mm') &middot; commit $commit &middot; overall <b class="$cls">$overall</b></div>
<div class="sum">
<div class="card"><b class="pass">$pass</b>passed</div>
<div class="card"><b class="fail">$fail</b>failed</div>
<div class="card"><b class="skip">$skip</b>skipped</div>
</div>
<div class="wrap"><table>
<tr><th>Stage</th><th>Check</th><th>Status</th><th>Result</th><th>Time</th></tr>
$($rows -join "`n")
</table></div>
</body></html>
"@
Set-Content -Path $OutFile -Value $html -Encoding UTF8
Write-Host "`nHTML report: $(Resolve-Path $OutFile)" -ForegroundColor Cyan
exit ([int]($fail -gt 0))