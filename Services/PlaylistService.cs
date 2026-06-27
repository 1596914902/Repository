using MediaPlayer.Models;

namespace MediaPlayer.Services;

/// <summary>
/// 播放列表管理服务，提供播放列表的业务逻辑层
/// 负责协调UI层与数据访问层（DatabaseService）之间的数据流转
/// </summary>
public class PlaylistService : IDisposable
{
    private readonly DatabaseService _db;
    private List<MediaItem> _items;
    private int _currentIndex = -1;  // 当前播放索引，-1表示未选中

    /// <summary>播放列表中的媒体项集合</summary>
    public IReadOnlyList<MediaItem> Items => _items.AsReadOnly();

    /// <summary>当前播放索引</summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        set
        {
            if (value >= -1 && value < _items.Count)
                _currentIndex = value;
        }
    }

    /// <summary>当前播放的媒体项（无选中时返回null）</summary>
    public MediaItem? CurrentItem =>
        _currentIndex >= 0 && _currentIndex < _items.Count
            ? _items[_currentIndex] : null;

    /// <summary>播放列表变更事件</summary>
    public event Action? PlaylistChanged;

    /// <summary>当前索引变更事件</summary>
    public event Action<int>? CurrentIndexChanged;

    public PlaylistService()
    {
        _db = new DatabaseService();
        _items = _db.GetAllItems();
    }

    /// <summary>
    /// 添加媒体文件到播放列表（支持批量添加）
    /// </summary>
    /// <param name="filePaths">媒体文件路径数组</param>
    public void AddFiles(string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            // 仅添加存在的文件
            if (!File.Exists(path)) continue;

            var item = MediaItem.FromFilePath(path);
            _db.AddItem(item);
        }
        RefreshFromDatabase();
    }

    /// <summary>
    /// 移除指定索引的媒体项
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;

        _db.RemoveItem(_items[index].Id);

        // 调整当前索引：如果删除的是当前项或之前的项，需要调整
        if (index == _currentIndex)
        {
            _currentIndex = -1;
            CurrentIndexChanged?.Invoke(_currentIndex);
        }
        else if (index < _currentIndex)
        {
            _currentIndex--;
            CurrentIndexChanged?.Invoke(_currentIndex);
        }

        RefreshFromDatabase();
    }

    /// <summary>清空播放列表</summary>
    public void Clear()
    {
        _db.ClearAll();
        _currentIndex = -1;
        RefreshFromDatabase();
    }

    /// <summary>获取下一首的索引（循环播放）</summary>
    public int GetNextIndex()
    {
        if (_items.Count == 0) return -1;
        return (_currentIndex + 1) % _items.Count;
    }

    /// <summary>获取上一首的索引（循环播放）</summary>
    public int GetPreviousIndex()
    {
        if (_items.Count == 0) return -1;
        return (_currentIndex - 1 + _items.Count) % _items.Count;
    }

    /// <summary>从数据库重新加载播放列表</summary>
    private void RefreshFromDatabase()
    {
        _items = _db.GetAllItems();
        PlaylistChanged?.Invoke();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
