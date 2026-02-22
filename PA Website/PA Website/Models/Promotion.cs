using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA_Website.Models
{
    public class Promotion
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Заглавието е задължително")]
        [Display(Name = "Заглавие")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Описанието е задължително")]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Типът на промоцията е задължителен")]
        [Display(Name = "Тип на промоцията")]
        public string PromotionType { get; set; } // "FirstBooking", "Discount", "FreeService", etc.

        [Display(Name = "Процент отстъпка")]
        [Range(0, 100, ErrorMessage = "Процентът трябва да е между 0 и 100")]
        public decimal? DiscountPercentage { get; set; }

        [Display(Name = "Фиксирана отстъпка (€)")]
        [Range(0, double.MaxValue, ErrorMessage = "Сумата трябва да е положителна")]
        public decimal? FixedDiscount { get; set; }

        [Display(Name = "Безплатна услуга")]
        public string? FreeServiceName { get; set; }

        [Required(ErrorMessage = "Датата на начало е задължителна")]
        [Display(Name = "Дата на начало")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Датата на край е задължителна")]
        [Display(Name = "Дата на край")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Максимален брой използвания")]
        [Range(1, int.MaxValue, ErrorMessage = "Броят трябва да е положителен")]
        public int? MaxUsage { get; set; }

        [Display(Name = "Използвани пъти")]
        public int UsedCount { get; set; } = 0;

        [Display(Name = "Активна")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Създадена на")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Обновена на")]
        public DateTime? UpdatedAt { get; set; }

        public string Slug { get; set; } = string.Empty;

        // Navigation properties
        public virtual ICollection<UserPromotion> UserPromotions { get; set; } = new List<UserPromotion>();
    }

    public class UserPromotion
    {
        public int Id { get; set; }
        
        [ForeignKey("UserId")]
        public string? UserId { get; set; }
        public virtual User? User { get; set; }
        
        public int PromotionId { get; set; }
        public virtual Promotion Promotion { get; set; }
        
        [Display(Name = "Използвана на")]
        public DateTime UsedAt { get; set; } = DateTime.Now;
        
        [Display(Name = "Приложена към резервация")]
        public int? UserServiceId { get; set; }
        public virtual UserService? UserService { get; set; }
    }
} 