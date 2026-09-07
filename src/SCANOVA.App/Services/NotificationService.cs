namespace SCANOVA.App.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<AppNotification>? Notified;

    public void ShowInfo(string message) => Notified?.Invoke(this, new AppNotification(message, NotificationSeverity.Info));

    public void ShowSuccess(string message) => Notified?.Invoke(this, new AppNotification(message, NotificationSeverity.Success));

    public void ShowWarning(string message) => Notified?.Invoke(this, new AppNotification(message, NotificationSeverity.Warning));

    public void ShowError(string message) => Notified?.Invoke(this, new AppNotification(message, NotificationSeverity.Error));
}
