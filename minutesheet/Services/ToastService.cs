namespace minutesheet.Services;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public sealed class ToastItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public ToastType Type { get; set; }
    public string Message { get; set; } = "";
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Scoped toast bus. Interactive components call Show*() to raise a toast that
/// <see cref="Components.ToastHost"/> renders. Lives for the whole circuit, so
/// toasts survive SPA navigation.
/// </summary>
public sealed class ToastService
{
    public const int ToastDurationSeconds = 5;
    private const int MaxToasts = 8;

    private readonly object _lock = new();
    private readonly List<ToastItem> _items = new();

    public event Action? OnChange;

    public IReadOnlyList<ToastItem> Items
    {
        get
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    public void Show(ToastType type, string message, string? title = null)
    {
        lock (_lock)
        {
            _items.Add(new ToastItem { Type = type, Message = message, Title = title });
            if (_items.Count > MaxToasts)
            {
                _items.RemoveAt(0);
            }
        }
        Notify();
    }

    public void ShowSuccess(string message, string? title = null) => Show(ToastType.Success, message, title);    public void ShowError(string message, string? title = null) => Show(ToastType.Error, message, title);
    public void ShowWarning(string message, string? title = null) => Show(ToastType.Warning, message, title);
    public void ShowInfo(string message, string? title = null) => Show(ToastType.Info, message, title);

    public void Remove(Guid id)
    {
        bool changed;
        lock (_lock)
        {
            changed = _items.RemoveAll(i => i.Id == id) > 0;
        }
        if (changed)
        {
            Notify();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
        Notify();
    }

    // Never let a subscriber failure break the caller (e.g. a Save() handler).
    private void Notify()
    {
        try
        {
            OnChange?.Invoke();
        }
        catch
        {
        }
    }
}
