using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace PA_Website.Models
{
    public class UserService
    {
        public int Id { get; set; }
        [ForeignKey("UserId")]
        public string? UserId { get; set; }
        public int ServiceId { get; set; }
        public DateTime? AstrologicalDate { get; set; } // Add this property
        public DateTime ReservationDate { get; set; }
        public TimeSpan? ReservationTime { get; set; }

        public string AstrologicalPlaceOfBirth { get; set; }

        public string Status { get; set; } = "Pending";

        public string? AstroCardFileName { get; set; }
        public string? AstroCardFilePath { get; set; }
        public long? AstroCardFileSize { get; set; }
        public string? AstroCardContentType { get; set; }
        public DateTime? AstroCardUploadDate { get; set; }

        public decimal PricePaid { get; set; }

        // Navigation properties
        public User User { get; set; }
        public Service Service { get; set; }
    }
}
