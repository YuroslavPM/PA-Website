using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PA_Website.Services
{
    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;
        private readonly IWebHostEnvironment _environment;
        
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB max upload

        public ImageService(ILogger<ImageService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!AllowedExtensions.Contains(extension))
                return false;

            if (file.Length > MaxFileSizeBytes)
                return false;

            // Validate it's actually an image 
            try
            {
                using var stream = file.OpenReadStream();
                var format = Image.DetectFormat(stream);
                return format != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ImageOptimizationResult> OptimizeAndSaveAsync(
            IFormFile file,
            string subFolder,
            int maxWidth = 1920,
            int maxHeight = 1080,
            int quality = 80)
        {
            if (!IsValidImageFile(file))
            {
                return ImageOptimizationResult.Failure("Invalid image file. Supported formats: JPG, PNG, GIF, WebP, BMP");
            }

            try
            {
                var originalSize = file.Length;
                
                var outputExtension = ".webp";
                var fileName = $"{Guid.NewGuid()}{outputExtension}";
                
                var directoryPath = Path.Combine(_environment.WebRootPath, "Images", subFolder);
                Directory.CreateDirectory(directoryPath);
                
                var filePath = Path.Combine(directoryPath, fileName);

                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream);

                var resizeOptions = new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                };

                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    image.Mutate(x => x.Resize(resizeOptions));
                }

                var webpEncoder = new WebpEncoder
                {
                    Quality = quality,
                    FileFormat = WebpFileFormatType.Lossy,
                    Method = WebpEncodingMethod.BestQuality,
                    UseAlphaCompression = true,
                    EntropyPasses = 3,
                    SpatialNoiseShaping = 50,
                    FilterStrength = 60
                };

                await using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await image.SaveAsync(outputStream, webpEncoder);
                
                var optimizedSize = new FileInfo(filePath).Length;
                var relativePath = $"/Images/{subFolder}/{fileName}";

                _logger.LogInformation(
                    "Image optimized: Original={OriginalSize}KB, Optimized={OptimizedSize}KB, Saved={Saved}%",
                    originalSize / 1024,
                    optimizedSize / 1024,
                    Math.Round((1 - (double)optimizedSize / originalSize) * 100, 1));

                return ImageOptimizationResult.Success(relativePath, originalSize, optimizedSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize and save image");
                return ImageOptimizationResult.Failure("An error occurred while processing the image.");
            }
        }


        public async Task<ImageOptimizationResult> OptimizeAndSaveOriginalFormatAsync(
            IFormFile file,
            string subFolder,
            int maxWidth = 1920,
            int maxHeight = 1080,
            int quality = 80)
        {
            if (!IsValidImageFile(file))
            {
                return ImageOptimizationResult.Failure("Invalid image file. Supported formats: JPG, PNG, GIF, WebP, BMP");
            }

            try
            {
                var originalSize = file.Length;
                var originalExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{originalExtension}";
                
                var directoryPath = Path.Combine(_environment.WebRootPath, "Images", subFolder);
                Directory.CreateDirectory(directoryPath);
                
                var filePath = Path.Combine(directoryPath, fileName);

                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream);

                // Resize if necessary
                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maxWidth, maxHeight)
                    }));
                }

                await using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                
                switch (originalExtension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsync(outputStream, new JpegEncoder { Quality = quality });
                        break;
                    case ".png":
                        await image.SaveAsync(outputStream, new PngEncoder 
                        { 
                            CompressionLevel = PngCompressionLevel.BestCompression,
                            FilterMethod = PngFilterMethod.Adaptive
                        });
                        break;
                    case ".webp":
                        await image.SaveAsync(outputStream, new WebpEncoder 
                        { 
                            Quality = quality,
                            Method = WebpEncodingMethod.BestQuality
                        });
                        break;
                    default:
                        await image.SaveAsync(outputStream, new WebpEncoder { Quality = quality });
                        break;
                }

                var optimizedSize = new FileInfo(filePath).Length;
                var relativePath = $"/Images/{subFolder}/{fileName}";

                _logger.LogInformation(
                    "Image optimized (original format): {Extension}, Original={OriginalSize}KB, Optimized={OptimizedSize}KB",
                    originalExtension,
                    originalSize / 1024,
                    optimizedSize / 1024);

                return ImageOptimizationResult.Success(relativePath, originalSize, optimizedSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize and save image in original format");
                return ImageOptimizationResult.Failure("An error occurred while processing the image.");
            }
        }

        public Task<bool> DeleteImageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return Task.FromResult(false);

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Image deleted: {ImagePath}", imagePath);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {ImagePath}", imagePath);
                return Task.FromResult(false);
            }
        }
    }
}

