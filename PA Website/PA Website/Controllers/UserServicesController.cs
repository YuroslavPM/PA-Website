using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using PA_Website.Services;

namespace PA_Website.Controllers
{
    public class UserServicesController : Controller
    {
        private readonly IUserServiceService _userServiceService;
        private readonly IEmailService _emailService;
        private readonly IFileService _fileService;
        private readonly IUserProfileService _userProfileService;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserServicesController> _logger;
        private readonly IConfiguration _configuration;

        public UserServicesController(
            IUserServiceService userServiceService,
            IEmailService emailService,
            IFileService fileService,
            IUserProfileService userProfileService,
            UserManager<User> userManager,
            ApplicationDbContext context,
            ILogger<UserServicesController> logger,
            IConfiguration configuration)
        {
            _userServiceService = userServiceService;
            _emailService = emailService;
            _fileService = fileService;
            _userProfileService = userProfileService;
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        #region Public Actions

        // GET: UserServices
        public async Task<IActionResult> Index(string sortOrder, string categoryFilter, string statusFilter,
            DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 12)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var filter = new ReservationFilter
            {
                SortOrder = sortOrder,
                CategoryFilter = categoryFilter,
                StatusFilter = statusFilter,
                StartDate = startDate,
                EndDate = endDate,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var reservations = await _userServiceService.GetUserReservationsAsync(user.Id, filter);

            // Set view data for pagination and filters
            SetViewDataForIndex(filter, reservations.Count());

            return View(reservations);
        }

        // GET: UserServices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var reservation = await _userServiceService.GetReservationByIdAsync(id.Value);
            if (reservation == null)
                return NotFound();

            // Check if current user is owner or admin
            var currentUserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (reservation.User.Id != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            return View(reservation);
        }

        // GET: UserServices/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            // Get user's recent reservations
            var recentReservations = await _context.userServices
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Service)
                .OrderByDescending(r => r.ReservationDate)
                .Take(5)
                .ToListAsync();

            // Get available services for scheduling
            var availableServices = await _context.Service
                .Where(s => s.CategoryOfService.ToLower() == "психология")
                .ToListAsync();

            ViewData["User"] = user;
            ViewData["RecentReservations"] = recentReservations;
            ViewData["AvailableServices"] = availableServices;

            return View();
        }

