using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PA_Website.Data;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using PA_Website.Models;
using Microsoft.Extensions.Logging;

namespace PA_Website.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(UserManager<User> userManager, IEmailSender emailSender, ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    _logger.LogWarning($"Password reset requested for non-existent email: {Input.Email}");
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { code },
                    protocol: Request.Scheme);

                var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='text-align: center; margin-bottom: 30px;'>
                        <h1 style='color: #333; margin-bottom: 10px;'>Промяна на парола/h1>
                        <p style='color: #666; margin: 0;'>Dushevna Mozaika</p>
                    </div>
                    
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                        <h2 style='color: #495057; margin-top: 0;'>Здравейте!</h2>
                        <p>Получихме заявка за промяна на паролата за вашия акаунт в Dushevna Mozaika.</p>
                        <p>За да промените паролата си, моля кликнете бутона по-долу:</p>
                    </div>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' 
                           style='background-color: #007bff; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>
                            Променете паролата си
                        </a>
                    </div>
                    
                    <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 0; color: #856404;'>
                            <strong>Важно:</strong> Тази връзка за промяна на парола ще изтече след 24 часа.
                        </p>
                    </div>
                    
                    <p style='color: #666; font-size: 14px; margin-top: 30px;'>
                        <strong>Бележка за сигурност:</strong> Ако не сте поискали тази промяна на парола, моля игнорирайте този имейл. Паролата ви ще остане непроменена.
                    </p>
                    
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>
                        Този имейл е изпратен на {DateTime.Now:dd.MM.yyyy HH:mm}<br>
                        От системата за сигурност на Dushevna Mozaika
                    </p>
                </div>";

                try
                {
                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Нулиране на парола - Dushevna Mozaika",
                        htmlMessage);

                    _logger.LogInformation($"Password reset email sent successfully to: {Input.Email}");
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error sending password reset email to: {Input.Email}");
                    ModelState.AddModelError(string.Empty, "Възникна грешка при изпращането на имейла за нулиране на парола. Моля, опитайте отново по-късно.");
                    return Page();
                }
            }

            return Page();
        }
    }
} 