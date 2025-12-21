using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using SimpleMvcSitemap;

namespace PA_Website.Controllers
{
    public class SeoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SeoController> _logger;

        public SeoController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<SeoController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        //Generates robots.txt file
        [Route("robots.txt")]
        public IActionResult RobotsTxt()
        {
            var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var sitemapUrl = $"{baseUrl}/sitemap.xml";

            var robotsContent = $@"# Robots.txt for Душевна Мозайка

# Allow all search engine crawlers
User-agent: *
Allow: /
Disallow: /Identity/
Disallow: /UserServices/
Disallow: /Users/
Disallow: /Services/Create
Disallow: /Services/Edit
Disallow: /Services/Delete
Disallow: /Articles/Create
Disallow: /Articles/Edit
Disallow: /Articles/Delete
Disallow: /Promotions/Create
Disallow: /Promotions/Edit
Disallow: /Promotions/Delete

# Block AI training crawlers
User-agent: GPTBot
Disallow: /

User-agent: ChatGPT-User
Disallow: /

User-agent: CCBot
Disallow: /

User-agent: Google-Extended
Disallow: /

User-agent: anthropic-ai
Disallow: /

User-agent: Claude-Web
Disallow: /

User-agent: Bytespider
Disallow: /

User-agent: FacebookBot
Disallow: /

User-agent: cohere-ai
Disallow: /

User-agent: PerplexityBot
Disallow: /

User-agent: Amazonbot
Disallow: /

User-agent: ClaudeBot
Disallow: /

User-agent: Omgilibot
Disallow: /

User-agent: Diffbot
Disallow: /

User-agent: YouBot
Disallow: /

User-agent: ImagesiftBot
Disallow: /

User-agent: Applebot-Extended
Disallow: /

# Sitemap location
Sitemap: {sitemapUrl}";

            return Content(robotsContent, "text/plain");
        }

        // Generates sitemap.xml file
        [Route("sitemap.xml")]
        public async Task<IActionResult> SitemapXml()
        {
            try
            {
                var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var nodes = new List<SitemapNode>();

                // Static pages
                nodes.Add(new SitemapNode(baseUrl + Url.Action("Index", "Home"))
                {
                    ChangeFrequency = ChangeFrequency.Daily,
                    Priority = (decimal?)1.0,
                    LastModificationDate = DateTime.UtcNow
                });

                nodes.Add(new SitemapNode(baseUrl + Url.Action("Author", "Home"))
                {
                    ChangeFrequency = ChangeFrequency.Monthly,
                    Priority = (decimal?)0.8,
                    LastModificationDate = DateTime.UtcNow
                });

                nodes.Add(new SitemapNode(baseUrl + Url.Action("Privacy", "Home"))
                {
                    ChangeFrequency = ChangeFrequency.Monthly,
                    Priority = (decimal?)0.5,
                    LastModificationDate = DateTime.UtcNow
                });

                // Services index
                nodes.Add(new SitemapNode(baseUrl + Url.Action("Index", "Services"))
                {
                    ChangeFrequency = ChangeFrequency.Weekly,
                    Priority = (decimal?)0.9,
                    LastModificationDate = DateTime.UtcNow
                });

                var services = await _context.Service.ToListAsync();
                foreach (var service in services)
                {
                    nodes.Add(new SitemapNode(baseUrl + Url.Action("Details", "Services", new { id = service.Id }))
                    {
                        ChangeFrequency = ChangeFrequency.Weekly,
                        Priority = (decimal?)0.8,
                        LastModificationDate = service.ReservationDate
                    });
                }

                // Articles index
                nodes.Add(new SitemapNode(baseUrl + Url.Action("Index", "Articles"))
                {
                    ChangeFrequency = ChangeFrequency.Daily,
                    Priority = (decimal?)0.9,
                    LastModificationDate = DateTime.UtcNow
                });

                var articles = await _context.Articles.ToListAsync();
                foreach (var article in articles)
                {
                    nodes.Add(new SitemapNode(baseUrl + Url.Action("Details", "Articles", new { id = article.Id }))
                    {
                        ChangeFrequency = ChangeFrequency.Weekly,
                        Priority = (decimal?)0.8,
                        LastModificationDate = article.PublicationDate
                    });
                }

                nodes.Add(new SitemapNode(baseUrl + Url.Action("Index", "Promotions"))
                {
                    ChangeFrequency = ChangeFrequency.Weekly,
                    Priority = (decimal?)0.7,
                    LastModificationDate = DateTime.UtcNow
                });

                var promotions = await _context.Promotions.ToListAsync();
                foreach (var promotion in promotions)
                {
                    nodes.Add(new SitemapNode(baseUrl + Url.Action("Details", "Promotions", new { id = promotion.Id }))
                    {
                        ChangeFrequency = ChangeFrequency.Weekly,
                        Priority = (decimal?)0.7,
                        LastModificationDate = promotion.StartDate
                    });
                }

                return new SitemapProvider().CreateSitemap(new SitemapModel(nodes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sitemap");
                return StatusCode(500, "Error generating sitemap");
            }
        }
    }
}