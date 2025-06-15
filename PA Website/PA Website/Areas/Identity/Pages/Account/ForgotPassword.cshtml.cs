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
                        <h1 style='color: #333; margin-bottom: 10px;'>Password Reset Request</h1>
                        <p style='color: #666; margin: 0;'>PA Website</p>
                    </div>
                    
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                        <h2 style='color: #495057; margin-top: 0;'>Hello!</h2>
                        <p>We received a request to reset your password for your PA Website account.</p>
                        <p>To reset your password, please click the button below:</p>
                    </div>
                    
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' 
                           style='background-color: #007bff; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>
                            Reset Your Password
                        </a>
                    </div>
                    
                    <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 0; color: #856404;'>
                            <strong>Important:</strong> This password reset link will expire in 24 hours.
                        </p>
                    </div>
                    
                    <p style='color: #666; font-size: 14px; margin-top: 30px;'>
                        <strong>Security Note:</strong> If you didn't request this password reset, please ignore this email. Your password will remain unchanged.
                    </p>
                    
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>
                        This email was sent on {DateTime.Now:yyyy-MM-dd HH:mm:ss}<br>
                        From PA Website Security System
                    </p>
                </div>";

                try
                {
                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Reset Your PA Website Password",
                        htmlMessage);

                    _logger.LogInformation($"Password reset email sent successfully to: {Input.Email}");
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error sending password reset email to: {Input.Email}");
                    ModelState.AddModelError(string.Empty, "There was an error sending the password reset email. Please try again later.");
                    return Page();
                }
            }

            return Page();
        }
    }
} 