using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;

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

        // GET: Promotions
        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(promotions);
        }

        // GET: Promotions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.UserPromotions)
                .ThenInclude(up => up.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // GET: Promotions/Create
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

        // POST: Promotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,PromotionType,DiscountPercentage,FixedDiscount,FreeServiceName,StartDate,EndDate,MaxUsage,IsActive")] Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                promotion.CreatedAt = DateTime.Now;
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

        // GET: Promotions/Edit/5
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

        // POST: Promotions/Edit/5
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
                    promotion.UpdatedAt = DateTime.Now;
                    _context.Update(promotion);
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

        // GET: Promotions/Delete/5
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

        // POST: Promotions/Delete/5
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

        // POST: Promotions/ToggleActive/5
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