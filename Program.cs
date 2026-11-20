using Npgsql;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http.Features;
namespace PictureStorage;

public class Program {
    private static string? s_fsDestination;

    public static string? connectionString;

    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);
        // connectionString = builder.Configuration.GetConnectionString("PictureDB");
        connectionString = builder.Configuration.GetConnectionString("PostgresDB");
        if(connectionString == null){
            throw new Exception("Database not found");
        }

        string? folder = builder.Configuration.GetValue<string>("MainFolder");
        if(folder == null) throw new Exception("Main folder not found");

        s_fsDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),folder);

        builder.Services.AddCors(options => {
                options.AddPolicy("AllowAll", policy =>{
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                        });
                });

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddAuthorization();
        
        builder.Services.AddScoped<IImageRepository,ImageRepositoryDB>();

        builder.Services.Configure<FormOptions>(options =>{
                options.MultipartBodyLengthLimit = 524288000;
                });

        builder.WebHost.ConfigureKestrel(options =>{
                options.Limits.MaxRequestBodySize = 524288000;
                });


        var app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions{
                FileProvider = new PhysicalFileProvider(folder),
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
