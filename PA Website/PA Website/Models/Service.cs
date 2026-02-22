using System.ComponentModel.DataAnnotations;

namespace PA_Website.Models
{
    public class Service
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string NameService { get; set; }
        [Required]
        public string CategoryOfService { get; set; }

        public decimal Price { get; set; }

        
        public DateTime ReservationDate { get; set; }

        public string Description { get; set; } 

        public string? ImagePath { get; set; }
        public string Slug { get; set; } = string.Empty;

    }
}
