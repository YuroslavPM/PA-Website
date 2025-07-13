using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA_Website.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [StringLength(60)]
        public string Title { get; set; }
        [ForeignKey(nameof(User))]
        public string? CreatorId { get; set; }
        public User? Creator { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public DateTime PublicationDate { get; set; }
        [Required]
        [StringLength(30)]
        public string Category { get; set; }
        public string? ImagePath { get; set; }      

        [NotMapped] 
        public IFormFile? ImageFile { get; set; }
    }
}
