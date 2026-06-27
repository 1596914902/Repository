namespace MediaPlayer.Models;

/// <summary>
/// 媒体文件项模型，用于表示播放列表中的单个媒体条目
/// </summary>
public class MediaItem
{
    /// <summary>数据库主键ID</summary>
    public int Id { get; set; }

    /// <summary>媒体文件完整路径</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>媒体文件标题（从文件路径中提取的文件名，不含扩展名）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>媒体文件时长（格式：MM:SS，仅音频文件时使用）</summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>添加到播放列表的时间</summary>
    public DateTime AddedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 从文件路径创建MediaItem实例
    /// </summary>
    /// <param name="filePath">文件的完整路径</param>
    /// <returns>初始化的MediaItem对象</returns>
    public static MediaItem FromFilePath(string filePath)
    {
        return new MediaItem
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            AddedTime = DateTime.Now
        };
    }

    public override string ToString() => Title;
}
