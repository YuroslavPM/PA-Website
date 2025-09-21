using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using PA_Website.Services;

namespace PA_Website.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<User> _userManager;
        private readonly IPromotionService _promotionService;
        private readonly IConfiguration _configuration;

        // Constants
        private const string AstrologyCategory = "астрология";
        private const string PendingStatus = "Pending";
        private const string CancelledStatus = "Cancelled";
        private const string FirstBookingPromotionType = "FirstBooking";
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

        public ServicesController(
            ApplicationDbContext context, 
            IEmailSender emailSender, 
            UserManager<User> userManager,
            IConfiguration configuration, 
            IPromotionService promotionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _promotionService = promotionService ?? throw new ArgumentNullException(nameof(promotionService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        #region Public Actions

        /// <summary>
        /// GET: Services - Display all services with promotion eligibility
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var services = await _context.Service.ToListAsync();
                var promotionData = await GetPromotionDataAsync();

                ViewBag.FirstBookingPromo = promotionData.FirstBookingPromo;
                ViewBag.IsEligibleForFirstBookingPromo = promotionData.IsEligible;
                ViewBag.DebugInfo = promotionData.DebugInfo;

                return View(services);
            }
            catch (Exception ex)
            {
                // Log exception here
                return View("Error");
            }
        }

        /// <summary>
        /// GET: Services/Details/5 - Display service details with reserved times
        /// </summary>
        public async Task<IActionResult> Details(int? id, string selectedDate)
        {
            if (id == null)
                return NotFound();

            try
            {
                var service = await GetServiceByIdAsync(id.Value);
                if (service == null)
                    return NotFound();

                var reservedTimes = await GetReservedTimesForServiceAsync(id.Value);
                
                ViewData["ReservedTimes"] = reservedTimes;
                ViewData["SelectedDate"] = selectedDate;

                return View(service);
            }
            catch (Exception ex)
            {
                // Log exception here
                return View("Error");
            }
        }

        /// <summary>
        /// GET: Services/Create - Display create form for admins
        /// </summary>
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewBag.TinyMceApiKey = _configuration["TinyMCE:ApiKey"];
            return View();
        }

        /// <summary>
        /// POST: Services/Create - Create new service
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [Bind("Id,NameService,CategoryOfService,ReservationDate,Description,Price")]
            Service service,
            IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
                return View(service);

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageResult = await ProcessImageUploadAsync(imageFile);
                    if (!imageResult.IsValid)
                    {
                        ModelState.AddModelError("imageFile", imageResult.ErrorMessage);
                        return View(service);
                    }
                    service.ImagePath = imageResult.ImagePath;
                }

                await _context.AddAsync(service);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Възникна грешка при създаването на услугата. Моля, опитайте отново.");
                return View(service);
            }
        }

        /// <summary>
        /// GET: Services/Edit/5 - Display edit form for admins
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            try
            {
                var service = await GetServiceByIdAsync(id.Value);
                if (service == null)
                    return NotFound();

                ViewBag.TinyMceApiKey = _configuration["TinyMCE:ApiKey"];
                return View(service);
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

        /// <summary>
        /// POST: Services/Edit/5 - Update existing service
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,NameService,CategoryOfService,ReservationDate,Description,Price,ImagePath")]
            Service service,
            IFormFile? imageFile)
        {
            if (id != service.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(service);

            try
            {
                var existingService = await GetServiceByIdAsync(id);
                if (existingService == null)
                    return NotFound();

                await UpdateServiceAsync(existingService, service, imageFile);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ServiceExistsAsync(service.Id))
                    return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Възникна грешка при обновяването на услугата.");
                return View(service);
            }
        }

        /// <summary>
        /// GET: Services/Delete/5 - Display delete confirmation for admins
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            try
            {
                var service = await GetServiceByIdAsync(id.Value);
                if (service == null)
                    return NotFound();

                return View(service);
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

        /// <summary>
        /// POST: Services/Delete/5 - Delete service
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var service = await GetServiceByIdAsync(id);
                if (service != null)
                {
                    _context.Service.Remove(service);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

        /// <summary>
        /// POST: Create reservation for a service
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateReservation(
            int ServiceId, 
            string reservationDate,
            string reservationTime, 
            string astrologicalDate, 
            string birthCity)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                {
                    SetErrorMessage("Трябва да влезете в профила си, за да направите резервация.");
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                var service = await GetServiceByIdAsync(ServiceId);
                if (service == null)
                    return NotFound();

                var validationResult = ValidateReservationInput(reservationDate, reservationTime, astrologicalDate);
                if (!validationResult.IsValid)
                {
                    SetErrorMessage(validationResult.ErrorMessage);
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                var reservationDateTime = ParseReservationDateTime(service, reservationDate, reservationTime, astrologicalDate);
                if (reservationDateTime == null)
                {
                    SetErrorMessage("Невалиден формат на датата или часа.");
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                var reservationValidation = await ValidateReservationAsync(user, service, reservationDateTime.Value);
                if (!reservationValidation.IsValid)
                {
                    SetErrorMessage(reservationValidation.ErrorMessage);
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                var reservation = await CreateUserServiceAsync(user, service, reservationDateTime.Value, reservationTime, birthCity);
                await SendReservationEmailAsync(user, service, reservationDateTime.Value, birthCity);

                return View("ReservationSuccess");
            }
            catch (Exception ex)
            {
                SetErrorMessage("Възникна грешка при създаването на резервацията.");
                return RedirectToAction("Details", new { id = ServiceId });
            }
        }

        /// <summary>
        /// GET: Get available times for a specific date and service
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(DateTime date, int serviceId)
        {
            try
            {
                var allowedTimes = GetAllowedTimesForDay(date.DayOfWeek);
                if (allowedTimes == null)
                    return Json(new { success = false, message = "Невалиден ден." });

                var reservedTimes = await GetReservedTimesForDateAsync(serviceId, date);
                var availableTimes = allowedTimes
                    .Where(time => !reservedTimes.Contains(time))
                    .Select(time => time.ToString(@"hh\:mm"))
                    .ToList();

                return Json(new { success = true, availableTimes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Възникна грешка при зареждането на часовете." });
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<Service?> GetServiceByIdAsync(int id)
        {
            return await _context.Service.FindAsync(id);
        }

        private async Task<bool> ServiceExistsAsync(int id)
        {
            return await _context.Service.AnyAsync(e => e.Id == id);
        }

        private async Task<List<string>> GetReservedTimesForServiceAsync(int serviceId)
        {
            return await _context.userServices
                .Where(r => r.ServiceId == serviceId)
                .Select(r => r.ReservationTime)
                .Where(rt => rt.HasValue)
                .Select(rt => rt!.Value.ToString(@"hh\:mm"))
                .ToListAsync();
        }

        private async Task<List<TimeSpan>> GetReservedTimesForDateAsync(int serviceId, DateTime date)
        {
            return await _context.userServices
                .Where(r => r.ServiceId == serviceId &&
                           r.ReservationTime.HasValue &&
                           r.ReservationDate.Date == date.Date &&
                           r.Status != CancelledStatus)
                .Select(r => r.ReservationTime!.Value)
                .ToListAsync();
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            if (User.Identity?.Name == null)
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
        }

        private async Task<PromotionData> GetPromotionDataAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
                return new PromotionData();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return new PromotionData();

            var hasReservations = await _context.userServices.AnyAsync(us => us.UserId == user.Id);
            var firstBookingPromo = await GetActiveFirstBookingPromotionAsync();
            var isEligible = !hasReservations && firstBookingPromo != null;

            return new PromotionData
            {
                FirstBookingPromo = firstBookingPromo,
                IsEligible = isEligible,
                DebugInfo = new
                {
                    UserId = user.Id,
                    HasReservations = hasReservations,
                    FoundPromotion = firstBookingPromo != null,
                    PromotionId = firstBookingPromo?.Id,
                    PromotionType = firstBookingPromo?.PromotionType,
                    IsActive = firstBookingPromo?.IsActive,
                    StartDate = firstBookingPromo?.StartDate,
                    EndDate = firstBookingPromo?.EndDate,
                    IsEligible = isEligible
                }
            };
        }

        private async Task<Promotion?> GetActiveFirstBookingPromotionAsync()
        {
            return await _context.Promotions
                .Where(p => p.IsActive && 
                           p.PromotionType == FirstBookingPromotionType && 
                           p.StartDate <= DateTime.Now &&
                           p.EndDate >= DateTime.Now)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private List<TimeSpan>? GetAllowedTimesForDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday
                    => Enumerable.Range(9, 7).Select(hour => new TimeSpan(hour, 0, 0)).ToList(),
                DayOfWeek.Saturday
                    => Enumerable.Range(9, 3).Select(hour => new TimeSpan(hour, 0, 0))
                        .Concat(new []
                        {
                            new TimeSpan(12, 30, 0),
                            new TimeSpan(13, 30, 0),
                            new TimeSpan(14, 30, 0)
                        }).ToList(),
                _ => null
            };
        }

        private async Task<ImageUploadResult> ProcessImageUploadAsync(IFormFile imageFile)
        {
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(fileExtension))
            {
                return new ImageUploadResult
                {
                    IsValid = false,
                    ErrorMessage = "Само JPG, JPEG, PNG и GIF файлове са разрешени."
                };
            }

            if (imageFile.Length > MaxFileSizeBytes)
            {
                return new ImageUploadResult
                {
                    IsValid = false,
                    ErrorMessage = "Файлът не може да бъде по-голям от 5MB."
                };
            }

            try
            {
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", "services");
                
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                var filePath = Path.Combine(directoryPath, fileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: true);
                await imageFile.CopyToAsync(fileStream);

                return new ImageUploadResult
                {
                    IsValid = true,
                    ImagePath = $"/Images/services/{fileName}"
                };
            }
            catch (Exception)
            {
                return new ImageUploadResult
                {
                    IsValid = false,
                    ErrorMessage = "Грешка при качването на изображението."
                };
            }
        }

        private async Task UpdateServiceAsync(Service existingService, Service updatedService, IFormFile? imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var imageResult = await ProcessImageUploadAsync(imageFile);
                if (!imageResult.IsValid)
                    throw new InvalidOperationException(imageResult.ErrorMessage);

                // Delete old image
                if (!string.IsNullOrEmpty(existingService.ImagePath))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                        existingService.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                        System.IO.File.Delete(oldImagePath);
                }

                existingService.ImagePath = imageResult.ImagePath;
            }

            // Update properties
            existingService.NameService = updatedService.NameService;
            existingService.CategoryOfService = updatedService.CategoryOfService;
            existingService.ReservationDate = updatedService.ReservationDate;
            existingService.Description = updatedService.Description;
            existingService.Price = updatedService.Price;

            await _context.SaveChangesAsync();
        }

        private ValidationResult ValidateReservationInput(string reservationDate, string reservationTime, string astrologicalDate)
        {
            if ((string.IsNullOrEmpty(reservationDate) || string.IsNullOrEmpty(reservationTime)) &&
                string.IsNullOrEmpty(astrologicalDate))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Трябва да изберете дата и час за консултация."
                };
            }

            return new ValidationResult { IsValid = true };
        }

        private DateTime? ParseReservationDateTime(Service service, string reservationDate, string reservationTime, string astrologicalDate)
        {
            try
            {
                if (service.CategoryOfService.ToLower() == AstrologyCategory)
                {
                    return DateTime.ParseExact(astrologicalDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
                }
                else
                {
                    return DateTime.ParseExact($"{reservationDate} {reservationTime}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<ValidationResult> ValidateReservationAsync(User user, Service service, DateTime reservationDateTime)
        {
            if (service.CategoryOfService.ToLower() == AstrologyCategory)
                return new ValidationResult { IsValid = true };

            var lastActiveReservation = await _context.userServices
                .Where(u => u.UserId == user.Id && u.Status != CancelledStatus)
                .OrderByDescending(u => u.ReservationDate)
                .FirstOrDefaultAsync();

            if (lastActiveReservation != null)
            {
                if (reservationDateTime <= lastActiveReservation.ReservationDate)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Не можете да резервирате за дата преди последната ви резервация."
                    };
                }

                if (IsSameWeek(lastActiveReservation.ReservationDate, reservationDateTime))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Не можете да резервирате две консултации в една и съща седмица. Моля, изберете дата за следващата седмица."
                    };
                }
            }

            return new ValidationResult { IsValid = true };
        }

        private bool IsSameWeek(DateTime date1, DateTime date2)
        {
            var currentCulture = CultureInfo.CurrentCulture;
            var week1 = currentCulture.Calendar.GetWeekOfYear(date1, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
            var week2 = currentCulture.Calendar.GetWeekOfYear(date2, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
            
            return date1.Year == date2.Year && week1 == week2;
        }

        private async Task<UserService> CreateUserServiceAsync(User user, Service service, DateTime reservationDateTime, string reservationTime, string birthCity)
        {
            var (pricePaid, usedPromotions) = await _promotionService.CalculatePricePaidWithTracking(user.Id, service);

            UserService reservation;
            if (service.CategoryOfService.ToLower() == AstrologyCategory)
            {
                reservation = new UserService
                {
                    UserId = user.Id,
                    ServiceId = service.Id,
                    AstrologicalDate = reservationDateTime,
                    ReservationTime = null,
                    AstrologicalPlaceOfBirth = birthCity,
                    Status = PendingStatus,
                    PricePaid = pricePaid
                };
            }
            else
            {
                reservation = new UserService
                {
                    UserId = user.Id,
                    ServiceId = service.Id,
                    ReservationDate = reservationDateTime,
                    ReservationTime = TimeSpan.Parse(reservationTime),
                    AstrologicalPlaceOfBirth = "",
                    Status = PendingStatus,
                    PricePaid = pricePaid
                };
            }

            _context.userServices.Add(reservation);
            await _context.SaveChangesAsync();

            // Track used promotions
            foreach (var promotion in usedPromotions)
            {
                var userPromotion = new UserPromotion
                {
                    UserId = user.Id,
                    PromotionId = promotion.Id,
                    UsedAt = DateTime.Now,
                    UserServiceId = reservation.Id
                };
                _context.UserPromotions.Add(userPromotion);
                promotion.UsedCount++;
            }

            await _context.SaveChangesAsync();
            return reservation;
        }

        private async Task SendReservationEmailAsync(User user, Service service, DateTime reservationDateTime, string birthCity)
        {
            if (string.IsNullOrEmpty(user.Email))
                return;

            var subject = "Потвърждение на резервация";
            var iban = "BG89CECB979010G3001000";
            
            var dateInfo = service.CategoryOfService.ToLower() == AstrologyCategory
                ? $"<li>Дата за астрологичен анализ: {reservationDateTime:dd.MM.yyyy HH:mm}</li>"
                : $"<li>Дата на консултация: {reservationDateTime:dd.MM.yyyy HH:mm}</li>";
            
            var birthCityInfo = service.CategoryOfService.ToLower() == AstrologyCategory && !string.IsNullOrEmpty(birthCity)
                ? $"<li>Място на раждане: {birthCity}</li>"
                : string.Empty;

            var htmlMessage = $@"
<h3>Уважаеми/а {user.FName},</h3>
<p>Благодарим Ви, че избрахте нашите услуги!</p>
<p>Вашата заявка за <b>{service.NameService}</b> е получена успешно.</p>
<div style='background-color: #fff3cd; padding: 15px; border-left: 5px solid #ffeeba; margin: 15px 0;'>
    <h4 style='color: #856404; margin-top: 0;'>Статус на резервацията: <strong>ИЗЧАКВАНЕ</strong></h4>
    <p>Вашата резервация е в процес на обработка. За да бъде активирана, е необходимо да извършите плащане.</p>
</div>
<p><strong>Данни за резервация:</strong></p>
<ul>
    <li>Услуга: {service.NameService}</li>
    {dateInfo}
    {birthCityInfo}
</ul>
<p><strong>Инструкции за плащане:</strong></p>
<ol>
    <li>Сума за плащане: {service.Price} лв.</li>
    <li>Банкова сметка (IBAN): <strong>{iban}</strong></li>
    <li>Титуляр на сметката: Мариела Разпопова</li>
    <li>В основание на плащането моля посочете: Резервация {service.NameService}</li>
</ol>
<p>След получаване на плащането, ще получите потвърждение за активиране на резервацията.</p>
<p>Ако имате въпроси, не се колебайте да се свържете с мен.</p>
<p>С уважение,<br>Мариела Разпопова</p>";

            await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
        }

        private void SetErrorMessage(string message)
        {
            TempData["ErrorMessage"] = message;
        }

        #endregion

        #region Helper Classes

        private class ImageUploadResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public string ImagePath { get; set; } = string.Empty;
        }

        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        private class PromotionData
        {
            public Promotion? FirstBookingPromo { get; set; }
            public bool IsEligible { get; set; }
            public object? DebugInfo { get; set; }
        }

        #endregion
    }
}