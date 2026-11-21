# PictureStorage

ASP.NET Core API for storing image files on disk and their metadata in PostgreSQL.

## PostgreSQL setup

Create the database and application user in PostgreSQL, then run `database/schema.sql` against that database. The API expects a `pictures` table with `id`, `filename`, `file_path`, and `uploaded_at` columns.

Configure the connection string outside the repository. For example, in PowerShell:

```powershell
$env:ConnectionStrings__PostgresDB = "Host=localhost;Port=5432;Database=PictureDB;Username=picture_app;Password=<your-password>"
dotnet run --project .\PictureStorage.csproj
```

On Linux or in a systemd service, set the same `ConnectionStrings__PostgresDB` environment variable in the service environment. `MainFolder` in `appsettings.json` sets the directory used for uploaded files and static file serving.

Image thumbnails are generated on demand and cached outside `MainFolder`; originals are never changed. `ThumbnailFolder` can be set to choose another cache directory.

## Capture date sorting

Before deploying a version that sorts by capture date to an existing PostgreSQL database, run `database/2026-11-capture-dates.sql` as a database owner. The API reads EXIF capture dates when images are uploaded and backfills existing image rows in the background. Items without readable capture metadata use `uploaded_at` in the gallery sort. Query `GET /api/pictures?mediaType=all&sortOrder=newest`; accepted media types are `all`, `images`, and `videos`, and sort orders are `newest` and `oldest`.
