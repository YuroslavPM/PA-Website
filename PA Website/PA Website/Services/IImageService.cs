namespace PA_Website.Services
{
    public interface IImageService
    {

        Task<ImageOptimizationResult> OptimizeAndSaveAsync(
            IFormFile file,
            string subFolder,
            int maxWidth = 1920,
            int maxHeight = 1080,
            int quality = 80);

        bool IsValidImageFile(IFormFile file);
        Task<bool> DeleteImageAsync(string imagePath);
    }

    public class ImageOptimizationResult
    {
        public bool IsSuccess { get; set; }
        public string? ImagePath { get; set; }
        public string? ErrorMessage { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long OptimizedSizeBytes { get; set; }
        public double CompressionRatio => OriginalSizeBytes > 0 
            ? Math.Round((1 - (double)OptimizedSizeBytes / OriginalSizeBytes) * 100, 2) 
            : 0;

        public static ImageOptimizationResult Success(string imagePath, long originalSize, long optimizedSize)
        {
            return new ImageOptimizationResult
            {
                IsSuccess = true,
                ImagePath = imagePath,
                OriginalSizeBytes = originalSize,
                OptimizedSizeBytes = optimizedSize
            };
        }

        public static ImageOptimizationResult Failure(string errorMessage)
        {
            return new ImageOptimizationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}


