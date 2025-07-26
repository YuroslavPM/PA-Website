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
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<UserPromotion> UserPromotions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Promotion decimals
            modelBuilder.Entity<Promotion>()
                .Property(p => p.DiscountPercentage)
                .HasPrecision(5, 2); // e.g. 99.99%

            modelBuilder.Entity<Promotion>()
                .Property(p => p.FixedDiscount)
                .HasPrecision(18, 2); // e.g. 99999999.99

            // Service price
            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasPrecision(18, 2);

            // Prevent cascade delete for UserService and UserPromotion
            modelBuilder.Entity<UserService>()
                .HasOne(us => us.User)
                .WithMany()
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UserPromotion>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
