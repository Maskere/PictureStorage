namespace PictureStorage;

public interface IImageRepository{
    int Add(string? filePath, string? pictureName, DateTime? capturedAt = null);
    int AddRange(IEnumerable<(string? FilePath, string? PictureName, DateTime? CapturedAt)> images);
    List<string>? GetTop(int top);
    List<string>? GetAll(int pageNumber, int pageSize, string mediaType, string sortOrder);
    List<(int Id, string? FilePath, string? PictureName)> GetWithoutCaptureDate(int limit);
    int UpdateCaptureDate(int id, DateTime? capturedAt);
    ImageData? GetById(int id);
    int Remove(int id);
}
