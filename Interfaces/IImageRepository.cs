namespace PictureStorage;

public interface IImageRepository{
    int Add(string? filePath, string? pictureName);
    int AddRange(IEnumerable<(string? FilePath, string? PictureName)> images);
    List<string>? GetTop(int top);
    List<string>? GetAll(int pageNumber, int pageSize);
    ImageData? GetById(int id);
    int Remove(int id);
}
