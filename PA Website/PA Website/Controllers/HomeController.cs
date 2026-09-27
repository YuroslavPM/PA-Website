using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PA_Website.Data;
using PA_Website.Helpers;
using PA_Website.ViewModels;

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

            ViewBag.PathCards = SitePathCatalog.Create(Url);
            ViewData["Image"] = "/Images/siteImg/Author.webp";
            ViewData["ImageWidth"] = "1584";
            ViewData["ImageHeight"] = "2376";
            return View(latestArticles);
            
        }

        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult Author()
        {
            ViewData["Title"] = "За мен";
            ViewData["Description"] = "Мариела Разпопова – психолог и астролог, създател на Душевна Мозайка.";
            ViewData["Image"] = "/Images/siteImg/Author.webp";
            ViewData["ImageWidth"] = "1584";
            ViewData["ImageHeight"] = "2376";
            return View();
        }

        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult Program()
        {
            ViewData["Title"] = "Пътешествие към себе си";
            ViewData["Description"] = "8-седмична авторска програма по терапевтично писане за себепознание и личностно развитие.";
            ViewData["Keywords"] = "терапевтично писане, пътешествие към себе си, себепознание, Душевна Мозайка";
            ViewData["Image"] = "/Images/siteImg/program-writing.png";
            return View();
        }

        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult Events()
        {
            ViewData["Title"] = "Групи и събития";
            ViewData["Description"] = "Предстоящи групи, срещи и събития на Душевна Мозайка.";
            ViewData["Keywords"] = "групи, събития, работилници, Душевна Мозайка";
            ViewData["Image"] = "/Images/siteImg/events-coming-soon.png";
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
