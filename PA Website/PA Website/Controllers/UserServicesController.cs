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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<UserServicesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IPromotionService _promotionService;

        public UserServicesController(ApplicationDbContext context, UserManager<User> userService, IEmailSender emailSender, ILogger<UserServicesController> logger, IConfiguration configuration, IPromotionService promotionService)
        {
            _context = context;
            _userManager = userService;
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
            _promotionService = promotionService;
        }

        // GET: UserServices
        public async Task<IActionResult> Index(string sortOrder, string categoryFilter, string statusFilter, DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 12)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // Първо заредете всички резервации без филтри за статус
            var reservations = _context.userServices
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Service)
                .AsQueryable();

            // Приложете филтър за категория
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                reservations = reservations.Where(r => r.Service.CategoryOfService.ToLower() == categoryFilter.ToLower());
            }

            // Приложете филтър за дата
            if (startDate.HasValue)
            {
                reservations = reservations.Where(r => r.ReservationDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                reservations = reservations.Where(r => r.ReservationDate <= endDate.Value);
            }

            // Приложете сортиране
            reservations = sortOrder switch
            {
                "date_desc" => reservations.OrderByDescending(r => r.ReservationDate),
                "date_asc" => reservations.OrderBy(r => r.ReservationDate),
                "service" => reservations.OrderBy(r => r.Service.NameService),
                "status" => reservations.OrderBy(r => r.Status),
                _ => reservations.OrderByDescending(r => r.ReservationDate)
            };

            // Заредете всички записи и обработете изтеклите статуси
            var result = await reservations.ToListAsync();

            // Маркирайте изтеклите резервации
            foreach (var reservation in result)
            {
                if (reservation.Service.CategoryOfService.ToLower() == "психология" &&
                    reservation.Status != "Completed" &&
                    reservation.Status != "Cancelled" &&
                    reservation.ReservationDate.Add(reservation.ReservationTime ?? TimeSpan.Zero).AddHours(1) < DateTime.Now)
                {
                    reservation.Status = "Expired";
                }
            }

            // Сега приложете филтъра за статус след като всички статуси са актуални
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Expired")
                {
                    result = result.Where(r => r.Status == "Expired").ToList();
                }
                else
                {
                    result = result.Where(r => r.Status == statusFilter).ToList();
                }
            }

            int totalRecords = result.Count;
            var pagedReservations = result
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewData["SortOrder"] = sortOrder;
            ViewData["CategoryFilter"] = categoryFilter;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");

            return View(pagedReservations);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IndexAdmin(string sortOrder, string statusFilter, int pageNumber = 1, int pageSize = 12)
        {
            // Първо заредете всички резервации с необходимите включвания
            var query = _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .AsQueryable();

            // Приложете сортиране
            query = sortOrder switch
            {
                "date_desc" => query.OrderByDescending(r => r.ReservationDate),
                "date_asc" => query.OrderBy(r => r.ReservationDate),
                _ => query.OrderByDescending(r => r.ReservationDate)
            };

            // Заредете всички записи в паметта
            var allReservations = await query.ToListAsync();

            // Маркирайте изтеклите резервации
            foreach (var reservation in allReservations)
            {
                if (reservation.Service.CategoryOfService.ToLower() == "психология" &&
                    reservation.Status != "Completed" &&
                    reservation.Status != "Cancelled" &&
                    reservation.ReservationDate.Add(reservation.ReservationTime ?? TimeSpan.Zero).AddHours(1) < DateTime.Now)
                {
                    reservation.Status = "Expired";
                }
            }

            // Сега приложете филтъра за статус
            IEnumerable<UserService> filteredReservations = allReservations;
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Expired")
                {
                    filteredReservations = filteredReservations.Where(r => r.Status == "Expired");
                }
                else
                {
                    filteredReservations = filteredReservations.Where(r => r.Status == statusFilter);
                }
            }

            // Пагинация
            int totalRecords = filteredReservations.Count();
            var pagedReservations = filteredReservations
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewData["SortOrder"] = sortOrder;
            ViewData["StatusFilter"] = statusFilter;

            return View(pagedReservations);
        }


        public async Task<IActionResult> SortReservations(string sortOrder)
        {
            var reservations = _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .AsQueryable();

            switch (sortOrder)
            {
                case "category_asc":
                    reservations = reservations.OrderBy(r => r.Service.CategoryOfService);
                    break;
                case "category_desc":
                    reservations = reservations.OrderByDescending(r => r.Service.CategoryOfService);
                    break;
                case "date_asc":
                    reservations = reservations.OrderBy(r => r.ReservationDate);
                    break;
                case "date_desc":
                    reservations = reservations.OrderByDescending(r => r.ReservationDate);
                    break;
                case "status_asc":
                    reservations = reservations.OrderBy(r => r.ReservationDate < DateTime.Now);
                    break;
                case "status_desc":
                    reservations = reservations.OrderByDescending(r => r.ReservationDate < DateTime.Now);
                    break;
                default:
                    reservations = reservations.OrderBy(r => r.ReservationDate);
                    break;
            }

            return PartialView("ReservedTable", await reservations.ToListAsync());
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
            var service = await _context.Service.FindAsync(userService.ServiceId);
            if (service == null)
            {
                ModelState.AddModelError("", "Избраната услуга не съществува.");
                return View(userService);
            }

            if (service.CategoryOfService.ToLower() == "астрология")
            {
                if (userService.AstrologicalDate == DateTime.MinValue)
                {
                    ModelState.AddModelError("AstrologicalDate", "Моля, въведете валидна дата за астрологичната услуга.");
                    return View(userService);
                }
                userService.ReservationDate = DateTime.MinValue;
                userService.ReservationTime = null;
            }
            else
            {
                if (userService.ReservationDate < DateTime.Now)
                {
                    ModelState.AddModelError("ReservationDate", "Моля, изберете бъдеща дата.");
                    return View(userService);
                }
                if (userService.ReservationTime == null)
                {
                    ModelState.AddModelError("ReservationTime", "Моля, изберете час за среща.");
                    return View(userService);
                }
                userService.AstrologicalDate = DateTime.MinValue;
            }

            if (ModelState.IsValid)
            {
                // Calculate price paid (with promotion logic) and track used promotions
                var (pricePaid, usedPromotions) = await _promotionService.CalculatePricePaidWithTracking(userService.UserId, service);
                userService.PricePaid = pricePaid;
                _context.Add(userService);
                await _context.SaveChangesAsync();

                // Record promotion usage
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
                    
                    // Update promotion usage count
                    promotion.UsedCount++;
                }
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Успешно резервирахте услуга!";
                return RedirectToAction(nameof(Index));
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
            {
                return NotFound();
            }

            var userService = await _context.userServices.FindAsync(id);
            if (userService == null)
            {
                return NotFound();
            }

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
            {
                return NotFound();
            }

            var service = await _context.Service.FindAsync(userService.ServiceId);
            if (service == null)
            {
                ModelState.AddModelError("", "Избраната услуга не съществува.");
                return View(userService);
            }

            if (service.CategoryOfService.ToLower() == "астрология")
            {
                userService.ReservationDate = DateTime.MinValue;
                userService.ReservationTime = null;
            }
            else
            {
                userService.AstrologicalDate = DateTime.MinValue;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(userService);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserServiceExists(userService.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["ServiceId"] = new SelectList(_context.Service, "Id", "CategoryOfService", userService.ServiceId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", userService.UserId);
            return View(userService);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userService = await _context.userServices
                .Include(u => u.User)
                .Include(u => u.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (userService == null)
            {
                return NotFound();
            }

            // Check if current user is owner or admin
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userService.User.Id != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(userService);
        }



        // GET: UserServices/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userService = await _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (userService == null)
            {
                return NotFound();
            }

            return View(userService);
        }

        // POST: UserServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userService = await _context.userServices.FindAsync(id);
            if (userService != null)
            {
                _context.userServices.Remove(userService);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserServiceExists(int id)
        {
            return _context.userServices.Any(e => e.Id == id);
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var reservation = await _context.userServices
                .Include(r => r.User)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (reservation == null)
            {
                return NotFound();
            }

            // Validate status
            var validStatuses = new[] { "Pending", "Confirmed", "Completed", "Cancelled" };
            if (!validStatuses.Contains(newStatus))
            {
                TempData["ErrorMessage"] = "Невалиден статус.";
                return RedirectToAction("Details", new { id });
            }

            var oldStatus = reservation.Status;
            reservation.Status = newStatus;
            // Only set PricePaid if it's zero (legacy data)
            if (newStatus == "Completed" && reservation.PricePaid == 0)
            {
                reservation.PricePaid = await _promotionService.CalculatePricePaid(reservation.UserId, reservation.Service);
            }
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            // Send confirmation email when status is changed to "Confirmed"
            if (newStatus == "Confirmed" && oldStatus != "Confirmed" && !string.IsNullOrEmpty(reservation.User?.Email))
            {
                var subject = "Потвърждение на плащането - Резервацията е активирана";
                
                string dateInfo = reservation.Service.CategoryOfService.ToLower() == "астрология"
                    ? $"<li>Дата за астрологичен анализ: {reservation.AstrologicalDate:dd.MM.yyyy HH:mm}</li>"
                    : $"<li>Дата на консултация: {reservation.ReservationDate:dd.MM.yyyy HH:mm}</li>";
                    
                string birthCityInfo = reservation.Service.CategoryOfService.ToLower() == "астрология" && !string.IsNullOrEmpty(reservation.AstrologicalPlaceOfBirth)
                    ? $"<li>Място на раждане: {reservation.AstrologicalPlaceOfBirth}</li>"
                    : string.Empty;

                var htmlMessage = $@"
<h3>Уважаеми/а {reservation.User.FName},</h3>
<p>Благодарим Ви за плащането!</p>
<div style='background-color: #d1fae5; padding: 15px; border-left: 5px solid #10b981; margin: 15px 0;'>
    <h4 style='color: #065f46; margin-top: 0;'>Статус на резервацията: <strong>ПОТВЪРДЕНА</strong></h4>
    <p>Вашата резервация е успешно активирана и потвърдена.</p>
</div>
<p><strong>Данни за резервация:</strong></p>
<ul>
    <li>Услуга: {reservation.Service.NameService}</li>
    <li>Категория: {reservation.Service.CategoryOfService}</li>
    {dateInfo}
    {birthCityInfo}
    <li>Цена: {reservation.Service.Price} лв.</li>
</ul>
<p><strong>Следващи стъпки:</strong></p>
<ul>
    <li>За психологически консултации: Ще се свържем с Вас за потвърждение на часа</li>
    <li>За астрологични услуги: Ще получите вашата астрологична карта в срок</li>
</ul>
<p>Ако имате въпроси, не се колебайте да се свържете с нас.</p>
<p>С уважение,<br>Екипът на Душевна Мозайка</p>
";
                await _emailSender.SendEmailAsync(reservation.User.Email, subject, htmlMessage);
            }

            // Send completion email when status is changed to "Completed" for astrology services
            if (newStatus == "Completed" && oldStatus != "Completed" && 
                reservation.Service.CategoryOfService.ToLower() == "астрология" && 
                !string.IsNullOrEmpty(reservation.User?.Email))
            {
                var subject = "Вашата астрологична услуга е завършена";
                
                // Generate the URL for the user service details
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var userServiceUrl = $"{baseUrl}/UserServices/Details/{reservation.Id}";

                var htmlMessage = $@"
<h3>Уважаеми/а {reservation.User.FName},</h3>
<p>Радваме се да Ви информираме, че вашата астрологична услуга е успешно завършена!</p>
<div style='background-color: #dbeafe; padding: 15px; border-left: 5px solid #3b82f6; margin: 15px 0;'>
    <h4 style='color: #1e40af; margin-top: 0;'>Статус на услугата: <strong>ЗАВЪРШЕНА</strong></h4>
    <p>Вашата астрологична карта е готова и качена в системата.</p>
</div>
<p><strong>Детайли за услугата:</strong></p>
<ul>
    <li>Услуга: {reservation.Service.NameService}</li>
    <li>Категория: {reservation.Service.CategoryOfService}</li>
    <li>Дата за астрологичен анализ: {reservation.AstrologicalDate:dd.MM.yyyy HH:mm}</li>
    <li>Място на раждане: {reservation.AstrologicalPlaceOfBirth}</li>
    <li>Цена: {reservation.Service.Price} лв.</li>
</ul>
<p><strong>Какво следва:</strong></p>
<ul>
    <li>Вашата астрологична карта е качена и достъпна</li>
    <li>Можете да я изтеглите от вашия профил</li>
    <li>Ще получите подробен анализ на вашата астрологична карта</li>
</ul>
<div style='text-align: center; margin: 30px 0;'>
    <a href='{userServiceUrl}' style='background-color: #7c3aed; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
        <i class='fas fa-download' style='margin-right: 8px;'></i>
        Преглед на резервацията
    </a>
</div>
<p style='font-size: 14px; color: #6b7280;'>
    <strong>Забележка:</strong> Бутонът по-горе ще Ви отведе директно към страницата с детайлите на вашата резервация, 
    където можете да изтеглите астрологичната карта.
</p>
<p>Благодарим Ви, че избрахте нашите услуги!</p>
<p>С уважение,<br>Екипът на Душевна Мозайка</p>
";
                await _emailSender.SendEmailAsync(reservation.User.Email, subject, htmlMessage);
            }

            TempData["SuccessMessage"] = $"Статусът е променен на {newStatus}.";
            return RedirectToAction("Details", new { id });
        }


        //Logic for files
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UploadAstroCard(int id, IFormFile astroCardFile)
        {
            var reservation = await _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (reservation == null)
            {
                return NotFound();
            }

            // Check if the service is astrology
            if (reservation.Service.CategoryOfService.ToLower() != "астрология")
            {
                TempData["ErrorMessage"] = "Може да качвате файлове само за астрологични услуги.";
                return RedirectToAction("Details", new { id });
            }

            // Validate file
            if (astroCardFile == null || astroCardFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Моля, изберете файл.";
                return RedirectToAction("Details", new { id });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".docx" };
            var fileExtension = Path.GetExtension(astroCardFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "Невалиден файлов формат. Разрешени са само JPG, PNG и PDF.";
                return RedirectToAction("Details", new { id });
            }

            if (astroCardFile.Length > 6 * 1024 * 1024) // 6MB limit
            {
                TempData["ErrorMessage"] = "Файлът трябва да бъде по-малък от 6MB.";
                return RedirectToAction("Details", new { id });
            }

            // Create unique filename
            var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "astro-cards");
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Ensure directory exists
            Directory.CreateDirectory(uploadsFolder);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await astroCardFile.CopyToAsync(fileStream);
            }

            // Store old status to check if we need to send email
            var oldStatus = reservation.Status;

            // Update reservation with file info
            reservation.AstroCardFileName = astroCardFile.FileName;
            reservation.AstroCardFilePath = $"/astro-cards/{uniqueFileName}";
            reservation.AstroCardFileSize = astroCardFile.Length;
            reservation.AstroCardContentType = astroCardFile.ContentType;
            reservation.AstroCardUploadDate = DateTime.Now;
            reservation.Status = "Completed";

            _context.Update(reservation);
            await _context.SaveChangesAsync();

            // Send completion email when status is changed to "Completed" for astrology services
            if (reservation.Status == "Completed" && oldStatus != "Completed" && 
                reservation.Service.CategoryOfService.ToLower() == "астрология" && 
                !string.IsNullOrEmpty(reservation.User?.Email))
            {
                var subject = "Вашата астрологична услуга е завършена";
                
                // Generate the URL for the user service details
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var userServiceUrl = $"{baseUrl}/UserServices/Details/{reservation.Id}";

                var htmlMessage = $@"
<h3>Уважаеми/а {reservation.User.FName},</h3>
<p>Радваме се да Ви информираме, че вашата астрологична услуга е успешно завършена!</p>
<div style='background-color: #dbeafe; padding: 15px; border-left: 5px solid #3b82f6; margin: 15px 0;'>
    <h4 style='color: #1e40af; margin-top: 0;'>Статус на услугата: <strong>ЗАВЪРШЕНА</strong></h4>
    <p>Вашата астрологична карта е готова и качена в системата.</p>
</div>
<p><strong>Детайли за услугата:</strong></p>
<ul>
    <li>Услуга: {reservation.Service.NameService}</li>
    <li>Категория: {reservation.Service.CategoryOfService}</li>
    <li>Дата за астрологичен анализ: {reservation.AstrologicalDate:dd.MM.yyyy HH:mm}</li>
    <li>Място на раждане: {reservation.AstrologicalPlaceOfBirth}</li>
    <li>Цена: {reservation.Service.Price} лв.</li>
</ul>
<p><strong>Какво следва:</strong></p>
<ul>
    <li>Вашата астрологична карта е качена и достъпна</li>
    <li>Можете да я изтеглите от вашия профил</li>
    <li>Ще получите подробен анализ на вашата астрологична карта</li>
</ul>
<div style='text-align: center; margin: 30px 0;'>
    <a href='{userServiceUrl}' style='background-color: #7c3aed; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
        <i class='fas fa-download' style='margin-right: 8px;'></i>
        Преглед на резервацията
    </a>
</div>
<p style='font-size: 14px; color: #6b7280;'>
    <strong>Забележка:</strong> Бутонът по-горе ще Ви отведе директно към страницата с детайлите на вашата резервация, 
    където можете да изтеглите астрологичната карта.
</p>
<p>Благодарим Ви, че избрахте нашите услуги!</p>
<p>С уважение,<br>Екипът на Душевна Мозайка</p>
";
                await _emailSender.SendEmailAsync(reservation.User.Email, subject, htmlMessage);
            }

            TempData["SuccessMessage"] = "Астрологичната карта е качена успешно!";
            return RedirectToAction("Details", new { id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteAstroCard(int id)
        {
            var reservation = await _context.userServices.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(reservation.AstroCardFilePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", reservation.AstroCardFilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                reservation.AstroCardFileName = null;
                reservation.AstroCardFilePath = null;
                reservation.AstroCardFileSize = null;
                reservation.AstroCardContentType = null;
                reservation.AstroCardUploadDate = null;

                _context.Update(reservation);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Астрологичната карта е изтрита успешно!";
            }

            return RedirectToAction("Details", new { id });
        }

        public async Task<IActionResult> DownloadAstroCard(int id)
        {
            var reservation = await _context.userServices.FindAsync(id);
            if (reservation == null || string.IsNullOrEmpty(reservation.AstroCardFilePath))
            {
                return NotFound();
            }

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", reservation.AstroCardFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(path, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, reservation.AstroCardContentType, reservation.AstroCardFileName);
        }

        // GET: UserServices/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // Get user's reservations
            var reservations = await _context.userServices
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
            ViewData["RecentReservations"] = reservations;
            ViewData["AvailableServices"] = availableServices;

            return View();
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

        // GET: UserServices/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            return View(user);
        }

        // POST: UserServices/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind("Id,FName,LName,Email,PhoneNumber,Birth_Date")] User user)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Clear validation errors for fields we're not updating
            ModelState.Remove("Password");
            ModelState.Remove("Zodiacal_Sign");

            if (ModelState.IsValid)
            {
                try
                {
                    currentUser.FName = user.FName;
                    currentUser.LName = user.LName;
                    currentUser.Email = user.Email;
                    currentUser.PhoneNumber = user.PhoneNumber;
                    currentUser.Birth_Date = user.Birth_Date;
                    
                    // Automatically recalculate zodiac sign based on birth date
                    currentUser.Zodiacal_Sign = CalculateZodiacSign(user.Birth_Date);

                    var result = await _userManager.UpdateAsync(currentUser);
                    
                    if (result.Succeeded)
                    {
                        TempData["SuccessMessage"] = "Профилът е обновен успешно!";
                        return RedirectToAction(nameof(Dashboard));
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Грешка при обновяване на профила: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Грешка при обновяване на профила: " + ex.Message;
                }
            }
            else
            {
                // Log validation errors for debugging
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["ErrorMessage"] = "Грешка при валидация: " + string.Join(", ", errors);
            }

            return RedirectToAction(nameof(Profile));
        }

        // Utility method to calculate zodiac sign based on birth date
        private string CalculateZodiacSign(DateTime birthDate)
        {
            string zodiac;
            switch (birthDate.Month)
            {
                case 1:
                    zodiac = birthDate.Day <= 19 ? "Козирог" : "Водолей";
                    break;
                case 2:
                    zodiac = birthDate.Day <= 18 ? "Водолей" : "Риби";
                    break;
                case 3:
                    zodiac = birthDate.Day <= 20 ? "Риби" : "Овен";
                    break;
                case 4:
                    zodiac = birthDate.Day <= 19 ? "Овен" : "Телец";
                    break;
                case 5:
                    zodiac = birthDate.Day <= 20 ? "Телец" : "Близнаци";
                    break;
                case 6:
                    zodiac = birthDate.Day <= 20 ? "Близнаци" : "Рак";
                    break;
                case 7:
                    zodiac = birthDate.Day <= 22 ? "Рак" : "Лъв";
                    break;
                case 8:
                    zodiac = birthDate.Day <= 22 ? "Лъв" : "Дева";
                    break;
                case 9:
                    zodiac = birthDate.Day <= 22 ? "Дева" : "Везни";
                    break;
                case 10:
                    zodiac = birthDate.Day <= 22 ? "Везни" : "Скорпион";
                    break;
                case 11:
                    zodiac = birthDate.Day <= 21 ? "Скорпион" : "Стрелец";
                    break;
                case 12:
                    zodiac = birthDate.Day <= 21 ? "Стрелец" : "Козирог";
                    break;
                default:
                    zodiac = "Неизвестна зодия";
                    break;
            }
            return zodiac;
        }

        // POST: UserServices/CancelReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var reservation = await _context.userServices
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status == "Completed" || reservation.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Не можете да отмените тази резервация.";
                return RedirectToAction(nameof(Dashboard));
            }

            reservation.Status = "Cancelled";
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            // Send email notifications
            try
            {
                // Send email to user
                var userEmailHtml = CancelReservationEmailTemplate(user, reservation, reservation.Service);
                await _emailSender.SendEmailAsync(user.Email, "Резервация отменена - Душевна Мозайка", userEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send user cancellation email");
            }
            try
            {
                // Send email to admin
                var adminEmailHtml = AdminNotificationEmailTemplate(user, reservation, reservation.Service, "cancelled");
                var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? "dushevna_mozaika@abv.bg";
                await _emailSender.SendEmailAsync(adminEmail, "Отменена резервация - Известие", adminEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin cancellation email");
            }

            TempData["SuccessMessage"] = "Резервацията е отменена успешно!";
            return RedirectToAction(nameof(Dashboard));
        }

        // POST: UserServices/RescheduleReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleReservation(int id, DateTime newDate, TimeSpan newTime, int serviceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var reservation = await _context.userServices
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status == "Completed" || reservation.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Не можете да пренасрочите тази резервация.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Check if new date/time is available
            var conflictingReservation = await _context.userServices
                .Where(r => r.ServiceId == serviceId && 
                            r.ReservationDate == newDate.Date && 
                            r.ReservationTime == newTime &&
                            r.Status != "Cancelled" &&
                            r.Id != id)
                .FirstOrDefaultAsync();

            if (conflictingReservation != null)
            {
                TempData["ErrorMessage"] = "Избраният час вече е зает. Моля, изберете друг час.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Cancel the old reservation
            reservation.Status = "Cancelled";
            _context.Update(reservation);

            // Create new reservation
            var newReservation = new UserService
            {
                UserId = user.Id,
                ServiceId = serviceId,
                ReservationDate = newDate.Date,
                ReservationTime = newTime,
                Status = "Pending",
                AstrologicalDate = reservation.AstrologicalDate,
                AstrologicalPlaceOfBirth = reservation.AstrologicalPlaceOfBirth,
                AstroCardFileName = reservation.AstroCardFileName,
                AstroCardFilePath = reservation.AstroCardFilePath,
                AstroCardFileSize = reservation.AstroCardFileSize,
                AstroCardContentType = reservation.AstroCardContentType,
                AstroCardUploadDate = reservation.AstroCardUploadDate
            };

            _context.userServices.Add(newReservation);
            await _context.SaveChangesAsync();

            // Send email notifications
            try
            {
                // Send email to user
                var userEmailHtml = RescheduleReservationEmailTemplate(user, reservation, newReservation, reservation.Service);
                await _emailSender.SendEmailAsync(user.Email, "Резервация пренасрочена - Душевна Мозайка", userEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send user reschedule email");
            }
            try
            {
                // Send email to admin
                var adminEmailHtml = AdminNotificationEmailTemplate(user, newReservation, reservation.Service, "rescheduled");
                var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? "dushevna_mozaika@abv.bg";
                await _emailSender.SendEmailAsync(adminEmail, "Пренасрочена резервация - Известие", adminEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin reschedule email");
            }

            TempData["SuccessMessage"] = "Резервацията е пренасрочена успешно!";
            return RedirectToAction(nameof(Dashboard));
        }

        // Email template methods
        private string CreateReservationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Нова резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е създадена успешно. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Чакаща</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Ще получите потвърждение от администратор в най-кратки срокове.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string RescheduleReservationEmailTemplate(User user, UserService oldReservation, UserService newReservation, Service service)
        {
            var oldDateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? oldReservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : oldReservation.ReservationTime.HasValue 
                    ? $"{oldReservation.ReservationDate:dd.MM.yyyy} в {oldReservation.ReservationTime.Value:hh\\:mm}"
                    : $"{oldReservation.ReservationDate:dd.MM.yyyy}";

            var newDateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? newReservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : newReservation.ReservationTime.HasValue 
                    ? $"{newReservation.ReservationDate:dd.MM.yyyy} в {newReservation.ReservationTime.Value:hh\\:mm}"
                    : $"{newReservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Резервация пренасрочена</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е пренасрочена успешно. Ето промените:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на промяната</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Стара дата:</strong> <span style='color: #ef4444;'>{oldDateTime}</span></p>
                            <p style='margin: 5px 0;'><strong>Нова дата:</strong> <span style='color: #10b981;'>{newDateTime}</span></p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Чакаща</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Ще получите потвърждение от администратор в най-кратки срокове.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CancelReservationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #ef4444, #dc2626); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Резервация отменена</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е отменена успешно. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на отменената резервация</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #ef4444; font-weight: bold;'>Отменена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Ако имате въпроси или искате да направите нова резервация, не се колебайте да се свържете с нас.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Нова резервация
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string AdminNotificationEmailTemplate(User user, UserService reservation, Service service, string action)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            var actionText = action switch
            {
                "created" => "нова резервация",
                "rescheduled" => "пренасрочена резервация",
                "cancelled" => "отменена резервация",
                _ => "промяна в резервация"
            };

            var color = action switch
            {
                "created" => "#10b981",
                "rescheduled" => "#f59e0b",
                "cancelled" => "#ef4444",
                _ => "#6b7280"
            };

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Административно известие</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Известие за администратор</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Има {actionText} от потребител. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Потребител:</strong> {user.FName} {user.LName}</p>
                            <p style='margin: 5px 0;'><strong>Имейл:</strong> {user.Email}</p>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: {color}; font-weight: bold;'>{reservation.Status}</span></p>
                            <p style='margin: 5px 0;'><strong>ID на резервация:</strong> {reservation.Id}</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("IndexAdmin", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        // POST: UserServices/CreateReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(int serviceId, DateTime reservationDate, TimeSpan? reservationTime, DateTime? astrologicalDate, string? astrologicalPlaceOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var service = await _context.Service.FindAsync(serviceId);
            if (service == null)
            {
                TempData["ErrorMessage"] = "Избраната услуга не съществува.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Validate based on service category
            if (service.CategoryOfService.ToLower() == "астрология")
            {
                if (!astrologicalDate.HasValue || astrologicalDate.Value == DateTime.MinValue)
                {
                    TempData["ErrorMessage"] = "Моля, въведете валидна дата за астрологичната услуга.";
                    return RedirectToAction(nameof(Dashboard));
                }
            }
            else
            {
                if (reservationDate < DateTime.Now)
                {
                    TempData["ErrorMessage"] = "Моля, изберете бъдеща дата.";
                    return RedirectToAction(nameof(Dashboard));
                }
                if (!reservationTime.HasValue)
                {
                    TempData["ErrorMessage"] = "Моля, изберете час за среща.";
                    return RedirectToAction(nameof(Dashboard));
                }

                // Check for conflicts
                var conflictingReservation = await _context.userServices
                    .Where(r => r.ServiceId == serviceId && 
                                r.ReservationDate == reservationDate.Date && 
                                r.ReservationTime == reservationTime &&
                                r.Status != "Cancelled")
                    .FirstOrDefaultAsync();

                if (conflictingReservation != null)
                {
                    TempData["ErrorMessage"] = "Избраният час вече е зает. Моля, изберете друг час.";
                    return RedirectToAction(nameof(Dashboard));
                }
            }

            // Calculate price paid (with promotion logic) and track used promotions
            var (pricePaid, usedPromotions) = await _promotionService.CalculatePricePaidWithTracking(user.Id, service);

            // Create new reservation
            var newReservation = new UserService
            {
                UserId = user.Id,
                ServiceId = serviceId,
                ReservationDate = service.CategoryOfService.ToLower() == "астрология" ? DateTime.MinValue : reservationDate.Date,
                ReservationTime = service.CategoryOfService.ToLower() == "астрология" ? null : reservationTime,
                AstrologicalDate = service.CategoryOfService.ToLower() == "астрология" ? astrologicalDate : DateTime.MinValue,
                AstrologicalPlaceOfBirth = astrologicalPlaceOfBirth,
                Status = "Pending",
                PricePaid = pricePaid
            };

            _context.userServices.Add(newReservation);
            await _context.SaveChangesAsync();

            // Record promotion usage
            foreach (var promotion in usedPromotions)
            {
                var userPromotion = new UserPromotion
                {
                    UserId = user.Id,
                    PromotionId = promotion.Id,
                    UsedAt = DateTime.Now,
                    UserServiceId = newReservation.Id
                };
                _context.UserPromotions.Add(userPromotion);
                
                // Update promotion usage count
                promotion.UsedCount++;
            }
            await _context.SaveChangesAsync();

            // Send email notifications
            try
            {
                // Send email to user
                var userEmailHtml = CreateReservationEmailTemplate(user, newReservation, service);
                await _emailSender.SendEmailAsync(user.Email, "Нова резервация - Душевна Мозайка", userEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send user creation email");
            }
            try
            {
                // Send email to admin
                var adminEmailHtml = AdminNotificationEmailTemplate(user, newReservation, service, "created");
                var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? "dushevna_mozaika@abv.bg";
                await _emailSender.SendEmailAsync(adminEmail, "Нова резервация - Известие", adminEmailHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin creation email");
            }

            TempData["SuccessMessage"] = "Резервацията е създадена успешно!";
            return RedirectToAction(nameof(Dashboard));
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
            var reservation = await _context.userServices
                .Include(r => r.User)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Резервацията не е намерена.";
                return RedirectToAction(nameof(SendEmail));
            }

            try
            {
                string emailHtml = "";
                string emailSubject = "";

                switch (emailType)
                {
                    case "confirmation":
                        emailHtml = CreateConfirmationEmailTemplate(reservation.User, reservation, reservation.Service);
                        emailSubject = "Потвърждение на резервация - Душевна Мозайка";
                        break;
                    case "reminder":
                        emailHtml = CreateReminderEmailTemplate(reservation.User, reservation, reservation.Service);
                        emailSubject = "Напомняне за резервация - Душевна Мозайка";
                        break;
                    case "custom":
                        emailHtml = CreateCustomEmailTemplate(reservation.User, reservation, reservation.Service, message);
                        emailSubject = subject;
                        break;
                    default:
                        TempData["ErrorMessage"] = "Невалиден тип имейл.";
                        return RedirectToAction(nameof(SendEmail));
                }

                await _emailSender.SendEmailAsync(reservation.User.Email, emailSubject, emailHtml);
                TempData["SuccessMessage"] = "Имейлът е изпратен успешно!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin email to user");
                TempData["ErrorMessage"] = "Грешка при изпращане на имейла.";
            }

            return RedirectToAction(nameof(SendEmail));
        }

        // Email template for confirmation
        private string CreateConfirmationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #10b981, #059669); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Потвърждение на резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е потвърдена от администратор. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #10b981; font-weight: bold;'>Потвърдена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Очакваме ви в посочения час. Ако имате въпроси, не се колебайте да се свържете с нас.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        // Email template for reminder
        private string CreateReminderEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #f59e0b, #d97706); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Напомняне за резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Напомняме ви за предстоящата резервация. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} лв.</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Потвърдена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Моля, пристигнете навреме за вашата среща. Ако имате въпроси, не се колебайте да се свържете с нас.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        // Email template for custom message
        private string CreateCustomEmailTemplate(User user, UserService reservation, Service service, string customMessage)
        {
            var dateTime = service.CategoryOfService.ToLower() == "астрология" 
                ? reservation.AstrologicalDate?.ToString("dd.MM.yyyy") 
                : reservation.ReservationTime.HasValue 
                    ? $"{reservation.ReservationDate:dd.MM.yyyy} в {reservation.ReservationTime.Value:hh\\:mm}"
                    : $"{reservation.ReservationDate:dd.MM.yyyy}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Съобщение от администратор</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Вашата резервация</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                        </div>
                        
                        <div style='background: #fef3c7; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                            <h3 style='color: #92400e; margin: 0 0 15px 0;'>Съобщение от администратор</h3>
                            <p style='color: #374151; line-height: 1.6; margin: 0;'>{customMessage}</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{Url.Action("Dashboard", "UserServices")}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        // GET: UserServices/GetAvailableTimes
        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(int serviceId, DateTime date)
        {
            var service = await _context.Service.FindAsync(serviceId);
            if (service == null)
            {
                return Json(new { success = false, message = "Услугата не е намерена." });
            }

            // Determine allowed times based on the day
            List<TimeSpan> allowedTimes = new List<TimeSpan>();

            switch (date.DayOfWeek)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday:
                case DayOfWeek.Friday:
                    allowedTimes.Add(new TimeSpan(18, 30, 0));
                    allowedTimes.Add(new TimeSpan(19, 30, 0));
                    break;
                case DayOfWeek.Saturday:
                    for (int hour = 9; hour <= 16; hour++)
                    {
                        allowedTimes.Add(new TimeSpan(hour, 0, 0));
                    }
                    break;
                case DayOfWeek.Sunday:
                    for (int hour = 9; hour <= 13; hour++)
                    {
                        allowedTimes.Add(new TimeSpan(hour, 0, 0));
                    }
                    break;
                default:
                    return Json(new { success = false, message = "Невалиден ден." });
            }

            // Get already reserved times
            var reservedTimes = await _context.userServices
                .Where(r => r.ServiceId == serviceId && 
                            r.ReservationTime.HasValue && 
                            r.ReservationDate.Date == date.Date &&
                            r.Status != "Cancelled")
                .Select(r => r.ReservationTime!.Value)
                .ToListAsync();

            // Filter only available times
            var availableTimes = allowedTimes
                .Where(time => !reservedTimes.Contains(time))
                .Select(time => time.ToString(@"hh\:mm"))
                .ToList();

            return Json(new { success = true, availableTimes });
        }

        // GET: UserServices/SendBulkEmail
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendBulkEmail()
        {
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

            // Pass both collections separately
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
            _logger.LogInformation($"Bulk email request received. Selected users: {selectedUserIds?.Length ?? 0}, Subject: {subject}, EmailType: {emailType}");

            if (selectedUserIds == null || !selectedUserIds.Any())
            {
                _logger.LogWarning("No users selected for bulk email");
                TempData["ErrorMessage"] = "Моля, изберете поне един потребител.";
                return RedirectToAction(nameof(SendBulkEmail));
            }

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(message))
            {
                _logger.LogWarning("Missing subject or message for bulk email");
                TempData["ErrorMessage"] = "Моля, попълнете заглавие и съобщение.";
                return RedirectToAction(nameof(SendBulkEmail));
            }

            // Separate regular user IDs from newsletter subscriber IDs
            var regularUserIds = selectedUserIds.Where(id => !id.StartsWith("newsletter_")).ToList();
            var newsletterIds = selectedUserIds.Where(id => id.StartsWith("newsletter_"))
                .Select(id => int.Parse(id.Replace("newsletter_", "")))
                .ToList();

            _logger.LogInformation($"Regular users: {regularUserIds.Count}, Newsletter subscribers: {newsletterIds.Count}");

            var recipients = new List<dynamic>();

            // Get regular users
            if (regularUserIds.Any())
            {
                var users = await _userManager.Users
                    .Where(u => regularUserIds.Contains(u.Id))
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .ToListAsync();

                foreach (var user in users)
                {
                    recipients.Add(new { 
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
                    recipients.Add(new { 
                        FName = "Newsletter", 
                        LName = "Subscriber", 
                        Email = subscriber.Email,
                        IsUser = false
                    });
                }
            }

            _logger.LogInformation($"Found {recipients.Count} recipients to send emails to");

            if (!recipients.Any())
            {
                _logger.LogWarning("No recipients with valid email addresses found");
                TempData["ErrorMessage"] = "Не са намерени потребители с валидни имейл адреси.";
                return RedirectToAction(nameof(SendBulkEmail));
            }

            int successCount = 0;
            int failureCount = 0;
            var failedEmails = new List<string>();

            foreach (var recipient in recipients)
            {
                try
                {
                    _logger.LogInformation($"Attempting to send email to {recipient.Email}");
            
                    // Validate email
                    if (string.IsNullOrEmpty(recipient.Email))
                    {
                        _logger.LogWarning($"Recipient {recipient.FName} {recipient.LName} has no email address");
                        failureCount++;
                        failedEmails.Add($"{recipient.FName} {recipient.LName} (no email)");
                        continue;
                    }

                    string emailHtml;
                    if (recipient.IsUser)
                    {
                        // Create a user-like object for the template
                        var userObj = new { FName = recipient.FName, LName = recipient.LName, Email = recipient.Email };
                        emailHtml = CreateBulkEmailTemplateForUser(userObj, message, emailType);
                    }
                    else
                    {
                        // For newsletter subscribers, use a simpler template
                        emailHtml = CreateBulkEmailTemplateForSubscriber(recipient.Email, message, emailType);
                    }
            
                    await _emailSender.SendEmailAsync(recipient.Email, subject, emailHtml);
                    successCount++;
                    _logger.LogInformation($"Successfully sent email to {recipient.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send bulk email to recipient {recipient.Email}");
                    failureCount++;
                    failedEmails.Add($"{recipient.FName} {recipient.LName} ({recipient.Email})");
                }
            }

            _logger.LogInformation($"Bulk email completed. Success: {successCount}, Failures: {failureCount}");

            if (successCount > 0)
            {
                TempData["SuccessMessage"] = $"Имейлът е изпратен успешно на {successCount} потребител(и).";
                if (failureCount > 0)
                {
                    TempData["WarningMessage"] = $"Грешка при изпращане на {failureCount} имейл(а): {string.Join(", ", failedEmails.Take(5))}";
                    if (failedEmails.Count > 5)
                    {
                        TempData["WarningMessage"] += $" и още {failedEmails.Count - 5}...";
                    }
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Грешка при изпращане на всички имейли.";
            }

            return RedirectToAction(nameof(SendBulkEmail));
        }

        // Updated email template method for users
        private string CreateBulkEmailTemplateForUser(dynamic user, string message, string emailType)
        {
            var headerColor = emailType switch
            {
                "announcement" => "linear-gradient(135deg, #4c1d95, #7c3aed)",
                "newsletter" => "linear-gradient(135deg, #10b981, #059669)",
                "promotion" => "linear-gradient(135deg, #f59e0b, #d97706)",
                _ => "linear-gradient(135deg, #4c1d95, #7c3aed)"
            };

            var headerText = emailType switch
            {
                "announcement" => "Важно съобщение",
                "newsletter" => "Новини и обновления",
                "promotion" => "Специална оферта",
                _ => "Съобщение от Душевна Мозайка"
            };

            // Generate the base URL for the website
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var servicesUrl = $"{baseUrl}/Services";

            return $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
            <div style='background: {headerColor}; color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>{headerText}</p>
            </div>
            
            <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                
                <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                    <div style='color: #374151; line-height: 1.6; white-space: pre-wrap;'>{message}</div>
                </div>
                
                <div style='text-align: center; margin-top: 30px;'>
                    <a href='{servicesUrl}' 
                       style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                        Разгледай услуги
                    </a>
                </div>
            </div>
            
            <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                <p>Този имейл е изпратен от администратор.</p>
                <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
            </div>
        </div>";
        }

// New email template method for newsletter subscribers
        private string CreateBulkEmailTemplateForSubscriber(string email, string message, string emailType)
        {
            var headerColor = emailType switch
            {
                "announcement" => "linear-gradient(135deg, #4c1d95, #7c3aed)",
                "newsletter" => "linear-gradient(135deg, #10b981, #059669)",
                "promotion" => "linear-gradient(135deg, #f59e0b, #d97706)",
                _ => "linear-gradient(135deg, #4c1d95, #7c3aed)"
            };

            var headerText = emailType switch
            {
                "announcement" => "Важно съобщение",
                "newsletter" => "Новини и обновления",
                "promotion" => "Специална оферта",
                _ => "Съобщение от Душевна Мозайка"
            };

            // Generate the base URL for the website
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var servicesUrl = $"{baseUrl}/Services";

            return $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
            <div style='background: {headerColor}; color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>{headerText}</p>
            </div>
            
            <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте!</h2>
                
                <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                    <div style='color: #374151; line-height: 1.6; white-space: pre-wrap;'>{message}</div>
                </div>
                
                <div style='text-align: center; margin-top: 30px;'>
                    <a href='{servicesUrl}' 
                       style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                        Разгледай услуги
                    </a>
                </div>
            </div>
            
            <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                <p>Този имейл е изпратен от администратор.</p>
                <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
            </div>
        </div>";
        }

        // GET: UserServices/GetFinancials
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult GetFinancials(int? year, int? month)
        {
            var completedStatuses = new[] { "Completed" };
            var reservations = _context.userServices
                .Include(u => u.Service)
                .Where(r => completedStatuses.Contains(r.Status));

            if (year.HasValue)
            {
                reservations = reservations.Where(r => r.ReservationDate.Year == year.Value);
            }
            if (month.HasValue)
            {
                reservations = reservations.Where(r => r.ReservationDate.Month == month.Value);
            }

            // Total earnings for the filter
            var totalEarnings = reservations.Sum(r => r.PricePaid);

            // Calculate previous period
            decimal previousEarnings = 0;
            if (year.HasValue && month.HasValue)
            {
                // Previous month (handle January)
                int prevMonth = month.Value == 1 ? 12 : month.Value - 1;
                int prevYear = month.Value == 1 ? year.Value - 1 : year.Value;
                previousEarnings = _context.userServices
                    .Where(r => completedStatuses.Contains(r.Status)
                                && r.ReservationDate.Year == prevYear
                                && r.ReservationDate.Month == prevMonth)
                    .Sum(r => r.PricePaid);
            }
            else if (year.HasValue && !month.HasValue)
            {
                // Previous year
                int prevYear = year.Value - 1;
                previousEarnings = _context.userServices
                    .Where(r => completedStatuses.Contains(r.Status)
                                && r.ReservationDate.Year == prevYear)
                    .Sum(r => r.PricePaid);
            }
            // else: no filter, or only month (shouldn't happen)

            // Calculate percent change
            decimal percentChange = 0;
            if (previousEarnings > 0)
            {
                percentChange = ((totalEarnings - previousEarnings) / previousEarnings) * 100;
            }
            else if (totalEarnings > 0)
            {
                percentChange = 100; // 100% increase from 0
            }
            // else: both zero, percentChange stays 0

            // Earnings per month for the selected year
            var earningsByMonth = _context.userServices
                .Include(u => u.Service)
                .Where(r => completedStatuses.Contains(r.Status));
            if (year.HasValue)
            {
                earningsByMonth = earningsByMonth.Where(r => r.ReservationDate.Year == year.Value);
            }
            var monthlyData = earningsByMonth
                .GroupBy(r => r.ReservationDate.Month)
                .Select(g => new { Month = g.Key, Earnings = g.Sum(r => r.PricePaid) })
                .OrderBy(x => x.Month)
                .ToList();

            // Earnings by service (for pie chart, optional)
            var earningsByService = reservations
                .GroupBy(r => r.Service.NameService)
                .Select(g => new { Service = g.Key, Earnings = g.Sum(r => r.PricePaid) })
                .OrderByDescending(x => x.Earnings)
                .ToList();

            return Json(new
            {
                totalEarnings,
                previousEarnings,
                percentChange,
                monthlyData,
                earningsByService
            });
        }



    }
}