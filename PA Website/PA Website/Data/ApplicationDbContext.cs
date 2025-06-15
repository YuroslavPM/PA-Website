using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PA_Website.Models;

namespace PA_Website.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Article> Articles { get; set; }
        public DbSet<PA_Website.Models.Service> Service { get; set; } = default!;
        public DbSet<UserService> userServices { get; set; }
    }
}
