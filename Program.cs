using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http.Features;
using PictureStorage.Services;
namespace PictureStorage;

public class Program {
    private static string? s_fsDestination;
    private const long MaxRequestBodySize = 2L * 1024 * 1024 * 1024;

    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);
        string connectionString = builder.Configuration.GetConnectionString("PostgresDB")
            ?? throw new InvalidOperationException("Connection string 'PostgresDB' is not configured. Set ConnectionStrings__PostgresDB.");

        string folder = builder.Configuration.GetValue<string>("MainFolder")
            ?? throw new InvalidOperationException("MainFolder is not configured.");
        s_fsDestination = Path.GetFullPath(folder);
        string thumbnailFolder = Path.GetFullPath(builder.Configuration["ThumbnailFolder"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PictureStorage", "Thumbnails"));

        builder.Services.AddCors(options => {
                options.AddPolicy("AllowAll", policy =>{
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                        });
                });

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddAuthorization();
        
        builder.Services.AddScoped<IImageRepository>(_ => new ImageRepositoryDB(connectionString, s_fsDestination));
        builder.Services.AddSingleton(new ThumbnailService(s_fsDestination, thumbnailFolder));
        builder.Services.AddHostedService<CaptureDateBackfillService>();

        builder.Services.Configure<FormOptions>(options =>{
                options.MultipartBodyLengthLimit = MaxRequestBodySize;
                });

        builder.WebHost.ConfigureKestrel(options =>{
                options.Limits.MaxRequestBodySize = MaxRequestBodySize;
                });


        Directory.CreateDirectory(s_fsDestination);
        Directory.CreateDirectory(thumbnailFolder);
        var app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions{
                FileProvider = new PhysicalFileProvider(s_fsDestination),
                RequestPath = "/static"
                });

        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseCors("AllowAll");
        app.MapControllers();
        app.Run();
    }
}
