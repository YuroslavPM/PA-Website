using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;

namespace PA_Website.Services
{
    public class UserServiceService : IUserServiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPromotionService _promotionService;
        private readonly ILogger<UserServiceService> _logger;

        public UserServiceService(ApplicationDbContext context, IPromotionService promotionService, ILogger<UserServiceService> logger)
        {
            _context = context;
            _promotionService = promotionService;
            _logger = logger;
        }

        public async Task<IEnumerable<UserService>> GetUserReservationsAsync(string userId, ReservationFilter filter)
        {
            var query = _context.userServices
                .Where(u => u.UserId == userId)
                .Include(u => u.Service)
                .AsNoTracking() // Improve performance for read-only operations
                .AsQueryable();

            query = ApplyFilters(query, filter);
            var result = await query.ToListAsync();
            
            // Mark expired reservations
            MarkExpiredReservations(result);
            
            // Apply status filter after marking expired
            if (!string.IsNullOrEmpty(filter.StatusFilter))
            {
                result = ApplyStatusFilter(result, filter.StatusFilter);
            }

            return result
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize);
        }

        public async Task<IEnumerable<UserService>> GetAllReservationsAsync(ReservationFilter filter)
        {
            var query = _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .AsNoTracking() // Improve performance for read-only operations
                .AsQueryable();

            query = ApplyFilters(query, filter);
            var result = await query.ToListAsync();
            
            // Mark expired reservations
            MarkExpiredReservations(result);
            
            // Apply status filter after marking expired
            if (!string.IsNullOrEmpty(filter.StatusFilter))
            {
                result = ApplyStatusFilter(result, filter.StatusFilter);
            }

            return result
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize);
        }

        public async Task<UserService?> GetReservationByIdAsync(int id)
        {
            return await _context.userServices
                .Include(u => u.User)
                .Include(u => u.Service)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<UserService> CreateReservationAsync(CreateReservationRequest request)
        {
            var service = await _context.Service.FindAsync(request.ServiceId);
            if (service == null)
                throw new ArgumentException("Service not found");

            ValidateReservationRequest(request, service);

            var userService = new UserService
            {
                UserId = request.UserId,
                ServiceId = request.ServiceId,
                ReservationDate = service.CategoryOfService.ToLower() == "астрология" 
                    ? DateTime.MinValue 
                    : request.ReservationDate ?? DateTime.MinValue,
                ReservationTime = service.CategoryOfService.ToLower() == "астрология" 
                    ? null 
                    : request.ReservationTime,
                AstrologicalDate = service.CategoryOfService.ToLower() == "астрология" 
                    ? request.AstrologicalDate 
                    : DateTime.MinValue,
                AstrologicalPlaceOfBirth = request.AstrologicalPlaceOfBirth ?? string.Empty,
                Status = "Pending"
            };

            // Calculate price with promotions
            var (pricePaid, usedPromotions) = await _promotionService.CalculatePricePaidWithTracking(request.UserId, service);
            userService.PricePaid = pricePaid;

            _context.userServices.Add(userService);
            await _context.SaveChangesAsync();

            // Record promotion usage
            await RecordPromotionUsageAsync(userService, usedPromotions);

            return userService;
        }

        public async Task<UserService> UpdateReservationAsync(int id, UpdateReservationRequest request)
        {
            var userService = await _context.userServices.FindAsync(id);
            if (userService == null)
                throw new ArgumentException("Reservation not found");

            var service = await _context.Service.FindAsync(request.ServiceId);
            if (service == null)
                throw new ArgumentException("Service not found");

            // Update properties based on service type
            if (service.CategoryOfService.ToLower() == "астрология")
            {
                userService.AstrologicalDate = request.AstrologicalDate;
                userService.AstrologicalPlaceOfBirth = request.AstrologicalPlaceOfBirth ?? string.Empty;
                userService.ReservationDate = DateTime.MinValue;
                userService.ReservationTime = null;
            }
            else
            {
                userService.ReservationDate = request.ReservationDate ?? DateTime.MinValue;
                userService.ReservationTime = request.ReservationTime;
                userService.AstrologicalDate = DateTime.MinValue;
                userService.AstrologicalPlaceOfBirth = string.Empty;
            }

            userService.ServiceId = request.ServiceId;

            _context.Update(userService);
            await _context.SaveChangesAsync();

            return userService;
        }

        public async Task<bool> DeleteReservationAsync(int id)
        {
            var userService = await _context.userServices.FindAsync(id);
            if (userService == null)
                return false;

            _context.userServices.Remove(userService);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateReservationStatusAsync(int id, string newStatus)
        {
            var reservation = await _context.userServices
                .Include(r => r.User)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return false;

            var validStatuses = new[] { "Pending", "Confirmed", "Completed", "Cancelled", "Rejected" };
            if (!validStatuses.Contains(newStatus))
                return false;

            var oldStatus = reservation.Status;
            reservation.Status = newStatus;

            // Set PricePaid if it's zero (legacy data)
            if (newStatus == "Completed" && reservation.PricePaid == 0)
            {
                reservation.PricePaid = await _promotionService.CalculatePricePaid(reservation.UserId, reservation.Service);
            }

            _context.Update(reservation);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CancelReservationAsync(int id, string userId)
        {
            var reservation = await _context.userServices
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (reservation == null)
                return false;

            if (reservation.Status == "Completed" || reservation.Status == "Cancelled")
                return false;

            reservation.Status = "Cancelled";
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RescheduleReservationAsync(int id, RescheduleRequest request, string userId)
        {
            var result = await RescheduleReservationWithDetailsAsync(id, request, userId);
            return result.HasValue;
        }

        public async Task<(UserService OldReservation, UserService NewReservation)?> RescheduleReservationWithDetailsAsync(int id, RescheduleRequest request, string userId)
        {
            var reservation = await _context.userServices
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (reservation == null)
                return null;

            if (reservation.Status == "Completed" || reservation.Status == "Cancelled")
                return null;

            // Check for conflicts
            var conflictingReservation = await _context.userServices
                .Where(r => r.ServiceId == request.ServiceId &&
                            r.ReservationDate == request.NewDate.Date &&
                            r.ReservationTime == request.NewTime &&
                            r.Status != "Cancelled" &&
                            r.Id != id)
                .FirstOrDefaultAsync();

            if (conflictingReservation != null)
                return null;

            // Store old reservation details before cancellation
            var oldReservation = new UserService
            {
                Id = reservation.Id,
                UserId = reservation.UserId,
                ServiceId = reservation.ServiceId,
                ReservationDate = reservation.ReservationDate,
                ReservationTime = reservation.ReservationTime,
                AstrologicalDate = reservation.AstrologicalDate,
                AstrologicalPlaceOfBirth = reservation.AstrologicalPlaceOfBirth,
                Status = reservation.Status,
                PricePaid = reservation.PricePaid,
                AstroCardFileName = reservation.AstroCardFileName,
                AstroCardFilePath = reservation.AstroCardFilePath,
                AstroCardFileSize = reservation.AstroCardFileSize,
                AstroCardContentType = reservation.AstroCardContentType,
                AstroCardUploadDate = reservation.AstroCardUploadDate
            };

            // Cancel old reservation
            reservation.Status = "Cancelled";
            _context.Update(reservation);

            // Create new reservation
            var newReservation = new UserService
            {
                UserId = userId,
                ServiceId = request.ServiceId,
                ReservationDate = request.NewDate.Date,
                ReservationTime = request.NewTime,
                Status = "Pending",
                AstrologicalDate = reservation.AstrologicalDate,
                AstrologicalPlaceOfBirth = reservation.AstrologicalPlaceOfBirth,
                AstroCardFileName = reservation.AstroCardFileName,
                AstroCardFilePath = reservation.AstroCardFilePath,
                AstroCardFileSize = reservation.AstroCardFileSize,
                AstroCardContentType = reservation.AstroCardContentType,
                AstroCardUploadDate = reservation.AstroCardUploadDate,
                PricePaid = reservation.PricePaid // Preserve the price paid
            };

            _context.userServices.Add(newReservation);
            await _context.SaveChangesAsync();

            // Update the new reservation with the actual ID from database
            newReservation.Id = newReservation.Id; // This will be set by EF Core

            return (oldReservation, newReservation);
        }

        public async Task<bool> UploadAstroCardAsync(int id, IFormFile file)
        {
            var reservation = await _context.userServices
                .Include(u => u.Service)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (reservation == null)
                return false;

            if (reservation.Service.CategoryOfService.ToLower() != "астрология")
                return false;

            // File validation would be handled by FileService
            // This is just the database update part
            reservation.AstroCardFileName = file.FileName;
            reservation.AstroCardFileSize = file.Length;
            reservation.AstroCardContentType = file.ContentType;
            reservation.AstroCardUploadDate = DateTime.Now;
            reservation.Status = "Completed";

            _context.Update(reservation);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAstroCardAsync(int id)
        {
            var reservation = await _context.userServices.FindAsync(id);
            if (reservation == null)
                return false;

            reservation.AstroCardFileName = null;
            reservation.AstroCardFilePath = null;
            reservation.AstroCardFileSize = null;
            reservation.AstroCardContentType = null;
            reservation.AstroCardUploadDate = null;

            _context.Update(reservation);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<AstroCardFileResult?> DownloadAstroCardAsync(int id)
        {
            var reservation = await _context.userServices.FindAsync(id);
            if (reservation == null || string.IsNullOrEmpty(reservation.AstroCardFilePath))
                return null;

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", reservation.AstroCardFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(path))
                return null;

            var memory = new MemoryStream();
            using (var stream = new FileStream(path, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }

            return new AstroCardFileResult
            {
                Content = memory.ToArray(),
                ContentType = reservation.AstroCardContentType ?? "application/octet-stream",
                FileName = reservation.AstroCardFileName ?? "astro-card"
            };
        }

        public async Task<IEnumerable<TimeSpan>> GetAvailableTimesAsync(int serviceId, DateTime date)
        {
            var service = await _context.Service
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceId);
                
            if (service == null)
                return Enumerable.Empty<TimeSpan>();

            var allowedTimes = GetAllowedTimesForDay(date.DayOfWeek);
            
            // Use more efficient query with projection
            var reservedTimes = await _context.userServices
                .AsNoTracking()
                .Where(r => r.ServiceId == serviceId &&
                            r.ReservationTime.HasValue &&
                            r.ReservationDate.Date == date.Date &&
                            r.Status != "Cancelled")
                .Select(r => r.ReservationTime!.Value)
                .Distinct()
                .ToListAsync();

            return allowedTimes.Where(time => !reservedTimes.Contains(time));
        }

        public async Task<FinancialReport> GetFinancialReportAsync(int? year, int? month)
        {
            var completedStatuses = new[] { "Completed" };
            var query = _context.userServices
                .Include(u => u.Service)
                .Where(r => completedStatuses.Contains(r.Status));

            if (year.HasValue)
                query = query.Where(r => r.ReservationDate.Year == year.Value);

            if (month.HasValue)
                query = query.Where(r => r.ReservationDate.Month == month.Value);

            var totalEarnings = await query.SumAsync(r => r.PricePaid);
            var previousEarnings = await CalculatePreviousEarningsAsync(year, month, completedStatuses);
            var percentChange = CalculatePercentChange(totalEarnings, previousEarnings);

            var monthlyData = await GetMonthlyDataAsync(year, completedStatuses);
            var earningsByService = await GetEarningsByServiceAsync(query);

            return new FinancialReport
            {
                TotalEarnings = totalEarnings,
                PreviousEarnings = previousEarnings,
                PercentChange = percentChange,
                MonthlyData = monthlyData,
                EarningsByService = earningsByService
            };
        }

        public async Task<bool> UserServiceExistsAsync(int id)
        {
            return await _context.userServices.AnyAsync(e => e.Id == id);
        }

        #region Private Methods

        private IQueryable<UserService> ApplyFilters(IQueryable<UserService> query, ReservationFilter filter)
        {
            if (!string.IsNullOrEmpty(filter.CategoryFilter))
            {
                query = query.Where(r => r.Service.CategoryOfService.ToLower() == filter.CategoryFilter.ToLower());
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(r => r.ReservationDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(r => r.ReservationDate <= filter.EndDate.Value);
            }

            return filter.SortOrder switch
            {
                "date_desc" => query.OrderByDescending(r => r.ReservationDate),
                "date_asc" => query.OrderBy(r => r.ReservationDate),
                "service" => query.OrderBy(r => r.Service.NameService),
                "status" => query.OrderBy(r => r.Status),
                _ => query.OrderByDescending(r => r.ReservationDate)
            };
        }

        private void MarkExpiredReservations(List<UserService> reservations)
        {
            foreach (var reservation in reservations)
            {
                if (reservation.Service.CategoryOfService.ToLower() == "психология" &&
                    reservation.Status != "Completed" &&
                    reservation.Status != "Cancelled" &&
                    reservation.ReservationDate.Add(reservation.ReservationTime ?? TimeSpan.Zero).AddHours(1) < DateTime.Now)
                {
                    reservation.Status = "Expired";
                }
            }
        }

        private List<UserService> ApplyStatusFilter(List<UserService> reservations, string statusFilter)
        {
            return statusFilter == "Expired" 
                ? reservations.Where(r => r.Status == "Expired").ToList()
                : reservations.Where(r => r.Status == statusFilter).ToList();
        }

        private void ValidateReservationRequest(CreateReservationRequest request, Service service)
        {
            if (service.CategoryOfService.ToLower() == "астрология")
            {
                if (!request.AstrologicalDate.HasValue || request.AstrologicalDate.Value == DateTime.MinValue)
                    throw new ArgumentException("Valid astrological date is required for astrology services");
            }
            else
            {
                if (!request.ReservationDate.HasValue || request.ReservationDate.Value < DateTime.Now)
                    throw new ArgumentException("Future reservation date is required");

                if (!request.ReservationTime.HasValue)
                    throw new ArgumentException("Reservation time is required");

                // Check for conflicts
                var hasConflict = _context.userServices.Any(r => 
                    r.ServiceId == request.ServiceId &&
                    r.ReservationDate == request.ReservationDate.Value.Date &&
                    r.ReservationTime == request.ReservationTime.Value &&
                    r.Status != "Cancelled");

                if (hasConflict)
                    throw new ArgumentException("Selected time is already booked");
            }
        }

        private async Task RecordPromotionUsageAsync(UserService userService, List<Promotion> usedPromotions)
        {
            foreach (var promotion in usedPromotions)
            {
                var userPromotion = new UserPromotion
                {
                    UserId = userService.UserId,
                    PromotionId = promotion.Id,
                    UsedAt = DateTime.Now,
                    UserServiceId = userService.Id
                };
                _context.UserPromotions.Add(userPromotion);
                promotion.UsedCount++;
            }
            await _context.SaveChangesAsync();
        }

        private List<TimeSpan> GetAllowedTimesForDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday
                    => Enumerable.Range(9, 3).Select(hour => new TimeSpan(hour, 0, 0)).ToList(),
                DayOfWeek.Saturday
                    => Enumerable.Range(9, 5).Select(hour => new TimeSpan(hour, 0, 0)).ToList(),
                DayOfWeek.Sunday
                    => new List<TimeSpan>(), // Sunday is closed
                _ => new List<TimeSpan>()
            };
        }

        private async Task<decimal> CalculatePreviousEarningsAsync(int? year, int? month, string[] completedStatuses)
        {
            if (year.HasValue && month.HasValue)
            {
                var prevMonth = month.Value == 1 ? 12 : month.Value - 1;
                var prevYear = month.Value == 1 ? year.Value - 1 : year.Value;
                return await _context.userServices
                    .Where(r => completedStatuses.Contains(r.Status) &&
                                r.ReservationDate.Year == prevYear &&
                                r.ReservationDate.Month == prevMonth)
                    .SumAsync(r => r.PricePaid);
            }
            else if (year.HasValue)
            {
                return await _context.userServices
                    .Where(r => completedStatuses.Contains(r.Status) &&
                                r.ReservationDate.Year == year.Value - 1)
                    .SumAsync(r => r.PricePaid);
            }
            return 0;
        }

        private decimal CalculatePercentChange(decimal current, decimal previous)
        {
            if (previous > 0)
                return ((current - previous) / previous) * 100;
            return current > 0 ? 100 : 0;
        }

        private async Task<List<MonthlyData>> GetMonthlyDataAsync(int? year, string[] completedStatuses)
        {
            var query = _context.userServices.Where(r => completedStatuses.Contains(r.Status));
            if (year.HasValue)
                query = query.Where(r => r.ReservationDate.Year == year.Value);

            return await query
                .GroupBy(r => r.ReservationDate.Month)
                .Select(g => new MonthlyData { Month = g.Key, Earnings = g.Sum(r => r.PricePaid) })
                .OrderBy(x => x.Month)
                .ToListAsync();
        }

        private async Task<List<ServiceEarnings>> GetEarningsByServiceAsync(IQueryable<UserService> query)
        {
            return await query
                .GroupBy(r => r.Service.NameService)
                .Select(g => new ServiceEarnings { Service = g.Key, Earnings = g.Sum(r => r.PricePaid) })
                .OrderByDescending(x => x.Earnings)
                .ToListAsync();
        }

        #endregion
    }
} 