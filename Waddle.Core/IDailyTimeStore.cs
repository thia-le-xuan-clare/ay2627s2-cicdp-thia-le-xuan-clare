namespace Waddle.Core;

/// Saves and loads how many seconds were sat up on each day.
public interface IDailyTimeStore
{
    /// Seconds saved for that day, or 0 if there is no row yet.
    int GetSeconds(DateOnly day);

    /// Sets the saved seconds for that day (creates the row if needed).
    void SetSeconds(DateOnly day, int seconds);
}