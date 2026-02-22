using Microsoft.AspNetCore.Mvc;
using PA_Website.Data;
using PA_Website.Models;
using System.ComponentModel.DataAnnotations;

namespace PA_Website.Controllers
{
    public class NewsletterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NewsletterController> _logger;

        public NewsletterController(ApplicationDbContext context, ILogger<NewsletterController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Subscribe(string email)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            {
                var msg = "Въведете валиден имейл адрес.";
                if (isAjax) return Json(new { success = false, message = msg });
                TempData["NewsletterMessage"] = msg;
                return RedirectToAction("Index", "Home");
            }

            var exists = _context.NewsletterSubscribers.Any(x => x.Email == email);
            if (exists)
            {
                var msg = "Този имейл вече е абониран към бюлетина!";
                if (isAjax) return Json(new { success = false, message = msg });
                TempData["NewsletterMessage"] = msg;
                return RedirectToAction("Index", "Home");
            }

            var subscriber = new NewsletterSubscription
            {
                Email = email,
                ConsentGiven = true
            };

            _context.NewsletterSubscribers.Add(subscriber);
            _context.SaveChanges();

            var successMsg = "Благодаря за абонирането към бюлетина!";
            if (isAjax) return Json(new { success = true, message = successMsg });
            TempData["NewsletterMessage"] = successMsg;
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Unsubscribe(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Index", "Home");

            var subscriber = _context.NewsletterSubscribers.FirstOrDefault(x => x.Email == email);
            if (subscriber != null)
            {
                _context.NewsletterSubscribers.Remove(subscriber);
                _context.SaveChanges();
                TempData["NewsletterMessage"] = "Вие се отабонирахте към бюлетина.";
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
