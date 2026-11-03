using Waddle.Infrastructure;
using Xunit;

public class FileLogServiceTests
{
    [Fact]
    public void Info_And_Security_Write_To_Separate_Files()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var log = new FileLogService(dir);

        log.Info("hello");
        log.Security("camera on");

        Assert.Contains("hello", File.ReadAllText(Path.Combine(dir, "info.log")));
        Assert.Contains("camera on", File.ReadAllText(Path.Combine(dir, "security.log")));
    }
}
