using Npgsql;

namespace PictureStorage;

public sealed class ImageRepositoryDB(string connectionString) : IImageRepository
{
    public List<string>? GetAll(int pageNumber, int pageSize)
    {
        if (pageNumber < 1 || pageSize < 1) return [];

        const string sql = "SELECT picturename FROM picturedata ORDER BY picturename LIMIT @limit OFFSET @offset";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", pageSize);
        command.Parameters.AddWithValue("offset", (long)(pageNumber - 1) * pageSize);

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0)) result.Add(reader.GetString(0));
        }
        return result;
    }

    public List<string>? GetTop(int top)
    {
        if (top < 1) return [];

        const string sql = "SELECT picturename FROM picturedata ORDER BY id LIMIT @limit";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", top);

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0)) result.Add(reader.GetString(0));
        }
        return result;
    }

    public int Add(string? filePath, string? pictureName)
    {
        const string sql = "INSERT INTO picturedata (filepath, picturename) VALUES (@filepath, @picturename)";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("filepath", (object?)filePath ?? DBNull.Value);
        command.Parameters.AddWithValue("picturename", (object?)pictureName ?? DBNull.Value);
        return command.ExecuteNonQuery();
    }

    public ImageData? GetById(int id)
    {
        const string sql = "SELECT id, filepath, picturename FROM picturedata WHERE id = @id";
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
        const string sql = "DELETE FROM picturedata WHERE id = @id";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return command.ExecuteNonQuery();
    }
}
