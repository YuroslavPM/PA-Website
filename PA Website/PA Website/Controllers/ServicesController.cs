using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;

namespace PA_Website.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Services
        public async Task<IActionResult> Index()
        {
            return View(await _context.Service.ToListAsync());
        }

        // GET: Services/Details/5
        public async Task<IActionResult> Details(int? id, string selectedDate)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Service
                .FirstOrDefaultAsync(m => m.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            var reservedTimes = await _context.userServices
            .Where(r => r.ServiceId == id)
            .Select(r => r.ReservationTime)
            .Where(rt => rt.HasValue)
            .Select(rt => rt.Value.ToString(@"hh\:mm"))
            .ToListAsync();

            ViewData["ReservedTimes"] = reservedTimes;
            ViewData["SelectedDate"] = selectedDate;


            return View(service);
        }



        // GET: Services/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Services/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,NameService,CategoryOfService,ReservationDate,Description")] Service service)
        {
            if (ModelState.IsValid)
            {
                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(service);
        }

        // GET: Services/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Service.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }
            return View(service);
        }


        [HttpPost]
        public async Task<IActionResult> CreateReservation(int ServiceId, string reservationDate, string reservationTime, string astrologicalDate)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Трябва да влезете в профила си, за да направите резервация.";
                return RedirectToAction("Details", new { id = ServiceId });
            }

            var service = await _context.Service.FindAsync(ServiceId);
            if (service == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(reservationDate) || string.IsNullOrEmpty(reservationTime) && string.IsNullOrEmpty(astrologicalDate))
            {
                TempData["ErrorMessage"] = "Трябва да изберете дата и час за консултация.";
                return RedirectToAction("Details", new { id = ServiceId });
            }

            DateTime reservationDateTime;
            try
            {
                if (service.CategoryOfService.ToLower() == "астрология")
                {
                    reservationDateTime = DateTime.ParseExact(astrologicalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    reservationDateTime = DateTime.ParseExact($"{reservationDate} {reservationTime}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "Невалиден формат на датата или часа.";
                return RedirectToAction("Details", new { id = ServiceId });
            }

            // Проверка дали услугата е астрология или психология
            if (service.CategoryOfService.ToLower() == "астрология")
            {
                var astroReservation = new UserService
                {
                    UserId = user.Id,
                    ServiceId = ServiceId,
                    AstrologicalDate = reservationDateTime,
                    ReservationTime = null
                };
                _context.userServices.Add(astroReservation);
            }
            else
            {
                if (reservationDateTime.DayOfWeek == DayOfWeek.Saturday || reservationDateTime.DayOfWeek == DayOfWeek.Sunday)
                {
                    TempData["ErrorMessage"] = "Не може да резервирате в събота и неделя.";
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                TimeSpan selectedTime = TimeSpan.Parse(reservationTime);

                bool existingReservation = await _context.userServices
                    .AnyAsync(r => r.ServiceId == ServiceId && r.ReservationDate.Date == reservationDateTime.Date && r.ReservationTime == selectedTime);

                if (existingReservation)
                {
                    TempData["ErrorMessage"] = "Този час вече е зает. Моля, изберете друг.";
                    return RedirectToAction("Details", new { id = ServiceId });
                }

                var reservation = new UserService
                {
                    UserId = user.Id,
                    ServiceId = ServiceId,
                    ReservationDate = reservationDateTime,
                    ReservationTime = selectedTime
                };

                _context.userServices.Add(reservation);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Резервацията е успешна!";
            return RedirectToAction("Details", new { id = ServiceId });
        }


        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(DateTime date, int serviceId)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return Json(new { success = false, message = "Изберете делничен ден." });
            }

            var reservedTimes = await _context.userServices
                .Where(r => r.ServiceId == serviceId && r.ReservationTime.HasValue && r.ReservationDate.Date == date.Date)
                .Select(r => r.ReservationTime.Value) // Взимаме стойността на TimeSpan?
                .ToListAsync();

            var availableTimes = new List<string>();
            for (int hour = 9; hour <= 17; hour++)
            {
                var time = new TimeSpan(hour, 0, 0);
                if (!reservedTimes.Contains(time))
                {
                    availableTimes.Add($"{hour:00}:00");
                }
            }

            return Json(new { success = true, availableTimes });
        }



        // POST: Services/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,NameService,CategoryOfService,ReservationDate,Description")] Service service)
        {
            if (id != service.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(service.Id))
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
            return View(service);
        }

        // GET: Services/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Service
                .FirstOrDefaultAsync(m => m.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Service.FindAsync(id);
            if (service != null)
            {
                _context.Service.Remove(service);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ServiceExists(int id)
        {
            return _context.Service.Any(e => e.Id == id);
        }
    }
}