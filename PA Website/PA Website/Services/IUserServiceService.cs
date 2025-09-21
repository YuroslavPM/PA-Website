using PA_Website.Models;

namespace PA_Website.Services
{
    public interface IUserServiceService
    {
        Task<IEnumerable<UserService>> GetUserReservationsAsync(string userId, ReservationFilter filter);
        Task<IEnumerable<UserService>> GetAllReservationsAsync(ReservationFilter filter);
        Task<UserService?> GetReservationByIdAsync(int id);
        Task<UserService> CreateReservationAsync(CreateReservationRequest request);
        Task<UserService> UpdateReservationAsync(int id, UpdateReservationRequest request);
        Task<bool> DeleteReservationAsync(int id);
        Task<bool> UpdateReservationStatusAsync(int id, string newStatus);
        Task<bool> CancelReservationAsync(int id, string userId);
        Task<bool> RescheduleReservationAsync(int id, RescheduleRequest request, string userId);
        Task<(UserService OldReservation, UserService NewReservation)?> RescheduleReservationWithDetailsAsync(int id, RescheduleRequest request, string userId);
        Task<bool> UploadAstroCardAsync(int id, IFormFile file);
        Task<bool> DeleteAstroCardAsync(int id);
        Task<AstroCardFileResult?> DownloadAstroCardAsync(int id);
        Task<IEnumerable<TimeSpan>> GetAvailableTimesAsync(int serviceId, DateTime date);
        Task<FinancialReport> GetFinancialReportAsync(int? year, int? month);
        Task<bool> UserServiceExistsAsync(int id);
    }
} 