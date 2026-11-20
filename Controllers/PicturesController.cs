using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Npgsql;
namespace PictureStorage;

[Route("api/[controller]")]
[ApiController]
public class PicturesController : ControllerBase {
    private string? connectionString;
    private string s_fsDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "/mnt/data-ext42/Uploads/");
    private IImageRepository imageRepository;

    public PicturesController(IImageRepository imageRepository){
        this.imageRepository = imageRepository;
        this.connectionString = Program.connectionString;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ImageData>> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20){
        // List<string>? ret = imageRepository.GetTop(10);
        List<string>? ret = imageRepository.GetAll(pageNumber,pageSize);
        if(ret == null) return NoContent();
        if(ret.Count <= 0) return NoContent();
        return Ok(ret);
    }

    [HttpGet("{id}")]
    public ActionResult<ImageData> Get(int id){
        try{
            ImageData? res = imageRepository.GetById(id);
            if(res == null) return NotFound();

            return Ok(res);
        }
        catch(ArgumentNullException ex){
            return NotFound(ex.Message);
        }
    }

    [HttpPost("single")]
    public async Task<ActionResult> Post([FromForm] IFormFile myPic){
        // Console.WriteLine("Incoming request: " + myPic);
        if(myPic == null){
            return BadRequest("No file uploaded");
        }
        try{
            string? combined = Path.Combine("Uploads",myPic.Name);
            if(!Directory.Exists(s_fsDestination)) Directory.CreateDirectory(s_fsDestination);

            string ext = Path.GetExtension(myPic.FileName).ToLower();
            string? safeFileName = Path.GetFileName(myPic.FileName);
            if(safeFileName.StartsWith(".") || safeFileName.Equals(".database_uuid",StringComparison.OrdinalIgnoreCase)){
                return BadRequest();
            }
            string savePath = Path.Combine(s_fsDestination,safeFileName);

            using(FileStream stream = new(savePath, FileMode.Create)){
                await myPic.CopyToAsync(stream);
            }

            try{
                // ImageData imageData = new(savePath,myPic.FileName);
                int res = imageRepository.Add(savePath,safeFileName);
            }
            catch(ArgumentException ex){
                return BadRequest(ex.Message);
            }

            return Ok(savePath);
        }
        catch(ArgumentNullException ex){
            return BadRequest(ex.Message);
        }
        catch(ArgumentOutOfRangeException ex){
            return BadRequest(ex.Message);
        }
        catch(ArgumentException ex){
            return BadRequest(ex.Message);
        }
        catch(Exception ex){
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("folder")]
    public async Task<ActionResult> Post([FromForm] List<IFormFile> files){
        // Console.WriteLine("Incoming request. File count: " + files.Count);

        if(files == null || files.Count == 0){
            return BadRequest("No file uploaded");
        }

        // using SqliteConnection con = new(connectionString);
        // con.Open();

        string? s = "select picturename from picturedata where picturename = @check";
        using SqliteCommand checkCmd = new(s,con);
        checkCmd.Parameters.AddWithValue("@check",SqliteType.Text);

        string? insert = "insert into picturedata values(null,@filePath,@pictureName)";
        using SqliteCommand insertCmd = new(insert,con);
        insertCmd.Parameters.AddWithValue("@filePath",SqliteType.Text);
        insertCmd.Parameters.AddWithValue("@pictureName",SqliteType.Text);

        try{
            if(!Directory.Exists(s_fsDestination)) Directory.CreateDirectory(s_fsDestination);

            foreach(IFormFile f in files){
                // Console.WriteLine(f.FileName);
                checkCmd.Parameters["@check"].Value = f.FileName;
                object? res = checkCmd.ExecuteScalar();

                if(res != null){
                    // Console.WriteLine(f.FileName + " already exists in directory. Skipping...");
                    continue;
                }

                string ext = Path.GetExtension(f.FileName).ToLower();

                string? safeFileName = Path.GetFileName(f.FileName);
                if(safeFileName.StartsWith(".") || safeFileName.Equals(".database_uuid",StringComparison.OrdinalIgnoreCase)){
                    continue;
                }
                var savePath = Path.Combine(s_fsDestination,safeFileName);

                using(FileStream stream = new(savePath, FileMode.Create)){
                    await f.CopyToAsync(stream);
                }

                insertCmd.Parameters["@filePath"].Value = savePath;
                insertCmd.Parameters["@pictureName"].Value = f.FileName;
                insertCmd.ExecuteNonQuery();
            }

            // Console.WriteLine($"Saved {files.Count} files");

            return Ok();
        }
        catch(ArgumentNullException ex){
            return BadRequest(ex.Message);
        }
        catch(ArgumentOutOfRangeException ex){
            return BadRequest(ex.Message);
        }
        catch(ArgumentException ex){
            return BadRequest(ex.Message);
        }
        catch(Exception ex){
            return BadRequest(ex.Message);
        }
    }
}

// public class ImageUploadRequest{
//     public int Id {get;set;}
//     [FromForm(Name = "my-pic")]
//     public required IFormFile MyPic {get;set;}
//
//     public override string ToString() {
//         return $"{MyPic.FileName}";
//     }
// }
