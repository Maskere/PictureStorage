using System.Globalization;
using ImageMagick;

namespace PictureStorage.Services;

public static class CaptureDateService
{
    private static readonly string[] Attributes = ["exif:DateTimeOriginal", "exif:CreateDate", "exif:DateTimeDigitized", "exif:DateTime"];
    private static readonly string[] Formats = ["yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss"];

    public static DateTime? Read(string path)
    {
        try
        {
            using var image = new MagickImage(path);
            foreach (var attribute in Attributes)
            {
                var value = image.GetAttribute(attribute);
                if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                    return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            }
        }
        catch (MagickException) { }
        catch (IOException) { }
        return null;
    }
}

public sealed class CaptureDateBackfillService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CaptureDateBackfillService> logger) : BackgroundService
{
    private readonly string _uploadsRoot = Path.GetFullPath(configuration["MainFolder"]!);
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".tif", ".tiff", ".png", ".webp", ".bmp", ".heic", ".avif" };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IImageRepository>();
                var rows = repository.GetWithoutCaptureDate(100);
                if (rows.Count == 0) return;

                foreach (var row in rows)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    var path = ResolvePath(row.FilePath, row.PictureName);
                    DateTime? capturedAt = path != null && ImageExtensions.Contains(Path.GetExtension(path))
                        ? CaptureDateService.Read(path)
                        : null;
                    repository.UpdateCaptureDate(row.Id, capturedAt);
                }
                await Task.Yield();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not backfill capture dates; will retry shortly.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private string? ResolvePath(string? storedPath, string? fileName)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(storedPath))
            {
                var candidate = Path.GetFullPath(Path.IsPathRooted(storedPath) ? storedPath : Path.Combine(_uploadsRoot, storedPath));
                var relative = Path.GetRelativePath(_uploadsRoot, candidate);
                if (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(candidate))
                    return candidate;
            }
        }
        catch (ArgumentException) { }
        catch (IOException) { }
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        try { return Directory.EnumerateFiles(_uploadsRoot, fileName, SearchOption.AllDirectories).FirstOrDefault(); }
        catch (IOException) { return null; }
    }
}