        // GET: UserServices/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            return View(user);
        }

        // POST: UserServices/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind("Id,FName,LName,Email,PhoneNumber,Birth_Date")] User user)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            // Clear validation errors for fields we're not updating
            ModelState.Remove("Password");
            ModelState.Remove("Zodiacal_Sign");

            if (ModelState.IsValid)
            {
                var request = new UpdateProfileRequest
                {
                    FName = user.FName,
                    LName = user.LName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    BirthDate = user.Birth_Date
                };

                var success = await _userProfileService.UpdateProfileAsync(currentUser, request);

                if (success)
                {
                    TempData["SuccessMessage"] = "Профилът е обновен успешно!";
                    return RedirectToAction(nameof(Dashboard));
                }
                else
                {
                    TempData["ErrorMessage"] = "Грешка при обновяване на профила.";
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["ErrorMessage"] = "Грешка при валидация: " + string.Join(", ", errors);
            }

            return RedirectToAction(nameof(Profile));
        }

        // POST: UserServices/CreateReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(int serviceId, DateTime reservationDate,
            TimeSpan? reservationTime, DateTime? astrologicalDate, string? astrologicalPlaceOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            try
            {
                var request = new CreateReservationRequest
                {
                    UserId = user.Id,
                    ServiceId = serviceId,
                    ReservationDate = reservationDate,
                    ReservationTime = reservationTime,
                    AstrologicalDate = astrologicalDate,
                    AstrologicalPlaceOfBirth = astrologicalPlaceOfBirth
                };

                var reservation = await _userServiceService.CreateReservationAsync(request);
                var service = await _context.Service.FindAsync(serviceId);

                // Send email notifications
                await _emailService.SendReservationConfirmationAsync(user, reservation, service);
                await _emailService.SendAdminNotificationAsync(user, reservation, service, "created");

                TempData["SuccessMessage"] = "Резервацията е създадена успешно!";
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create reservation for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "Грешка при създаване на резервацията.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: UserServices/CancelReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            try
            {
                var success = await _userServiceService.CancelReservationAsync(id, user.Id);
                if (!success)
                {
                    TempData["ErrorMessage"] = "Не можете да отмените тази резервация.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var reservation = await _userServiceService.GetReservationByIdAsync(id);
                var service = await _context.Service.FindAsync(reservation.ServiceId);

                // Send email notifications
                await _emailService.SendReservationCancellationAsync(user, reservation, service);
                await _emailService.SendAdminNotificationAsync(user, reservation, service, "cancelled");

                TempData["SuccessMessage"] = "Резервацията е отменена успешно!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel reservation {ReservationId} for user {UserId}", id, user.Id);
                TempData["ErrorMessage"] = "Грешка при отменяване на резервацията.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: UserServices/RescheduleReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleReservation(int id, DateTime newDate, TimeSpan newTime, int serviceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            try
            {
                var request = new RescheduleRequest
                {
                    NewDate = newDate,
                    NewTime = newTime,
                    ServiceId = serviceId
                };

                // Use the optimized method that returns both old and new reservations
                var result = await _userServiceService.RescheduleReservationWithDetailsAsync(id, request, user.Id);
                if (!result.HasValue)
                {
                    TempData["ErrorMessage"] = "Избраният час вече е зает или не можете да пренасрочите тази резервация.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var (oldReservation, newReservation) = result.Value;
                var service = await _context.Service.FindAsync(serviceId);

                // Send email notifications with proper old and new reservations
                await _emailService.SendReservationRescheduleAsync(user, oldReservation, newReservation, service);
                await _emailService.SendAdminNotificationAsync(user, newReservation, service, "rescheduled");

                TempData["SuccessMessage"] = "Резервацията е пренасрочена успешно!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reschedule reservation {ReservationId} for user {UserId}", id, user.Id);
                TempData["ErrorMessage"] = "Грешка при пренасрочване на резервацията.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // GET: UserServices/GetAvailableTimes
        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(int serviceId, DateTime date)
        {
            try
            {
                var availableTimes = await _userServiceService.GetAvailableTimesAsync(serviceId, date);
                var timeStrings = availableTimes.Select(time => time.ToString(@"hh\:mm")).ToList();

                return Json(new { success = true, availableTimes = timeStrings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available times for service {ServiceId} on date {Date}", serviceId, date);
                return Json(new { success = false, message = "Грешка при зареждане на свободните часове." });
            }
        }

        public async Task<IActionResult> DownloadAstroCard(int id)
        {
            try
            {
                var reservation = await _userServiceService.GetReservationByIdAsync(id);
                if (reservation == null || string.IsNullOrEmpty(reservation.AstroCardFilePath))
                    return NotFound();

                var fileResult = await _fileService.DownloadAstroCardAsync(
                    reservation.AstroCardFilePath, 
                    reservation.AstroCardFileName ?? "astro-card",
                    reservation.AstroCardContentType ?? "application/octet-stream");

                if (fileResult == null)
                    return NotFound();

                return File(fileResult.Content, fileResult.ContentType, fileResult.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download astro card for reservation {ReservationId}", id);
                return NotFound();
            }
        }

        #endregion

        #region Admin Actions

        // GET: UserServices/IndexAdmin
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IndexAdmin(string sortOrder, string statusFilter, int pageNumber = 1, int pageSize = 12)
        {
            var filter = new ReservationFilter
            {
                SortOrder = sortOrder,
                StatusFilter = statusFilter,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var reservations = await _userServiceService.GetAllReservationsAsync(filter);

            // Set view data for pagination and filters
            SetViewDataForIndex(filter, reservations.Count());

            return View(reservations);
        }

        // GET: UserServices/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["ServiceId"] = new SelectList(_context.Service, "Id", "CategoryOfService");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName");
            return View();
        }

        // POST: UserServices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,UserId,ServiceId,AstrologicalDate,ReservationDate,ReservationTime")] UserService userService)
        {
            try
            {
                var request = new CreateReservationRequest
                {
                    UserId = userService.UserId,
                    ServiceId = userService.ServiceId,
                    ReservationDate = userService.ReservationDate,
                    ReservationTime = userService.ReservationTime,
                    AstrologicalDate = userService.AstrologicalDate
                };

                await _userServiceService.CreateReservationAsync(request);

                TempData["SuccessMessage"] = "Успешно резервирахте услуга!";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create reservation by admin");
                ModelState.AddModelError("", "Грешка при създаване на резервацията.");
            }

            ViewData["ServiceId"] = new SelectList(_context.Service, "Id", "CategoryOfService", userService.ServiceId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", userService.UserId);
            return View(userService);
        }

        // GET: UserServices/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var userService = await _userServiceService.GetReservationByIdAsync(id.Value);
            if (userService == null)
                return NotFound();

            ViewData["ServiceId"] = new SelectList(_context.Service, "Id", "CategoryOfService", userService.ServiceId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", userService.UserId);
            return View(userService);
        }

        // POST: UserServices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,ServiceId,AstrologicalDate,ReservationDate,ReservationTime")] UserService userService)
        {
            if (id != userService.Id)
                return NotFound();

            try
            {
                var request = new UpdateReservationRequest
                {
                    ServiceId = userService.ServiceId,
                    ReservationDate = userService.ReservationDate,
                    ReservationTime = userService.ReservationTime,
                    AstrologicalDate = userService.AstrologicalDate
                };

                await _userServiceService.UpdateReservationAsync(id, request);
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reservation {ReservationId}", id);
                ModelState.AddModelError("", "Грешка при обновяване на резервацията.");
            }

            ViewData["ServiceId"] = new SelectList(_context.Service, "Id", "CategoryOfService", userService.ServiceId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", userService.UserId);
            return View(userService);
        }

        // GET: UserServices/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var userService = await _userServiceService.GetReservationByIdAsync(id.Value);
            if (userService == null)
                return NotFound();

            return View(userService);
        }

        // POST: UserServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var success = await _userServiceService.DeleteReservationAsync(id);
            if (!success)
            {
                TempData["ErrorMessage"] = "Грешка при изтриване на резервацията.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserServices/UpdateStatus
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            try
            {
                var reservation = await _userServiceService.GetReservationByIdAsync(id);
                if (reservation == null)
                    return NotFound();

                var oldStatus = reservation.Status;
                var success = await _userServiceService.UpdateReservationStatusAsync(id, newStatus);

                if (success)
                {
                    var service = await _context.Service.FindAsync(reservation.ServiceId);
                    var user = await _userManager.FindByIdAsync(reservation.UserId);

                    // Send email notifications
                    await _emailService.SendStatusUpdateAsync(user, reservation, service, oldStatus, newStatus);

                    TempData["SuccessMessage"] = $"Статусът е променен на {newStatus}.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Невалиден статус.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update status for reservation {ReservationId}", id);
                TempData["ErrorMessage"] = "Грешка при обновяване на статуса.";
            }

            return RedirectToAction("Details", new { id });
        }

        // POST: UserServices/UploadAstroCard
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UploadAstroCard(int id, IFormFile astroCardFile)
        {
            try
            {
                if (!_fileService.IsValidAstroCardFile(astroCardFile))
                {
                    TempData["ErrorMessage"] = "Невалиден файл. Разрешени са само JPG, PNG, PDF и DOCX файлове до 6MB.";
                    return RedirectToAction("Details", new { id });
                }

                var reservation = await _userServiceService.GetReservationByIdAsync(id);
                if (reservation == null)
                    return NotFound();

                if (reservation.Service.CategoryOfService.ToLower() != "астрология")
                {
                    TempData["ErrorMessage"] = "Може да качвате файлове само за астрологични услуги.";
                    return RedirectToAction("Details", new { id });
                }

                // Upload file
                var filePath = await _fileService.UploadAstroCardAsync(astroCardFile);

                // Update reservation
                var success = await _userServiceService.UploadAstroCardAsync(id, astroCardFile);
                if (success)
                {
                    // Send completion email
                    var user = await _userManager.FindByIdAsync(reservation.UserId);
                    var service = await _context.Service.FindAsync(reservation.ServiceId);
                    await _emailService.SendAstroCardCompletionAsync(user, reservation, service);

                    TempData["SuccessMessage"] = "Астрологичната карта е качена успешно!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Грешка при качване на файла.";
                }
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload astro card for reservation {ReservationId}", id);
                TempData["ErrorMessage"] = "Грешка при качване на файла.";
            }

            return RedirectToAction("Details", new { id });
        }

        // POST: UserServices/DeleteAstroCard
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteAstroCard(int id)
        {
            try
            {
                var reservation = await _userServiceService.GetReservationByIdAsync(id);
                if (reservation == null)
                    return NotFound();

                // Delete file from storage
                if (!string.IsNullOrEmpty(reservation.AstroCardFilePath))
                {
                    await _fileService.DeleteAstroCardAsync(reservation.AstroCardFilePath);
                }

                // Update database
                var success = await _userServiceService.DeleteAstroCardAsync(id);
                if (success)
                {
                    TempData["SuccessMessage"] = "Астрологичната карта е изтрита успешно!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Грешка при изтриване на файла.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete astro card for reservation {ReservationId}", id);
                TempData["ErrorMessage"] = "Грешка при изтриване на файла.";
            }

            return RedirectToAction("Details", new { id });
        }

        // GET: UserServices/AdminDashboard
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var allReservations = await _context.userServices
                .Include(u => u.User)
                .Include(u => u.Service)
                .ToListAsync();

            return View(allReservations);
        }

        // GET: UserServices/SendEmail
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendEmail(int? reservationId = null)
        {
            var reservations = await _context.userServices
                .Include(r => r.User)
                .Include(r => r.Service)
                .Where(r => r.Status != "Cancelled")
                .OrderByDescending(r => r.ReservationDate)
                .ToListAsync();

            ViewBag.Reservations = reservations;
            ViewBag.SelectedReservationId = reservationId;

            return View();
        }

        // POST: UserServices/SendEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendEmail(int reservationId, string subject, string message, string emailType)
        {
            try
            {
                var reservation = await _userServiceService.GetReservationByIdAsync(reservationId);
                if (reservation == null)
                {
                    TempData["ErrorMessage"] = "Резервацията не е намерена.";
                    return RedirectToAction(nameof(SendEmail));
                }

                var service = await _context.Service.FindAsync(reservation.ServiceId);
                var user = await _userManager.FindByIdAsync(reservation.UserId);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Потребителят не е намерен.";
                    return RedirectToAction(nameof(SendEmail));
                }

                if (service == null)
                {
                    TempData["ErrorMessage"] = "Услугата не е намерена.";
                    return RedirectToAction(nameof(SendEmail));
                }

                // Set appropriate subject based on email type if not custom
                string emailSubject = subject;
                if (emailType != "custom")
                {
                    emailSubject = emailType?.ToLower() switch
                    {
                        "confirmation" => "Потвърждение на резервация - Душевна Мозайка",
                        "reminder" => "Напомняне за резервация - Душевна Мозайка",
                        _ => "Съобщение от Душевна Мозайка"
                    };
                }

                await _emailService.SendCustomEmailAsync(user, reservation, service, emailSubject, message, emailType);

                if (emailType == "confirmation")
                {
                    await _userServiceService.UpdateReservationStatusAsync(reservationId, "Confirmed");
                }

                TempData["SuccessMessage"] = "Имейлът е изпратен успешно!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for reservation {ReservationId}", reservationId);
                TempData["ErrorMessage"] = "Грешка при изпращане на имейла.";
            }

            return RedirectToAction(nameof(SendEmail));
        }

        // GET: UserServices/SendBulkEmail
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendBulkEmail()
        {
            var tinyMceApiKey = _configuration["TinyMCE:ApiKey"];
            ViewBag.TinyMceApiKey = tinyMceApiKey;

            var users = await _userManager.Users
                .Where(u => u.EmailSend)
                .OrderBy(u => u.FName)
                .ThenBy(u => u.LName)
                .Select(u => new
                {
                    Id = u.Id,
                    FName = u.FName,
                    LName = u.LName,
                    Email = u.Email
                })
                .ToListAsync();

            var newsletterSubscribers = await _context.NewsletterSubscribers
                .OrderBy(n => n.Email)
                .Select(n => new
                {
                    Id = "newsletter_" + n.Id.ToString(),
                    FName = "Newsletter",
                    LName = "Subscriber",
                    Email = n.Email
                })
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.NewsletterSubscribers = newsletterSubscribers;

            return View();
        }

        // POST: UserServices/SendBulkEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendBulkEmail(string[] selectedUserIds, string subject, string message, string emailType)
        {
            if (selectedUserIds == null || !selectedUserIds.Any())
            {
                TempData["ErrorMessage"] = "Моля, изберете поне един потребител.";
                return RedirectToAction(nameof(SendBulkEmail));
            }

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Моля, попълнете заглавие и съобщение.";
                return RedirectToAction(nameof(SendBulkEmail));
            }

            try
            {
                var recipients = await PrepareBulkEmailRecipientsAsync(selectedUserIds);
                await _emailService.SendBulkEmailAsync(recipients, subject, message, emailType);

                TempData["SuccessMessage"] = $"Имейлът е изпратен успешно на {recipients.Count} потребител(и).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send bulk email");
                TempData["ErrorMessage"] = "Грешка при изпращане на имейлите.";
            }

            return RedirectToAction(nameof(SendBulkEmail));
        }

        // GET: UserServices/GetFinancials
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetFinancials(int? year, int? month)
        {
            try
            {
                var report = await _userServiceService.GetFinancialReportAsync(year, month);
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get financial report");
                return Json(new { error = "Грешка при зареждане на финансовия отчет." });
            }
        }

        #endregion

        #region Private Methods

        private void SetViewDataForIndex(ReservationFilter filter, int totalCount)
        {
            ViewData["CurrentPage"] = filter.PageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalCount / filter.PageSize);
            ViewData["SortOrder"] = filter.SortOrder;
            ViewData["CategoryFilter"] = filter.CategoryFilter;
            ViewData["StatusFilter"] = filter.StatusFilter;
            ViewData["StartDate"] = filter.StartDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = filter.EndDate?.ToString("yyyy-MM-dd");
        }

        private async Task<List<EmailRecipient>> PrepareBulkEmailRecipientsAsync(string[] selectedUserIds)
        {
            var recipients = new List<EmailRecipient>();

            // Separate regular user IDs from newsletter subscriber IDs
            var regularUserIds = selectedUserIds.Where(id => !id.StartsWith("newsletter_")).ToList();
            var newsletterIds = selectedUserIds.Where(id => id.StartsWith("newsletter_"))
                .Select(id => int.Parse(id.Replace("newsletter_", "")))
                .ToList();

            // Get regular users
            if (regularUserIds.Any())
            {
                var users = await _userManager.Users
                    .Where(u => regularUserIds.Contains(u.Id))
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .ToListAsync();

                foreach (var user in users)
                {
                    recipients.Add(new EmailRecipient
                    {
                        FName = user.FName,
                        LName = user.LName,
                        Email = user.Email,
                        IsUser = true
                    });
                }
            }

            // Get newsletter subscribers
            if (newsletterIds.Any())
            {
                var newsletterSubscribers = await _context.NewsletterSubscribers
                    .Where(n => newsletterIds.Contains(n.Id))
                    .Where(n => !string.IsNullOrEmpty(n.Email))
                    .ToListAsync();

                foreach (var subscriber in newsletterSubscribers)
                {
                    recipients.Add(new EmailRecipient
                    {
                        FName = "Newsletter",
                        LName = "Subscriber",
                        Email = subscriber.Email,
                        IsUser = false
                    });
                }
            }

            return recipients;
        }

        #endregion
    }
}