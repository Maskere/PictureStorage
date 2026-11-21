using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using PictureStorage.Services;
using ImageMagick;
namespace PictureStorage;

[Route("api/[controller]")]
[ApiController]
public class PicturesController : ControllerBase {
    private readonly string s_fsDestination;
    private readonly IImageRepository imageRepository;
    private readonly ThumbnailService thumbnailService;

    public PicturesController(IImageRepository imageRepository, ThumbnailService thumbnailService, IConfiguration configuration){
        this.imageRepository = imageRepository;
        this.thumbnailService = thumbnailService;
        s_fsDestination = Path.GetFullPath(configuration["MainFolder"]
            ?? throw new InvalidOperationException("MainFolder is not configured."));
    }

    [HttpGet]
    public ActionResult<IEnumerable<string>> Get(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string mediaType = "all"){
        mediaType = mediaType.ToLowerInvariant();
        if (mediaType is not ("all" or "images" or "videos"))
            return BadRequest("mediaType must be 'all', 'images', or 'videos'.");

        pageSize = Math.Clamp(pageSize, 1, 100);
        List<string>? ret = imageRepository.GetAll(pageNumber, pageSize, search, mediaType);
        if(ret == null) return NoContent();
        if(ret.Count <= 0) return NoContent();
        return Ok(ret);
    }

    [HttpGet("thumbnail")]
    public async Task<IActionResult> GetThumbnail([FromQuery] string path, CancellationToken cancellationToken){
        if (string.IsNullOrWhiteSpace(path)) return BadRequest("A relative image path is required.");

        try{
            string thumbnailPath = await thumbnailService.GetOrCreateAsync(path, cancellationToken);
            return PhysicalFile(thumbnailPath, "image/webp", enableRangeProcessing: true);
        }
        catch(FileNotFoundException){
            return NotFound();
        }
        catch(NotSupportedException){
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        catch(ArgumentException ex){
            return BadRequest(ex.Message);
        }
        catch(MagickException){
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
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

            List<(string? FilePath, string? PictureName)> savedFiles = new();
            foreach(IFormFile f in files){
                string safeFileName = Path.GetFileName(f.FileName);
                if(safeFileName.StartsWith(".") || safeFileName.Equals(".database_uuid",StringComparison.OrdinalIgnoreCase)){
                    continue;
                }
                var savePath = Path.Combine(s_fsDestination,safeFileName);

                using(FileStream stream = new(savePath, FileMode.Create)){
                    await f.CopyToAsync(stream);
                }

                savedFiles.Add((savePath, safeFileName));
            }

            imageRepository.AddRange(savedFiles);

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
