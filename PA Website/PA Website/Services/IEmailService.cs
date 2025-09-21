using PA_Website.Models;

namespace PA_Website.Services
{
    public interface IEmailService
    {
        Task SendReservationConfirmationAsync(User user, UserService reservation, Service service);
        Task SendReservationCancellationAsync(User user, UserService reservation, Service service);
        Task SendReservationRescheduleAsync(User user, UserService oldReservation, UserService newReservation, Service service);
        Task SendStatusUpdateAsync(User user, UserService reservation, Service service, string oldStatus, string newStatus);
        Task SendAstroCardCompletionAsync(User user, UserService reservation, Service service);
        Task SendAdminNotificationAsync(User user, UserService reservation, Service service, string action);
        Task SendBulkEmailAsync(List<EmailRecipient> recipients, string subject, string message, string emailType);
        Task SendCustomEmailAsync(User user, UserService reservation, Service service, string subject, string message, string emailType);
    }
} 