namespace DDCore.Core.Logging;

public interface IDDLogger
{
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class NullDDLogger : IDDLogger
{
    public static NullDDLogger Instance { get; } = new();

    private NullDDLogger()
    {
    }

    public void Debug(string message)
    {
    }

    public void Information(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
