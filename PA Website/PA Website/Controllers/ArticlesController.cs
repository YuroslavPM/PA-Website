using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using PA_Website.Services;
using PA_Website.Helpers;

namespace PA_Website.Controllers
{
    public class ArticlesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IImageService _imageService;


        public ArticlesController(
            ApplicationDbContext context, 
            UserManager<User> userManager, 
            IConfiguration configuration,
            IImageService imageService)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _imageService = imageService;
        }


        public async Task<IActionResult> Index(int pageNumber=1, int pageSize=9)
        {
            var articles = _context.Articles
                .Include(a => a.Creator)
                .OrderByDescending(a => a.PublicationDate)
                .AsQueryable();

            int totalRecords = await _context.Articles.CountAsync();
            var pagedArticles = await articles.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);

            return View(pagedArticles);
        }

        
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Article? article = null;

            // Check if it's an ID for backward compatibility
            if (int.TryParse(id, out int articleId))
            {
                article = await _context.Articles.FindAsync(articleId);
                if (article != null)
                {
                    // Redirect to slug version (301 Permanent Redirect for SEO)
                    if (string.IsNullOrEmpty(article.Slug))
                    {
                        article.Slug = article.Title.ToSlug();
                        _context.Update(article);
                        await _context.SaveChangesAsync();
                    }
                    return RedirectToActionPermanent(nameof(Details), new { id = article.Slug });
                }
            }
            else
            {
                article = await _context.Articles.FirstOrDefaultAsync(m => m.Slug == id);
            }

            if (article == null)
            {
                return NotFound();
            }

            // Set SEO meta tags
            ViewData["Title"] = article.Title;
            ViewData["Description"] = article.Description.Length > 160 
                ? article.Description.Substring(0, 160) + "..." 
                : article.Description;
            ViewData["Keywords"] = $"{article.Category}, астрология, психология, {article.Title}";
            if (!string.IsNullOrEmpty(article.ImagePath))
            {
                ViewData["Image"] = article.ImagePath;
            }

            return View(article);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {

            var tinyMceApiKey = _configuration["TinyMCE:ApiKey"];
            ViewBag.TinyMceApiKey = tinyMceApiKey;

            // Populate category dropdown
            ViewData["Categories"] = new SelectList(new[]
            {
                "Психология",
                "Астрология", 
                "Личностно развитие",
                "Медитация",
                "Зодиакални знаци",
                "Други"
            });
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,Title,CreatorId,PublicationDate,Category,Description,ImageFile")] Article article)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null)
                    {
                        ModelState.AddModelError("", "Потребителят не е намерен.");
                        return View(article);
                    }

                    article.CreatorId = currentUser.Id;
                    article.PublicationDate = DateTime.Now;
                    article.Slug = article.Title.ToSlug();

                    // Handle image upload with optimization
                    if (article.ImageFile != null && article.ImageFile.Length > 0)
                    {
                        var imageResult = await _imageService.OptimizeAndSaveAsync(
                            article.ImageFile,
                            "articles",
                            maxWidth: 1200,
                            maxHeight: 800,
                            quality: 80);

                        if (!imageResult.IsSuccess)
                        {
                            ModelState.AddModelError("ImageFile", imageResult.ErrorMessage ?? "Грешка при качване на изображението.");
                            return View(article);
                        }

                        article.ImagePath = imageResult.ImagePath;
                    }

                    _context.Add(article);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Статията е създадена успешно!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Възникна грешка при създаването на статията.");
                    return View(article);
                }
            }
            return View(article);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            var tinyMceApiKey = _configuration["TinyMCE:ApiKey"];
            ViewBag.TinyMceApiKey = tinyMceApiKey;
            if (id == null)
            {
                return NotFound();
            }

            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            ViewData["Categories"] = new SelectList(new[]
            {
                "Психология",
                "Астрология",
                "Личностно развитие",
                "Медитация",
                "Зодиакални знаци",
                "Други"
            }, article.Category);
            return View(article);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,CreatorId,PublicationDate,Category,Description,ImageFile, ImagePath")] Article article)
        {
            if (id != article.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingArticle = await _context.Articles.FindAsync(id);
                    if (existingArticle == null)
                    {
                        return NotFound();
                    }

                    existingArticle.Title = article.Title;
                    existingArticle.Description = article.Description;
                    existingArticle.Category = article.Category;
                    existingArticle.Slug = article.Title.ToSlug();
                    existingArticle.PublicationDate = DateTime.Now;

                    if (article.ImageFile != null && article.ImageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(existingArticle.ImagePath))
                        {
                            await _imageService.DeleteImageAsync(existingArticle.ImagePath);
                        }

                        var imageResult = await _imageService.OptimizeAndSaveAsync(
                            article.ImageFile,
                            "articles",
                            maxWidth: 1200,
                            maxHeight: 800,
                            quality: 80);

                        if (!imageResult.IsSuccess)
                        {
                            ModelState.AddModelError("ImageFile", imageResult.ErrorMessage ?? "Грешка при качване на изображението.");
                            return View(article);
                        }

                        existingArticle.ImagePath = imageResult.ImagePath;
                    }

                    _context.Update(existingArticle);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ArticleExists(article.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(article);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var article = await _context.Articles
                .FirstOrDefaultAsync(m => m.Id == id);
            if (article == null)
            {
                return NotFound();
            }
            if (!string.IsNullOrEmpty(article.ImagePath))
            {
                await _imageService.DeleteImageAsync(article.ImagePath);
            }

            return View(article);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article != null)
            {
                _context.Articles.Remove(article);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ArticleExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadImage(IFormFile upload)
        {
            try
            {
                if (upload == null || upload.Length == 0)
                {
                    return Json(new { uploaded = 0, error = new { message = "No image file provided." } });
                }

                if (!_imageService.IsValidImageFile(upload))
                {
                    return Json(new { uploaded = 0, error = new { message = "Invalid file type or size." } });
                }

                var imageResult = await _imageService.OptimizeAndSaveAsync(
                    upload,
                    "articles/content",
                    maxWidth: 1200,
                    maxHeight: 900,
                    quality: 80);

                if (!imageResult.IsSuccess)
                {
                    return Json(new { uploaded = 0, error = new { message = imageResult.ErrorMessage } });
                }

                return Json(new { 
                    uploaded = 1, 
                    fileName = Path.GetFileName(imageResult.ImagePath), 
                    url = imageResult.ImagePath 
                });
            }
            catch (Exception ex)
            {
                return Json(new { uploaded = 0, error = new { message = "An error occurred while uploading the image." } });
            }
        }
    }
}