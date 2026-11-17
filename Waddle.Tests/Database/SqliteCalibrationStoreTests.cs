using Waddle.Core;
using Waddle.Infrastructure.Data;

namespace Waddle.Tests;

public sealed class SqliteCalibrationStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly SqliteCalibrationStore _store;

    public SqliteCalibrationStoreTests() => _store = new SqliteCalibrationStore(_temp.Db);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_WhenNeverCalibrated_ReturnsNull()
    {
        Assert.Null(_store.Load());
    }

    [Fact]
    public void Save_ThenLoad_ReturnsTheSameValues()
    {
        var data = new CalibrationData(0.2234, new DateTime(2026, 11, 12, 14, 30, 5, DateTimeKind.Local));

        _store.Save(data);
        var loaded = _store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(data.FaceRatio, loaded!.FaceRatio);
        Assert.Equal(data.CalibratedAt, loaded.CalibratedAt);
    }

    [Fact]
    public void Save_Twice_KeepsOnlyTheLatestCalibration()
    {
        _store.Save(new CalibrationData(0.20, new DateTime(2026, 11, 1)));
        _store.Save(new CalibrationData(0.31, new DateTime(2026, 11, 12)));

        var loaded = _store.Load();

        Assert.Equal(0.31, loaded!.FaceRatio);
        Assert.Equal(1, _temp.Scalar<int>("SELECT COUNT(*) FROM settings WHERE settings_key = 1"));
    }

    [Fact]
    public void Load_FromANewStoreInstance_FindsTheSavedCalibration()
    {
        // simulates closing and reopening the app: same database, brand new store object
        _store.Save(new CalibrationData(0.25, DateTime.Now));

        var reopened = new SqliteCalibrationStore(_temp.Db);

        Assert.NotNull(reopened.Load());
    }

    [Fact]
    public void Load_WhenTheSavedDateIsGarbage_TreatsItAsNeverCalibrated()
    {
        _temp.Execute("INSERT INTO settings (settings_key, face_value, calibrated_at) VALUES (1, 0.2, 'not a date')");

        Assert.Null(_store.Load());
    }
}
