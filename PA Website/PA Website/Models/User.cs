using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using PA_Website.Attributes;

namespace PA_Website.Models
{
    public class User:IdentityUser  
    {

        [Required]
        [StringLength(50)]
        public string FName { get; set; }
        [Required]
        [StringLength(50)]
        public string LName { get; set; }


        public string Zodiacal_Sign { get; set; }
        [Required(ErrorMessage = "Датата на раждане е задължителна")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата на раждане")]
        [MinimumAge(16, ErrorMessage = "Трябва да сте навършили поне 16 години")]
        public DateTime Birth_Date { get; set; }

        public bool EmailSend { get; set; }



        

    }
}
