using Microsoft.AspNetCore.Identity;
using PA_Website.Models;

namespace PA_Website.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<UserProfileService> _logger;

        public UserProfileService(UserManager<User> userManager, ILogger<UserProfileService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<bool> UpdateProfileAsync(User user, UpdateProfileRequest request)
        {
            try
            {
                user.FName = request.FName;
                user.LName = request.LName;
                user.Email = request.Email;
                user.PhoneNumber = request.PhoneNumber;
                user.Birth_Date = request.BirthDate;

                // Automatically recalculate zodiac sign based on birth date
                user.Zodiacal_Sign = CalculateZodiacSign(request.BirthDate);

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Profile updated successfully for user: {UserId}", user.Id);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to update profile for user {UserId}: {Errors}", 
                        user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while updating profile for user: {UserId}", user.Id);
                return false;
            }
        }

        public string CalculateZodiacSign(DateTime birthDate)
        {
            return birthDate.Month switch
            {
                1 => birthDate.Day <= 19 ? "Козирог" : "Водолей",
                2 => birthDate.Day <= 18 ? "Водолей" : "Риби",
                3 => birthDate.Day <= 20 ? "Риби" : "Овен",
                4 => birthDate.Day <= 19 ? "Овен" : "Телец",
                5 => birthDate.Day <= 20 ? "Телец" : "Близнаци",
                6 => birthDate.Day <= 20 ? "Близнаци" : "Рак",
                7 => birthDate.Day <= 22 ? "Рак" : "Лъв",
                8 => birthDate.Day <= 22 ? "Лъв" : "Дева",
                9 => birthDate.Day <= 22 ? "Дева" : "Везни",
                10 => birthDate.Day <= 22 ? "Везни" : "Скорпион",
                11 => birthDate.Day <= 21 ? "Скорпион" : "Стрелец",
                12 => birthDate.Day <= 21 ? "Стрелец" : "Козирог",
                _ => "Неизвестна зодия"
            };
        }
    }
} 