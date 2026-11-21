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

    public List<string>? GetAll(int pageNumber, int pageSize)
    {
        if (pageNumber < 1 || pageSize < 1) return [];

        const string sql = "SELECT file_path, filename FROM pictures ORDER BY filename LIMIT @limit OFFSET @offset";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
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

    public int Add(string? filePath, string? pictureName)
    {
        const string sql = "INSERT INTO pictures (file_path, filename) VALUES (@file_path, @filename)";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("file_path", (object?)filePath ?? DBNull.Value);
        command.Parameters.AddWithValue("filename", (object?)pictureName ?? DBNull.Value);
        return command.ExecuteNonQuery();
    }

    public int AddRange(IEnumerable<(string? FilePath, string? PictureName)> images)
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
                values.Add($"(@{filePathParameter}, @{fileNameParameter})");
                command.Parameters.AddWithValue(filePathParameter, (object?)batch[i].FilePath ?? DBNull.Value);
                command.Parameters.AddWithValue(fileNameParameter, (object?)batch[i].PictureName ?? DBNull.Value);
            }

            command.CommandText = $"INSERT INTO pictures (file_path, filename) VALUES {string.Join(", ", values)}";
            inserted += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return inserted;
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
