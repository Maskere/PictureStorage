using Npgsql;

namespace PictureStorage;

public sealed class ImageRepositoryDB(string connectionString, string uploadsRoot) : IImageRepository
{
    private readonly string _uploadsRoot = Path.GetFullPath(uploadsRoot);
    private readonly Lazy<Dictionary<string, string>> _fileIndex = new(() =>
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return Directory.EnumerateFiles(Path.GetFullPath(uploadsRoot), "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Name = Path.GetFileName(path),
                RelativePath = Path.GetRelativePath(Path.GetFullPath(uploadsRoot), path)
                    .Replace(Path.DirectorySeparatorChar, '/')
            })
            .GroupBy(file => file.Name, comparer)
            .ToDictionary(group => group.Key, group => group.First().RelativePath, comparer);
    });

    public List<string>? GetAll(int pageNumber, int pageSize, string mediaType, string sortOrder)
    {
        if (pageNumber < 1 || pageSize < 1) return [];

        var direction = sortOrder == "oldest" ? "ASC" : "DESC";
        var sql = $@"
            SELECT file_path, filename
            FROM pictures
            WHERE (
                    @mediaType = 'all'
                    OR (@mediaType = 'images' AND (lower(filename) LIKE '%.jpg' OR lower(filename) LIKE '%.jpeg' OR lower(filename) LIKE '%.png' OR lower(filename) LIKE '%.gif' OR lower(filename) LIKE '%.webp' OR lower(filename) LIKE '%.bmp' OR lower(filename) LIKE '%.tif' OR lower(filename) LIKE '%.tiff' OR lower(filename) LIKE '%.avif' OR lower(filename) LIKE '%.heic'))
                    OR (@mediaType = 'videos' AND (lower(filename) LIKE '%.mp4' OR lower(filename) LIKE '%.webm' OR lower(filename) LIKE '%.ogg' OR lower(filename) LIKE '%.mov' OR lower(filename) LIKE '%.m4v' OR lower(filename) LIKE '%.avi' OR lower(filename) LIKE '%.mkv' OR lower(filename) LIKE '%.3gp' OR lower(filename) LIKE '%.mpeg' OR lower(filename) LIKE '%.mpg'))
              )
            ORDER BY COALESCE(captured_at, uploaded_at) {direction} NULLS LAST, id {direction}
            LIMIT @limit OFFSET @offset";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("mediaType", mediaType);
        command.Parameters.AddWithValue("limit", pageSize);
        command.Parameters.AddWithValue("offset", (long)(pageNumber - 1) * pageSize);

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                result.Add(GetRelativePath(reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    public List<string>? GetTop(int top)
    {
        if (top < 1) return [];

        const string sql = "SELECT file_path, filename FROM pictures ORDER BY id LIMIT @limit";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", top);

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                result.Add(GetRelativePath(reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    public int Add(string? filePath, string? pictureName, DateTime? capturedAt = null)
    {
        const string sql = "INSERT INTO pictures (file_path, filename, captured_at, capture_date_checked) VALUES (@file_path, @filename, @captured_at, TRUE)";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("file_path", (object?)filePath ?? DBNull.Value);
        command.Parameters.AddWithValue("filename", (object?)pictureName ?? DBNull.Value);
        command.Parameters.AddWithValue("captured_at", (object?)capturedAt ?? DBNull.Value);
        return command.ExecuteNonQuery();
    }

    public int AddRange(IEnumerable<(string? FilePath, string? PictureName, DateTime? CapturedAt)> images)
    {
        var items = images.ToArray();
        if (items.Length == 0) return 0;

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var inserted = 0;

        foreach (var batch in items.Chunk(500))
        {
            var values = new List<string>(batch.Length);
            using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };

            for (var i = 0; i < batch.Length; i++)
            {
                var filePathParameter = $"file_path_{i}";
                var fileNameParameter = $"filename_{i}";
                var capturedAtParameter = $"captured_at_{i}";
                values.Add($"(@{filePathParameter}, @{fileNameParameter}, @{capturedAtParameter}, TRUE)");
                command.Parameters.AddWithValue(filePathParameter, (object?)batch[i].FilePath ?? DBNull.Value);
                command.Parameters.AddWithValue(fileNameParameter, (object?)batch[i].PictureName ?? DBNull.Value);
                command.Parameters.AddWithValue(capturedAtParameter, (object?)batch[i].CapturedAt ?? DBNull.Value);
            }

            command.CommandText = $"INSERT INTO pictures (file_path, filename, captured_at, capture_date_checked) VALUES {string.Join(", ", values)}";
            inserted += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return inserted;
    }

    public List<(int Id, string? FilePath, string? PictureName)> GetWithoutCaptureDate(int limit)
    {
        const string sql = "SELECT id, file_path, filename FROM pictures WHERE capture_date_checked = FALSE ORDER BY id LIMIT @limit";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);
        using var reader = command.ExecuteReader();
        var rows = new List<(int, string?, string?)>();
        while (reader.Read()) rows.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return rows;
    }

    public int UpdateCaptureDate(int id, DateTime? capturedAt)
    {
        const string sql = "UPDATE pictures SET captured_at = @captured_at, capture_date_checked = TRUE WHERE id = @id AND capture_date_checked = FALSE";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("captured_at", (object?)capturedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("id", id);
        return command.ExecuteNonQuery();
    }

    public ImageData? GetById(int id)
    {
        const string sql = "SELECT id, file_path, filename FROM pictures WHERE id = @id";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new ImageData
        {
            Id = reader.GetInt32(0),
            FilePath = reader.IsDBNull(1) ? null : reader.GetString(1),
            PictureName = reader.IsDBNull(2) ? null : reader.GetString(2)
        };
    }

    public int Remove(int id)
    {
        const string sql = "DELETE FROM pictures WHERE id = @id";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return command.ExecuteNonQuery();
    }

    private string GetRelativePath(string storedPath, string fileName)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.IsPathRooted(storedPath)
                ? storedPath
                : Path.Combine(_uploadsRoot, storedPath));
            var relativePath = Path.GetRelativePath(_uploadsRoot, fullPath);
            var isInsideUploads = relativePath != ".."
                && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath);

            if (isInsideUploads && File.Exists(fullPath))
                return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (ArgumentException)
        {
            // Fall back to finding the file by name when stored paths are stale or malformed.
        }

        return _fileIndex.Value.TryGetValue(fileName, out var foundPath) ? foundPath : fileName;
    }
}
