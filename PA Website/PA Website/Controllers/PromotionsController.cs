using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using PA_Website.Helpers;

namespace PA_Website.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(promotions);
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Promotion? promotion = null;

            // Check if it's an ID for backward compatibility
            if (int.TryParse(id, out int promoId))
            {
                promotion = await _context.Promotions.FindAsync(promoId);
                if (promotion != null)
                {
                    if (string.IsNullOrEmpty(promotion.Slug))
                    {
                        promotion.Slug = promotion.Title.ToSlug();
                        _context.Update(promotion);
                        await _context.SaveChangesAsync();
                    }
                    return RedirectToActionPermanent(nameof(Details), new { id = promotion.Slug });
                }
            }
            else
            {
                promotion = await _context.Promotions
                    .Include(p => p.UserPromotions)
                    .ThenInclude(up => up.User)
                    .FirstOrDefaultAsync(m => m.Slug == id);
            }

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        public IActionResult Create()
        {
            ViewData["PromotionTypes"] = new SelectList(new[]
            {
                new { Value = "FirstBooking", Text = "Първа резервация" },
                new { Value = "Discount", Text = "Отстъпка" },
                new { Value = "FreeService", Text = "Безплатна услуга" },
                new { Value = "Loyalty", Text = "Лоялност" }
            }, "Value", "Text");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,PromotionType,DiscountPercentage,FixedDiscount,FreeServiceName,StartDate,EndDate,MaxUsage,IsActive")] Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                promotion.CreatedAt = DateTime.Now;
                promotion.Slug = promotion.Title.ToSlug();
                _context.Add(promotion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Промоцията е създадена успешно!";
                return RedirectToAction(nameof(Index));
            }

            ViewData["PromotionTypes"] = new SelectList(new[]
            {
                new { Value = "FirstBooking", Text = "Първа резервация" },
                new { Value = "Discount", Text = "Отстъпка" },
                new { Value = "FreeService", Text = "Безплатна услуга" },
                new { Value = "Loyalty", Text = "Лоялност" }
            }, "Value", "Text");

            return View(promotion);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }

            ViewData["PromotionTypes"] = new SelectList(new[]
            {
                new { Value = "FirstBooking", Text = "Първа резервация" },
                new { Value = "Discount", Text = "Отстъпка" },
                new { Value = "FreeService", Text = "Безплатна услуга" },
                new { Value = "Loyalty", Text = "Лоялност" }
            }, "Value", "Text");

            return View(promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,PromotionType,DiscountPercentage,FixedDiscount,FreeServiceName,StartDate,EndDate,MaxUsage,IsActive")] Promotion promotion)
        {
            if (id != promotion.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPromo = await _context.Promotions.FindAsync(id);
                    if (existingPromo == null) return NotFound();

                    existingPromo.Title = promotion.Title;
                    existingPromo.Description = promotion.Description;
                    existingPromo.PromotionType = promotion.PromotionType;
                    existingPromo.DiscountPercentage = promotion.DiscountPercentage;
                    existingPromo.FixedDiscount = promotion.FixedDiscount;
                    existingPromo.FreeServiceName = promotion.FreeServiceName;
                    existingPromo.StartDate = promotion.StartDate;
                    existingPromo.EndDate = promotion.EndDate;
                    existingPromo.MaxUsage = promotion.MaxUsage;
                    existingPromo.IsActive = promotion.IsActive;
                    existingPromo.Slug = promotion.Title.ToSlug();
                    existingPromo.UpdatedAt = DateTime.Now;

                    _context.Update(existingPromo);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Промоцията е обновена успешно!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.Id))
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

            ViewData["PromotionTypes"] = new SelectList(new[]
            {
                new { Value = "FirstBooking", Text = "Първа резервация" },
                new { Value = "Discount", Text = "Отстъпка" },
                new { Value = "FreeService", Text = "Безплатна услуга" },
                new { Value = "Loyalty", Text = "Лоялност" }
            }, "Value", "Text");

            return View(promotion);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Промоцията е изтрита успешно!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }

            promotion.IsActive = !promotion.IsActive;
            promotion.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = promotion.IsActive ? 
                "Промоцията е активирана!" : 
                "Промоцията е деактивирана!";

            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.Id == id);
        }
    }
} 