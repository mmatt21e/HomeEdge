namespace HomeStock.Web.Services;

public enum ToastLevel { Success, Info, Warning, Error }

public record ToastMessage(Guid Id, ToastLevel Level, string Text, int DurationMs);

/// <summary>
/// Simple in-memory, per-circuit toast bus. Components raise toasts; the ToastHost subscribes
/// and renders them. Scoped so each Blazor circuit (user session) has its own queue.
/// </summary>
public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(ToastLevel level, string text, int durationMs = 4000)
        => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), level, text, durationMs));

    public void Success(string text) => Show(ToastLevel.Success, text);
    public void Info(string text) => Show(ToastLevel.Info, text);
    public void Warning(string text, int durationMs = 6000) => Show(ToastLevel.Warning, text, durationMs);
    public void Error(string text, int durationMs = 8000) => Show(ToastLevel.Error, text, durationMs);
}
