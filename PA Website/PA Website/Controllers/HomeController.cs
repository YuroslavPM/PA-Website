using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using PA_Website.ViewModels;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace PA_Website.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public IActionResult Index()
        {
            var latestArticles = _context.Articles
            .OrderByDescending(a => a.PublicationDate)
            .Take(3)
            .ToList();

            var services = _context.Service
                .OrderBy(x => Guid.NewGuid())
                .Take(3)
                .ToList();

            ViewBag.Services = services;
            return View(latestArticles);
            
        }

        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult Author()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
