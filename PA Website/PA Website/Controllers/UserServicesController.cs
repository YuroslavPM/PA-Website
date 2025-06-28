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

namespace PA_Website.Controllers
{
    public class UserServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;

        public UserServicesController(ApplicationDbContext context, UserManager<User> userService, IEmailSender emailSender)
        {
            _context = context;
            _userManager = userService;
            _emailSender = emailSender;
        }

        // GET: UserServices
        public async Task<IActionResult> Index(string sortOrder, string categoryFilter, string statusFilter, int pageNumber = 1, int pageSize = 12)
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

            // Приложете сортиране
            reservations = sortOrder switch
            {
                "date_desc" => reservations.OrderByDescending(r => r.ReservationDate),
                "date_asc" => reservations.OrderBy(r => r.ReservationDate),
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
                _context.Add(userService);
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



    }
    }
