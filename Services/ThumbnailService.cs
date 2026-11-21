using System.Collections.Concurrent;
using ImageMagick;

namespace PictureStorage.Services;

public sealed class ThumbnailService(string uploadsRoot, string cacheRoot)
{
    private readonly string _uploadsRoot = Path.GetFullPath(uploadsRoot);
    private readonly string _cacheRoot = Path.GetFullPath(cacheRoot);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _generationLimit = new(2, 2);

    public async Task<string> GetOrCreateAsync(string relativePath, CancellationToken cancellationToken)
    {
        var originalPath = ResolveUnderRoot(_uploadsRoot, relativePath);
        if (!File.Exists(originalPath)) throw new FileNotFoundException("Image file was not found.");

        if (!IsSupportedImage(Path.GetExtension(originalPath)))
            throw new NotSupportedException("Thumbnail format is not supported.");

        var cacheRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar) + ".webp";
        var thumbnailPath = ResolveUnderRoot(_cacheRoot, cacheRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);

        if (IsCacheCurrent(originalPath, thumbnailPath)) return thumbnailPath;

        var gate = _locks.GetOrAdd(thumbnailPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsCacheCurrent(originalPath, thumbnailPath)) return thumbnailPath;

            await _generationLimit.WaitAsync(cancellationToken);
            try
            {
                var temporaryPath = $"{thumbnailPath}.{Guid.NewGuid():N}.tmp.webp";
                try
                {
                    await Task.Run(() =>
                    {
                        using var image = new MagickImage(originalPath);
                        image.AutoOrient();
                        image.Thumbnail(new MagickGeometry(480, 480)
                        {
                            IgnoreAspectRatio = false
                        });
                        image.Format = MagickFormat.WebP;
                        image.Quality = 72;
                        image.Write(temporaryPath);
                    }, cancellationToken);

                    File.Move(temporaryPath, thumbnailPath, overwrite: true);
                    return thumbnailPath;
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            finally
            {
                _generationLimit.Release();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsCacheCurrent(string originalPath, string thumbnailPath) =>
        File.Exists(thumbnailPath)
        && new FileInfo(thumbnailPath).Length > 0
        && File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(originalPath);

    private static bool IsSupportedImage(string extension) =>
        new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new ArgumentException("Expected a relative path.");

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var relativeToRoot = Path.GetRelativePath(root, fullPath);
        if (relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new ArgumentException("Path must stay inside the image folder.");
        }

        return fullPath;
    }
}
