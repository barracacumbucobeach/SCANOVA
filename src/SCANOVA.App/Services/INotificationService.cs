namespace SCANOVA.App.Services;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record AppNotification(string Message, NotificationSeverity Severity);

/// <summary>
/// Notificações discretas de sucesso/erro (seção 97) — nunca popups modais em excesso.
/// MainWindow assina <see cref="Notified"/> e mostra um InfoBar transitório.
/// </summary>
public interface INotificationService
{
    event EventHandler<AppNotification>? Notified;

    void ShowInfo(string message);
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}
