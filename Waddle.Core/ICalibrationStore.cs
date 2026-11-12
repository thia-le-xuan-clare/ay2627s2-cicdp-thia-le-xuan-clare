namespace Waddle.Core;

/// <summary>The face size captured during calibration (FaceRatio = share of the camera frame the face fills, 0-1).</summary>
public sealed record CalibrationData(double FaceRatio, DateTime CalibratedAt);

public interface ICalibrationStore
{
    /// <summary>Null when the user has never calibrated.</summary>
    CalibrationData? Load();
    void Save(CalibrationData data);
}
