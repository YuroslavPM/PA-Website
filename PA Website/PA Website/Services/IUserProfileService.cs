using PA_Website.Models;

namespace PA_Website.Services
{
    public interface IUserProfileService
    {
        Task<bool> UpdateProfileAsync(User user, UpdateProfileRequest request);
        string CalculateZodiacSign(DateTime birthDate);
    }
} 