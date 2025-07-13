using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

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
        [Required]
        public DateTime Birth_Date { get; set; }




        

    }
}
