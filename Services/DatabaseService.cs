using System.Data.SQLite;
using MediaPlayer.Models;

namespace MediaPlayer.Services;

/// <summary>
/// SQLite数据库服务，负责播放列表数据的持久化存储
/// 技术要点：使用ADO.NET连接SQLite，通过参数化查询防止SQL注入
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private SQLiteConnection? _connection;

    /// <summary>
    /// 初始化数据库服务，数据库文件存储在应用程序目录下
    /// </summary>
    public DatabaseService()
    {
        // 数据库文件路径：应用程序目录下的 MediaPlayer.db
        string dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "MediaPlayer.db");
        _connectionString = $"Data Source={dbPath};Version=3;";
    }

    /// <summary>
    /// 获取数据库连接（懒加载，确保连接已打开）
    /// </summary>
    private SQLiteConnection Connection
    {
        get
        {
            if (_connection == null)
            {
                _connection = new SQLiteConnection(_connectionString);
                _connection.Open();
                InitializeDatabase();
            }
            return _connection;
        }
    }

    /// <summary>
    /// 初始化数据库表结构：创建Playlist表（如果不存在）
    /// </summary>
    private void InitializeDatabase()
    {
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS Playlist (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FilePath TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                Duration TEXT DEFAULT '',
                AddedTime DATETIME DEFAULT CURRENT_TIMESTAMP
            );";

        using var cmd = new SQLiteCommand(createTableSql, _connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取所有播放列表项，按添加时间降序排列
    /// </summary>
    public List<MediaItem> GetAllItems()
    {
        var items = new List<MediaItem>();
        const string sql = "SELECT Id, FilePath, Title, Duration, AddedTime FROM Playlist ORDER BY Id ASC;";

        using var cmd = new SQLiteCommand(sql, _connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new MediaItem
            {
                Id = reader.GetInt32(0),
                FilePath = reader.GetString(1),
                Title = reader.GetString(2),
                Duration = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AddedTime = reader.GetDateTime(4)
            });
        }
        return items;
    }

    /// <summary>
    /// 添加媒体项到数据库，使用参数化查询防止SQL注入
    /// </summary>
    public void AddItem(MediaItem item)
    {
        const string sql = @"
            INSERT OR IGNORE INTO Playlist (FilePath, Title, Duration)
            VALUES (@FilePath, @Title, @Duration);";

        using var cmd = new SQLiteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@FilePath", item.FilePath);
        cmd.Parameters.AddWithValue("@Title", item.Title);
        cmd.Parameters.AddWithValue("@Duration", item.Duration);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 从数据库中删除指定ID的媒体项
    /// </summary>
    public void RemoveItem(int id)
    {
        const string sql = "DELETE FROM Playlist WHERE Id = @Id;";
        using var cmd = new SQLiteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 清空整个播放列表
    /// </summary>
    public void ClearAll()
    {
        const string sql = "DELETE FROM Playlist;";
        using var cmd = new SQLiteCommand(sql, _connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 释放数据库资源
    /// </summary>
    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}
