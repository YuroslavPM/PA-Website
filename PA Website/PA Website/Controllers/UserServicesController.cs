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

namespace PA_Website.Controllers
{
    public class UserServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;


        public UserServicesController(ApplicationDbContext context, UserManager<User> userService)
        {
            _context = context;
            _userManager = userService;
        }

        // GET: UserServices
        public async Task<IActionResult> Index(string sortOrder, int pageNumber = 1, int pageSize = 12)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var reservations = _context.userServices
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Service)
                .AsQueryable();

            reservations = sortOrder switch
            {
                "date_desc" => reservations.OrderByDescending(r => r.ReservationDate),
                "date_asc" => reservations.OrderBy(r => r.ReservationDate),
                "category_asc" => reservations.OrderBy(r => r.Service.CategoryOfService),
                "category_desc" => reservations.OrderByDescending(r => r.Service.CategoryOfService),
                "status_asc" => reservations.OrderBy(r => r.ReservationDate < DateTime.Now),
                "status_desc" => reservations.OrderByDescending(r => r.ReservationDate < DateTime.Now),
                _ => reservations.OrderByDescending(r => r.ReservationDate)
            };

            int totalRecords = await reservations.CountAsync();
            var pagedReservations = await reservations
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewData["SortOrder"] = sortOrder;

            return View(pagedReservations);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IndexAdmin(string sortOrder, string statusFilter, int pageNumber = 1, int pageSize = 12)
        {
            var reservations = _context.userServices
                .Include(u => u.Service)
                .Include(u => u.User)
                .AsQueryable();
            if (!string.IsNullOrEmpty(statusFilter))
            {
                reservations = reservations.Where(r => r.Status == statusFilter);
            }

            // Сортиране според избора на потребителя
            reservations = sortOrder switch
            {
                "date_desc" => reservations.OrderByDescending(r => r.ReservationDate),
                "date_asc" => reservations.OrderBy(r => r.ReservationDate),
                "category_asc" => reservations.OrderBy(r => r.Service.CategoryOfService),
                "category_desc" => reservations.OrderByDescending(r => r.Service.CategoryOfService),
                "status_asc" => reservations.OrderBy(r => DateTime.Now > r.ReservationDate),
                "status_desc" => reservations.OrderByDescending(r => DateTime.Now > r.ReservationDate),
                _ => reservations.OrderByDescending(r => r.ReservationDate)
            };

            // Броим общия брой записи за пагинация
            int totalRecords = await reservations.CountAsync();
            var pagedReservations = await reservations.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewData["CurrentPage"] = pageNumber;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewData["SortOrder"] = sortOrder;

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
            var reservation = await _context.userServices.FindAsync(id);
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

            reservation.Status = newStatus;
            _context.Update(reservation);
            await _context.SaveChangesAsync();

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

            // Update reservation with file info
            reservation.AstroCardFileName = astroCardFile.FileName;
            reservation.AstroCardFilePath = $"/astro-cards/{uniqueFileName}";
            reservation.AstroCardFileSize = astroCardFile.Length;
            reservation.AstroCardContentType = astroCardFile.ContentType;
            reservation.AstroCardUploadDate = DateTime.Now;

            _context.Update(reservation);
            await _context.SaveChangesAsync();

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
