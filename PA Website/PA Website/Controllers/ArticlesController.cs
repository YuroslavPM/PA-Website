using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;

namespace PA_Website.Controllers
{
    public class ArticlesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        
        public async Task<IActionResult> Index(int pageNumber=1, int pageSize=9)
        {
            var articles = _context.Articles
                .AsQueryable();


            int totalRecords = await _context.Articles.CountAsync();
            var pagedArticles = await articles.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalRecords / pageSize);

            return View(pagedArticles);
        }

        
        public async Task<IActionResult> Details(int? id)
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

            return View(article);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Articles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,Title,CreatorId,PublicationDate,Category,Description,ImageFile")] Article article)
        {
            if (ModelState.IsValid)
            {
                // Handle image upload first
                if (article.ImageFile != null && article.ImageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(article.ImageFile.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", "articles", fileName);

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    // Save file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await article.ImageFile.CopyToAsync(stream);
                    }

                    // Store path in database
                    article.ImagePath = $"/Images/articles/{fileName}";
                }

                // Set publication date to current time if not set
                if (article.PublicationDate == default)
                {
                    article.PublicationDate = DateTime.Now;
                }

                // Now save the article with the image path
                _context.Add(article);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(article);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            return View(article);
        }

        // POST: Articles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,CreatorId,PublicationDate,Category,Description,ImageFile")] Article article)
        {
            if (id != article.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(article);
                    var existingArticle = await _context.Articles.FindAsync(id);

                    if (article.ImageFile != null && article.ImageFile.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existingArticle.ImagePath))
                        {
                            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingArticle.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // Upload new image
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(article.ImageFile.FileName);
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", "articles", fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await article.ImageFile.CopyToAsync(stream);
                        }

                        existingArticle.ImagePath = $"/Images/articles/{fileName}";
                        existingArticle.Title = article.Title;
                        existingArticle.Description = article.Description;
                        existingArticle.Category = article.Category;

                        _context.Update(existingArticle);
                        await _context.SaveChangesAsync();
                    }
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
                return RedirectToAction(nameof(Index));
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
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", article.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            return View(article);
        }

        // POST: Articles/Delete/5
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

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(upload.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { uploaded = 0, error = new { message = "Invalid file type. Only JPG, PNG, GIF, and WebP are allowed." } });
                }

                // Validate file size (max 5MB)
                if (upload.Length > 5 * 1024 * 1024)
                {
                    return Json(new { uploaded = 0, error = new { message = "File size too large. Maximum size is 5MB." } });
                }

                // Generate unique filename
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", "articles", "content", fileName);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await upload.CopyToAsync(stream);
                }

                // Return the URL for the uploaded image (CKEditor 4 format)
                var imageUrl = $"/Images/articles/content/{fileName}";

                return Json(new { uploaded = 1, fileName = fileName, url = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { uploaded = 0, error = new { message = "An error occurred while uploading the image." } });
            }
        }
    }
}
