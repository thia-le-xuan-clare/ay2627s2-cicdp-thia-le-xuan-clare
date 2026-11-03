namespace Waddle.Core;

public interface ILogService
{
    void Info(string message);
    void Security(string message);
}