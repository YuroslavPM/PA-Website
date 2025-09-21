using PA_Website.Models;

namespace PA_Website.Services
{
    public interface IFileService
    {
        Task<string> UploadAstroCardAsync(IFormFile file);
        Task<bool> DeleteAstroCardAsync(string filePath);
        Task<AstroCardFileResult?> DownloadAstroCardAsync(string filePath, string fileName, string contentType);
        bool IsValidAstroCardFile(IFormFile file);
    }
} 