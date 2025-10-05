namespace PA_Website.Models
{
    public class ReservationFilter
    {
        public string? SortOrder { get; set; }
        public string? CategoryFilter { get; set; }
        public string? StatusFilter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public class CreateReservationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public DateTime? ReservationDate { get; set; }
        public TimeSpan? ReservationTime { get; set; }
        public DateTime? AstrologicalDate { get; set; }
        public string? AstrologicalPlaceOfBirth { get; set; }
    }

    public class UpdateReservationRequest
    {
        public int ServiceId { get; set; }
        public DateTime? ReservationDate { get; set; }
        public TimeSpan? ReservationTime { get; set; }
        public DateTime? AstrologicalDate { get; set; }
        public string? AstrologicalPlaceOfBirth { get; set; }
    }

    public class RescheduleRequest
    {
        public DateTime NewDate { get; set; }
        public TimeSpan NewTime { get; set; }
        public int ServiceId { get; set; }
    }

    public class FinancialReport
    {
        public decimal TotalEarnings { get; set; }
        public decimal PreviousEarnings { get; set; }
        public decimal PercentChange { get; set; }
        public List<MonthlyData> MonthlyData { get; set; } = new();
        public List<ServiceEarnings> EarningsByService { get; set; } = new();
    }

    public class MonthlyData
    {
        public int Month { get; set; }
        public decimal Earnings { get; set; }
    }

    public class ServiceEarnings
    {
        public string Service { get; set; } = string.Empty;
        public decimal Earnings { get; set; }
    }

    public class EmailRecipient
    {
        public string FName { get; set; } = string.Empty;
        public string LName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsUser { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string FName { get; set; } = string.Empty;
        public string LName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
    }

    public class AstroCardFileResult
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
} 