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
using Microsoft.AspNetCore.Identity.UI.Services;

namespace PA_Website.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public ServicesController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
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
            .Select(rt => rt!.Value.ToString(@"hh\:mm"))
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
        public async Task<IActionResult> Create([Bind("Id,NameService,CategoryOfService,ReservationDate,Description,Price")] Service service)
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
        public async Task<IActionResult> CreateReservation(int ServiceId, string reservationDate, string reservationTime, string astrologicalDate, string birthCity)
        {
            if (User.Identity?.Name == null)
            {
                TempData["ErrorMessage"] = "Трябва да влезете в профила си, за да направите резервация.";
                return RedirectToAction("Details", new { id = ServiceId });
            }

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

            if ((string.IsNullOrEmpty(reservationDate) || string.IsNullOrEmpty(reservationTime)) && string.IsNullOrEmpty(astrologicalDate))
            {
                TempData["ErrorMessage"] = "Трябва да изберете дата и час за консултация.";
                return RedirectToAction("Details", new { id = ServiceId });
            }

            DateTime reservationDateTime;
            try
            {
                if (service.CategoryOfService.ToLower() == "астрология")
                {
                    reservationDateTime = DateTime.ParseExact(astrologicalDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
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
                    ReservationTime = null,
                    AstrologicalPlaceOfBirth = birthCity,
                    Status = "Pending"
                };
                _context.userServices.Add(astroReservation);
            }
            else
            {
                var lastReservationUser = await _context.userServices.
                    Where(u => u.UserId == user.Id).OrderByDescending(u=> u.ReservationDate)
                    .FirstOrDefaultAsync();
                //The client cant reserve more than one consultation in a week
                if (lastReservationUser != null)
                {
                    DateTime lastReservationDate = lastReservationUser.ReservationDate;

                    if (reservationDateTime <= lastReservationDate)
                    {
                        TempData["ErrorMessage"] = "Не можете да резервирате за дата преди последната ви резервация.";
                        return RedirectToAction("Details", new { id = ServiceId });
                    }

                    var currentCulture = CultureInfo.CurrentCulture;
                    var lastWeekReservation = currentCulture.Calendar.GetWeekOfYear(lastReservationDate, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                    var newReservationWeek = currentCulture.Calendar.GetWeekOfYear(reservationDateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);

                    if (lastReservationDate.Year == reservationDateTime.Year && lastWeekReservation == newReservationWeek)
                    {
                        TempData["ErrorMessage"] = "Не можете да резервирате две консултации в една и съща седмица. Моля, изберете дата за следващата седмица.";
                        return RedirectToAction("Details", new { id = ServiceId });
                    }

                }

                TimeSpan selectedTime = TimeSpan.Parse(reservationTime);

                var reservation = new UserService
                {
                    UserId = user.Id,
                    ServiceId = ServiceId,
                    ReservationDate = reservationDateTime,
                    ReservationTime = selectedTime,
                    AstrologicalPlaceOfBirth = "", 
                    Status = "Pending"
                };

                _context.userServices.Add(reservation);
            }

            await _context.SaveChangesAsync();

            // Send confirmation email
            if (!string.IsNullOrEmpty(user.Email))
            {
                var subject = "Потвърждение на резервация";
                var iban = "BG00XXXX00000000000000"; // Replace with your real IBAN
                string dateInfo = service.CategoryOfService.ToLower() == "астрология"
                    ? $"<li>Дата за астрологичен анализ: {reservationDateTime:dd.MM.yyyy HH:mm}</li>"
                    : $"<li>Дата на консултация: {reservationDateTime:dd.MM.yyyy HH:mm}</li>";
                string birthCityInfo = service.CategoryOfService.ToLower() == "астрология" && !string.IsNullOrEmpty(birthCity)
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
    <li>Титуляр на сметката: PA Website</li>
    <li>В основание на плащането моля посочете: Резервация {service.NameService}</li>
</ol>
<p>След получаване на плащането, ще получите потвърждение за активиране на резервацията.</p>
<p>Ако имате въпроси, не се колебайте да се свържете с нас.</p>
<p>С уважение,<br>Екипът на PA Website</p>
";
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
            }

            // Show pop-up and redirect to user panel
            return View("ReservationSuccess");
        }


        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(DateTime date, int serviceId)
        {
            // Определяме допустимите часове според деня
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

            // Взимаме вече резервираните часове
            var reservedTimes = await _context.userServices
                .Where(r => r.ServiceId == serviceId && r.ReservationTime.HasValue && r.ReservationDate.Date == date.Date)
                .Select(r => r.ReservationTime!.Value)
                .ToListAsync();

            // Филтрираме само свободните часове
            var availableTimes = allowedTimes
                .Where(time => !reservedTimes.Contains(time))
                .Select(time => time.ToString(@"hh\:mm"))
                .ToList();

            return Json(new { success = true, availableTimes });
        }





        // POST: Services/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,NameService,CategoryOfService,ReservationDate,Description, Price")] Service service)
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