// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using PA_Website.Models;

namespace PA_Website.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<User> userManager,
            IUserStore<User> userStore,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>

            [Required]
            [Display(Name = "First Name")]
            public string FName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LName { get; set; }

            [Required]
            [Display(Name = "Birth Date")]
            public string Birth_Date { get; set; }

            [Required]
            [Display(Name = "Phone Number")]
            public string Phone_Number { get; set; }

            [Display(Name = "Zodiacal Sign")]
            public string Zodiacal_Sign { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
            
            public bool EmailSend { get; set; }

        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                user.FName = Input.FName;
                user.LName = Input.LName;
                user.Birth_Date = DateTime.Parse(Input.Birth_Date);
                user.PhoneNumber = Input.Phone_Number;
                user.EmailSend = Input.EmailSend;

                string zodiac;
                switch (user.Birth_Date.Month)
                {
                    case 1:
                        zodiac = user.Birth_Date.Day <= 19 ? "Козирог" : "Водолей";
                        break;
                    case 2:
                        zodiac = user.Birth_Date.Day <= 18 ? "Водолей" : "Риби";
                        break;
                    case 3:
                        zodiac = user.Birth_Date.Day <= 20 ? "Риби" : "Овен";
                        break;
                    case 4:
                        zodiac = user.Birth_Date.Day <= 19 ? "Овен" : "Телец";
                        break;
                    case 5:
                        zodiac = user.Birth_Date.Day <= 20 ? "Телец" : "Близнаци";
                        break;
                    case 6:
                        zodiac = user.Birth_Date.Day <= 20 ? "Близнаци" : "Рак";
                        break;
                    case 7:
                        zodiac = user.Birth_Date.Day <= 22 ? "Рак" : "Лъв";
                        break;
                    case 8:
                        zodiac = user.Birth_Date.Day <= 22 ? "Лъв" : "Дева";
                        break;
                    case 9:
                        zodiac = user.Birth_Date.Day <= 22 ? "Дева" : "Везни";
                        break;
                    case 10:
                        zodiac = user.Birth_Date.Day <= 22 ? "Везни" : "Скорпион";
                        break;
                    case 11:
                        zodiac = user.Birth_Date.Day <= 21 ? "Скорпион" : "Стрелец";
                        break;
                    case 12:
                        zodiac = user.Birth_Date.Day <= 21 ? "Стрелец" : "Козирог";
                        break;
                    default:
                        zodiac = "Неизвестна зодия";
                        break;
                }
                user.Zodiacal_Sign = zodiac;
                


                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'. " +
                    $"Ensure that '{nameof(User)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<User> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<User>)_userStore;
        }
    }
}
