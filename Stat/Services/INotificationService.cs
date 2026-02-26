// In Services/INotificationService.cs
using Stat.Models.ViewModels;

namespace Stat.Services
{
    public interface INotificationService
    {
        Task<NotificationResultViewModel> GetNotificationsAsync();
    }
}