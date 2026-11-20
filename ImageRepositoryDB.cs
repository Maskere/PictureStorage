using Microsoft.Data.Sqlite;
namespace PictureStorage;

public class ImageRepositoryDB : IImageRepository{
    private string? connectionString => Program.connectionString;

    public List<string>? GetAll(int pageNumber, int pageSize){
        List<string> res = new();
        try{
            using(SqliteConnection con = new(connectionString)){
                con.Open();
                string q = @"select picturename from picturedata order by picturename limit @pageNumber offset @pagesize";
                using(SqliteCommand cmd = new(q,con)){
                    cmd.Parameters.AddWithValue("@pageNumber",pageNumber);
                    cmd.Parameters.AddWithValue("@pagesize",pageSize);

                    using(SqliteDataReader reader = cmd.ExecuteReader()){
                        while(reader.Read()){
                            res.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }
        catch(Exception){
            return null;
        }

        return res;
    }

    public List<string>? GetTop(int top){
        List<string> res = new();
        try{
            using(SqliteConnection con = new(connectionString)){
                con.Open();
                string q = "select picturename from picturedata limit @count";
                using(SqliteCommand cmd = new(q,con)){
                    cmd.Parameters.AddWithValue("@count",top);

                    using(SqliteDataReader reader = cmd.ExecuteReader()){
                        while(reader.Read()){
                            res.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }
        catch(Exception){
            return null;
        }
        return res;
    }

    public int Add(string? filePath,string? picturename){
        // if(imageData == null) throw new ArgumentException("Invalid image");
        int res = -1;
        try{
            using (SqliteConnection con = new(connectionString)){
                con.Open();
                string q = @"INSERT into PictureData values (@id,@filepath,@picturename)";

                con.Open();
                using(SqliteCommand cmd = new(q,con)){
                    cmd.Parameters.AddWithValue("@id",DBNull.Value);
                    cmd.Parameters.AddWithValue("@filepath",(object?)filePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@picturename",(object?)picturename ?? DBNull.Value);
                    res = cmd.ExecuteNonQuery();
                }
            };
            return res;
        }
        catch(Exception ex){
            Console.WriteLine(ex.Message);
            return res;
        }
    }

    public ImageData? GetById(int id){
        try{
            SqliteConnection con = new(connectionString);
            con.Open();
            string q = "select filepath from imagedata where id = @id";
            SqliteCommand cmd = new(q,con);
            cmd.Parameters.AddWithValue("@id",id);

            ImageData ret = new(){FilePath = (string?)cmd.ExecuteScalar()};
            return ret;
        }
        catch(Exception){
            return null;
        }
    }

    public int Remove(int id){
        try{
            SqliteConnection con = new(connectionString);
            con.Open();
            string q = "delete from imagedata where id = @id";
            SqliteCommand cmd = new(q,con);
            cmd.Parameters.AddWithValue("@id",id);
            return cmd.ExecuteNonQuery();
        }
        catch(Exception){
            return -1;
        }
    }
}
