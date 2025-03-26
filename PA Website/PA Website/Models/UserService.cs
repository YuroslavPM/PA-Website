using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace PA_Website.Models
{
    public class UserService
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ServiceId { get; set; }
        public DateTime? AstrologicalDate { get; set; } // Add this property
        public DateTime ReservationDate { get; set; }
        public TimeSpan? ReservationTime { get; set; }

        // Navigation properties
        public User User { get; set; }
        public Service Service { get; set; }
    }
}
