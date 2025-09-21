using Microsoft.AspNetCore.Mvc;
using PA_Website.Models;

namespace PA_Website.Services
{
    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly string _uploadsFolder;

        public FileService(ILogger<FileService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _uploadsFolder = Path.Combine(environment.WebRootPath, "astro-cards");
        }

        public async Task<string> UploadAstroCardAsync(IFormFile file)
        {
            if (!IsValidAstroCardFile(file))
            {
                throw new ArgumentException("Invalid file format or size");
            }

            // Create unique filename
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(_uploadsFolder, uniqueFileName);

            // Ensure directory exists
            Directory.CreateDirectory(_uploadsFolder);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Astro card uploaded successfully: {FileName}", uniqueFileName);
            return $"/astro-cards/{uniqueFileName}";
        }

        public async Task<bool> DeleteAstroCardAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
            
            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("Astro card deleted successfully: {FilePath}", filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete astro card: {FilePath}", filePath);
                    return false;
                }
            }

            return false;
        }

        public async Task<AstroCardFileResult?> DownloadAstroCardAsync(string filePath, string fileName, string contentType)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
            
            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("Astro card file not found: {FilePath}", fullPath);
                return null;
            }

            try
            {
                var memory = new MemoryStream();
                using (var stream = new FileStream(fullPath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }

                _logger.LogInformation("Astro card downloaded successfully: {FileName}", fileName);
                return new AstroCardFileResult
                {
                    Content = memory.ToArray(),
                    ContentType = contentType,
                    FileName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download astro card: {FilePath}", filePath);
                return null;
            }
        }

        public bool IsValidAstroCardFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".docx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
                return false;

            if (file.Length > 6 * 1024 * 1024) // 6MB limit
                return false;

            return true;
        }
    }
} 