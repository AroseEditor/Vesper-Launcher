namespace Vesper.Core.Diagnostics;

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record AppError(
    string Title,
    string Detail,
    ErrorSeverity Severity,
    DateTimeOffset Timestamp)
{
    public string TimeLabel => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string ClipboardText =>
        $"[{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}] {Severity}: {Title}" +
        (string.IsNullOrWhiteSpace(Detail) ? string.Empty : Environment.NewLine + Detail);
}

public sealed class ErrorService
{
    public const int MaxEntries = 200;

    public static ErrorService Shared { get; } = new();

    private readonly object _gate = new();
    private readonly List<AppError> _errors = [];

    public event EventHandler<AppError>? Reported;

    public event EventHandler? Cleared;

    public IReadOnlyList<AppError> Snapshot()
    {
        lock (_gate)
            return _errors.ToList();
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _errors.Count;
        }
    }

    public AppError Report(string title, string detail = "", ErrorSeverity severity = ErrorSeverity.Error)
    {
        var error = new AppError(title, detail ?? string.Empty, severity, DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _errors.Insert(0, error);

            while (_errors.Count > MaxEntries)
                _errors.RemoveAt(_errors.Count - 1);
        }

        Reported?.Invoke(this, error);
        return error;
    }

    public AppError Report(string title, Exception exception, ErrorSeverity severity = ErrorSeverity.Error) =>
        Report(title, Describe(exception), severity);

    public void Clear()
    {
        lock (_gate)
            _errors.Clear();

        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public string CopyAll()
    {
        lock (_gate)
            return string.Join(
                Environment.NewLine + Environment.NewLine,
                _errors.Select(e => e.ClipboardText));
    }

    public static string Describe(Exception exception)
    {
        var lines = new List<string>();
        var current = exception;

        while (current is not null)
        {
            lines.Add(current.GetType().Name + ": " + current.Message);
            current = current.InnerException;
        }

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            lines.Add(string.Empty);
            lines.Add(exception.StackTrace);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
