using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;

namespace PA_Website.Services
{
    public interface IPromotionService
    {
        Task<decimal> CalculatePricePaid(string userId, Service service);
        Task<(decimal pricePaid, List<Promotion> usedPromotions)> CalculatePricePaidWithTracking(string userId, Service service);
    }

    public class PromotionService : IPromotionService
    {
        private readonly ApplicationDbContext _context;

        public PromotionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculatePricePaid(string userId, Service service)
        {
            var (pricePaid, _) = await CalculatePricePaidWithTracking(userId, service);
            return pricePaid;
        }

        public async Task<(decimal pricePaid, List<Promotion> usedPromotions)> CalculatePricePaidWithTracking(string userId, Service service)
        {
            decimal pricePaid = service.Price;
            var usedPromotions = new List<Promotion>();
            
            // Get all active promotions that are currently valid
            var activePromotions = await _context.Promotions
                .Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Check for first booking promotion
            var hasReservations = await _context.userServices.AnyAsync(us => us.UserId == userId);
            var firstBookingPromo = activePromotions.FirstOrDefault(p => p.PromotionType == "FirstBooking");
            bool isEligibleForFirstBookingPromo = !hasReservations && firstBookingPromo != null;
            
            if (isEligibleForFirstBookingPromo)
            {
                if (firstBookingPromo.DiscountPercentage.HasValue)
                {
                    pricePaid = Math.Round(pricePaid * (1 - (firstBookingPromo.DiscountPercentage.Value / 100)), 2);
                }
                else if (firstBookingPromo.FixedDiscount.HasValue)
                {
                    pricePaid = Math.Max(0, Math.Round(pricePaid - firstBookingPromo.FixedDiscount.Value, 2));
                }
                usedPromotions.Add(firstBookingPromo);
            }

            // Check for general discount promotions
            var discountPromo = activePromotions.FirstOrDefault(p => p.PromotionType == "Discount");
            if (discountPromo != null)
            {
                // Check if user has used this promotion before
                var hasUsedPromotion = await _context.UserPromotions
                    .AnyAsync(up => up.UserId == userId && up.PromotionId == discountPromo.Id);
                
                // Check usage limits
                bool canUsePromotion = !discountPromo.MaxUsage.HasValue || 
                                     discountPromo.UsedCount < discountPromo.MaxUsage.Value;
                
                if (!hasUsedPromotion && canUsePromotion)
                {
                    if (discountPromo.DiscountPercentage.HasValue)
                    {
                        pricePaid = Math.Round(pricePaid * (1 - (discountPromo.DiscountPercentage.Value / 100)), 2);
                    }
                    else if (discountPromo.FixedDiscount.HasValue)
                    {
                        pricePaid = Math.Max(0, Math.Round(pricePaid - discountPromo.FixedDiscount.Value, 2));
                    }
                    usedPromotions.Add(discountPromo);
                }
            }

            // Check for loyalty promotions
            var loyaltyPromo = activePromotions.FirstOrDefault(p => p.PromotionType == "Loyalty");
            if (loyaltyPromo != null)
            {
                // Count user's completed reservations
                var completedReservations = await _context.userServices
                    .CountAsync(us => us.UserId == userId && us.Status == "Completed");
                
                // Check if user has used this promotion before
                var hasUsedPromotion = await _context.UserPromotions
                    .AnyAsync(up => up.UserId == userId && up.PromotionId == loyaltyPromo.Id);
                
                // Check usage limits
                bool canUsePromotion = !loyaltyPromo.MaxUsage.HasValue || 
                                     loyaltyPromo.UsedCount < loyaltyPromo.MaxUsage.Value;
                
                // Apply loyalty discount if user has completed reservations and hasn't used this promotion
                if (completedReservations > 0 && !hasUsedPromotion && canUsePromotion)
                {
                    if (loyaltyPromo.DiscountPercentage.HasValue)
                    {
                        pricePaid = Math.Round(pricePaid * (1 - (loyaltyPromo.DiscountPercentage.Value / 100)), 2);
                    }
                    else if (loyaltyPromo.FixedDiscount.HasValue)
                    {
                        pricePaid = Math.Max(0, Math.Round(pricePaid - loyaltyPromo.FixedDiscount.Value, 2));
                    }
                    usedPromotions.Add(loyaltyPromo);
                }
            }

            return (pricePaid, usedPromotions);
        }
    }
} 