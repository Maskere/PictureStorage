namespace PictureStorage;

public class ImageRepository : IImageRepository{
    private List<ImageData> images;

    public ImageRepository(){
        images = new();
    }

    public int Add(string? filePath, string? pictureName){
        ImageData toAdd = new(filePath,pictureName);
        images.Add(toAdd);
        return 1;
    }

    public int AddRange(IEnumerable<(string? FilePath, string? PictureName)> items){
        int added = 0;
        foreach(var item in items){
            added += Add(item.FilePath, item.PictureName);
        }
        return added;
    }

    public List<string>? GetAll(int pageNumber, int pageSize, string? searchTerm, string mediaType){
        List<string> res = new();
        IEnumerable<ImageData> filtered = images;
        if (!string.IsNullOrWhiteSpace(searchTerm))
            filtered = filtered.Where(image => image.PictureName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true);
        if (mediaType == "images")
            filtered = filtered.Where(image => IsImage(image.PictureName));
        else if (mediaType == "videos")
            filtered = filtered.Where(image => IsVideo(image.PictureName));

        foreach(ImageData imageData in filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize)){
            if(imageData.FilePath != null)
                res.Add(imageData.FilePath);
        }
        return new List<string>(res);
    }

    private static bool IsImage(string? name) => name != null &&
        new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".avif", ".heic" }
            .Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    private static bool IsVideo(string? name) => name != null &&
        new[] { ".mp4", ".webm", ".ogg", ".mov", ".m4v", ".avi", ".mkv", ".3gp", ".mpeg", ".mpg" }
            .Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    public List<string>? GetTop(int top){
        List<string> ret = new();
        foreach(ImageData id in images.GetRange(0,top)){
            if(id.FilePath != null)
                ret.Add(id.FilePath);
        }
        if(ret.Count <= 0) return null;
        return ret;
    }

    public ImageData? GetById(int id){
        int left = 0;
        int right = images.Count - 1;

        while(left <= right){
            int mid = left + (right - left) / 2;
            if(images[mid].Id == id){
                return images[mid];
            }
            else if(images[mid].Id < id){
                left = mid + 1;
            }
            else{
                right = mid - 1;
            }
        }

        return null;
    }

    public int Remove(int id){
        ImageData? toRemove = GetById(id);
        if(toRemove == null) return -1;

        images.Remove(toRemove);
        return 1;
    }

}
