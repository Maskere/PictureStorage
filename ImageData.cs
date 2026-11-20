namespace PictureStorage;

public class ImageData{
    private int id;
    private string? filePath;
    private string? pictureName;

    public ImageData(){
    }

    public ImageData(string? filePath,string? pictureName){
    }

    public int Id{
        get => id;
        set{
            id = value;
        }
    }

    public string? FilePath{
        get => filePath;
        set{
            filePath = value;
        }
    }

    public string? PictureName{
        get => pictureName;
        set{
            pictureName = value;
        }
    }

    public override string ToString() {
        return $"{PictureName}";
    }
}
