using Microsoft.AspNetCore.Mvc;
namespace PictureStorage;

[Route("api/[controller]")]
[ApiController]
public class PicturesController : ControllerBase {
    private readonly string s_fsDestination;
    private IImageRepository imageRepository;

    public PicturesController(IImageRepository imageRepository, IConfiguration configuration){
        this.imageRepository = imageRepository;
        s_fsDestination = Path.GetFullPath(configuration["MainFolder"]
            ?? throw new InvalidOperationException("MainFolder is not configured."));
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

        try{
            if(!Directory.Exists(s_fsDestination)) Directory.CreateDirectory(s_fsDestination);

            foreach(IFormFile f in files){
                string safeFileName = Path.GetFileName(f.FileName);
                if(safeFileName.StartsWith(".") || safeFileName.Equals(".database_uuid",StringComparison.OrdinalIgnoreCase)){
                    continue;
                }
                var savePath = Path.Combine(s_fsDestination,safeFileName);

                using(FileStream stream = new(savePath, FileMode.Create)){
                    await f.CopyToAsync(stream);
                }

                imageRepository.Add(savePath, safeFileName);
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
