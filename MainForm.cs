using System.ComponentModel;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using MediaPlayer.Models;
using MediaPlayer.Services;

// 使用别名避免与项目命名空间冲突
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace MediaPlayer;

/// <summary>
/// 多媒体播放器主窗体
/// 基于LibVLC引擎实现音视频播放，使用SQLite管理播放列表，
/// 体现了WinForms控件编程、多媒体处理、数据库连接、文件处理等技术。
/// </summary>
public partial class MainForm : Form
{
    // ==================== 核心服务与引擎 ====================
    private readonly PlaylistService _playlistService;
    private readonly LibVLC _libVlc;              // LibVLC核心引擎实例
    private readonly VlcMediaPlayer? _vlcPlayer;  // VLC媒体播放器实例
    private readonly VideoView _videoView;        // 视频渲染控件
    private readonly System.Windows.Forms.Timer _progressTimer;
    private readonly System.Windows.Forms.Timer _statusTimer;

    // ==================== UI控件声明 ====================
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _fileMenu = null!;
    private ToolStripMenuItem _openFileMenuItem = null!;
    private ToolStripMenuItem _openFolderMenuItem = null!;
    private ToolStripMenuItem _clearPlaylistMenuItem = null!;
    private ToolStripMenuItem _exitMenuItem = null!;
    private ToolStripMenuItem _playbackMenu = null!;
    private ToolStripMenuItem _repeatMenuItem = null!;
    private ToolStripMenuItem _helpMenu = null!;
    private ToolStripMenuItem _aboutMenuItem = null!;

    private Panel _videoPanel = null!;
    private ListView _playlistListView = null!;
    private SplitContainer _splitContainer = null!;

    private TableLayoutPanel _controlPanel = null!;
    private Button _btnPlay = null!;
    private Button _btnPause = null!;
    private Button _btnStop = null!;
    private Button _btnPrevious = null!;
    private Button _btnNext = null!;
    private Button _btnRemove = null!;
    private Label _lblVolume = null!;
    private TrackBar _volumeTrackBar = null!;
    private Label _lblTime = null!;
    private Label _lblStatus = null!;
    private TrackBar _progressTrackBar = null!;

    private bool _isUserSeeking = false;
    private bool _isRepeat = true;
    private long _currentDuration = 0;  // 当前媒体时长（毫秒）

    // 支持的媒体文件扩展名
    private static readonly string[] SupportedExtensions =
        [".mp3", ".wav", ".wma", ".mp4", ".avi", ".wmv", ".mkv",
         ".flv", ".mov", ".m4a", ".flac", ".ogg", ".webm", ".aac",
         ".wav", ".mid", ".midi", ".3gp", ".mpg", ".mpeg", ".ts"];

    public MainForm()
    {
        InitializeComponent();

        // 初始化LibVLC引擎（这是整个播放器的核心）
        // LibVLC是VLC媒体框架的.NET绑定，支持几乎所有音视频格式
        _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        _vlcPlayer = new VlcMediaPlayer(_libVlc);

        // 将VideoView绑定到VLC播放器
        _videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            MediaPlayer = _vlcPlayer,
            BackColor = Color.Black
        };

        // 初始化播放列表服务
        _playlistService = new PlaylistService();

        // 初始化定时器
        _progressTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _statusTimer = new System.Windows.Forms.Timer { Interval = 200 };

        // 注册事件
        RegisterEvents();

        // 将VideoView添加到视频面板
        _videoPanel.Controls.Add(_videoView);

        // 加载已保存的播放列表
        RefreshPlaylistItems();
        UpdateControlStates();
    }

    /// <summary>
    /// 初始化所有UI控件布局
    /// </summary>
    private void InitializeComponent()
    {
        // ==================== 窗体基本属性 ====================
        this.Text = "多媒体播放器 - Windows程序设计课程项目";
        this.Size = new Size(1100, 680);
        this.MinimumSize = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterScreen;

        // ==================== 菜单栏 ====================
        _menuStrip = new MenuStrip();
        _fileMenu = new ToolStripMenuItem("文件(&F)");
        _openFileMenuItem = new ToolStripMenuItem("打开文件(&O)...", null, OnOpenFileClick);
        _openFolderMenuItem = new ToolStripMenuItem("打开文件夹(&D)...", null, OnOpenFolderClick);
        _clearPlaylistMenuItem = new ToolStripMenuItem("清空播放列表(&C)", null, OnClearPlaylistClick);
        var fileSep1 = new ToolStripSeparator();
        _exitMenuItem = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close());

        _fileMenu.DropDownItems.AddRange([
            _openFileMenuItem, _openFolderMenuItem,
            fileSep1, _clearPlaylistMenuItem,
            new ToolStripSeparator(), _exitMenuItem
        ]);

        _playbackMenu = new ToolStripMenuItem("播放(&P)");
        _repeatMenuItem = new ToolStripMenuItem("循环播放", null, OnRepeatToggleClick)
        {
            Checked = true,
            CheckOnClick = true
        };
        _playbackMenu.DropDownItems.Add(_repeatMenuItem);

        _helpMenu = new ToolStripMenuItem("帮助(&H)");
        _aboutMenuItem = new ToolStripMenuItem("关于(&A)...", null, (_, _) =>
            MessageBox.Show(this,
                "多媒体播放器 v1.0\n\n" +
                "Windows程序设计课程期末项目\n" +
                "技术栈：C# + WinForms + LibVLC + SQLite\n" +
                "开发工具：Claude Code AI辅助编程\n" +
                "支持格式：MP3/WAV/MP4/AVI/MKV等主流音视频格式",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information));
        _helpMenu.DropDownItems.Add(_aboutMenuItem);

        _menuStrip.Items.AddRange([_fileMenu, _playbackMenu, _helpMenu]);

        // ==================== 分割容器 ====================
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill
        };

        // -- 左侧：视频显示面板 --
        _videoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle
        };

        _splitContainer.Panel1.Controls.Add(_videoPanel);

        // -- 右侧：播放列表 --
        _playlistListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            AllowDrop = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _playlistListView.Columns.Add("序号", 45, HorizontalAlignment.Center);
        _playlistListView.Columns.Add("标题", 220);
        _playlistListView.Columns.Add("时长", 60, HorizontalAlignment.Center);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        var lblPlaylist = new Label
        {
            Text = "  播放列表",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            BackColor = SystemColors.ControlLight,
            TextAlign = ContentAlignment.MiddleLeft
        };
        rightPanel.Controls.Add(_playlistListView);
        rightPanel.Controls.Add(lblPlaylist);
        _splitContainer.Panel2.Controls.Add(rightPanel);

        // ==================== 底部控制面板 ====================
        _controlPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            ColumnCount = 12,
            RowCount = 2,
            Padding = new Padding(8, 4, 8, 4)
        };
        _controlPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _controlPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        // --- 按钮 ---
        _btnPlay = CreateButton("▶ 播放", OnPlayClick);
        _btnPause = CreateButton("⏸ 暂停", OnPauseClick);
        _btnStop = CreateButton("⏹ 停止", OnStopClick);
        _btnPrevious = CreateButton("⏮ 上一曲", OnPreviousClick);
        _btnNext = CreateButton("⏭ 下一曲", OnNextClick);
        _btnRemove = CreateButton("✕ 移除", OnRemoveClick);

        // --- 音量 ---
        _lblVolume = new Label
        {
            Text = "音量:50%",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Width = 60
        };
        _volumeTrackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            TickFrequency = 10,
            Dock = DockStyle.Fill,
            AutoSize = false
        };

        // --- 进度 ---
        _lblTime = new Label
        {
            Text = "00:00 / 00:00",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Width = 120
        };
        _progressTrackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = 10000,
            Value = 0,
            TickFrequency = 1000,
            Dock = DockStyle.Fill,
            AutoSize = false
        };

        // --- 状态 ---
        _lblStatus = new Label
        {
            Text = "就绪 - 请打开媒体文件",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.DimGray
        };

        // 布局：第1行（进度）
        _controlPanel.Controls.Add(_lblTime, 0, 0);
        _controlPanel.SetColumnSpan(_lblTime, 2);
        _controlPanel.Controls.Add(_progressTrackBar, 2, 0);
        _controlPanel.SetColumnSpan(_progressTrackBar, 10);

        // 布局：第2行（按钮和音量）
        _controlPanel.Controls.Add(_btnPlay, 0, 1);
        _controlPanel.Controls.Add(_btnPause, 1, 1);
        _controlPanel.Controls.Add(_btnStop, 2, 1);
        _controlPanel.Controls.Add(_btnPrevious, 3, 1);
        _controlPanel.Controls.Add(_btnNext, 4, 1);
        _controlPanel.Controls.Add(_btnRemove, 5, 1);
        _controlPanel.Controls.Add(_lblVolume, 6, 1);
        _controlPanel.Controls.Add(_volumeTrackBar, 7, 1);
        _controlPanel.SetColumnSpan(_volumeTrackBar, 2);
        _controlPanel.Controls.Add(_lblStatus, 9, 1);
        _controlPanel.SetColumnSpan(_lblStatus, 3);

        // ==================== 组装窗体 ====================
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.Controls.Add(_menuStrip, 0, 0);
        mainLayout.Controls.Add(_splitContainer, 0, 1);

        this.Controls.Add(mainLayout);
        this.Controls.Add(_controlPanel);
    }

    /// <summary>
    /// 注册所有事件处理器
    /// </summary>
    private void RegisterEvents()
    {
        // 定时器
        _progressTimer.Tick += ProgressTimer_Tick;
        _statusTimer.Tick += StatusTimer_Tick;

        // 播放列表事件
        _playlistService.PlaylistChanged += PlaylistService_PlaylistChanged;
        _playlistService.CurrentIndexChanged += PlaylistService_CurrentIndexChanged;

        // 播放列表交互
        _playlistListView.MouseDoubleClick += OnPlaylistDoubleClick;
        _playlistListView.KeyDown += OnPlaylistKeyDown;
        _playlistListView.DragEnter += OnPlaylistDragEnter;
        _playlistListView.DragDrop += OnPlaylistDragDrop;

        // 音量控制
        _volumeTrackBar.Scroll += (_, _) =>
        {
            _vlcPlayer!.Volume = _volumeTrackBar.Value;
            _lblVolume.Text = $"音量:{_volumeTrackBar.Value}%";
        };

        // 进度条拖拽
        _progressTrackBar.MouseDown += (_, _) => _isUserSeeking = true;
        _progressTrackBar.MouseUp += (_, e) =>
        {
            _isUserSeeking = false;
            if (_currentDuration > 0)
            {
                long newPos = (long)(_progressTrackBar.Value / 10000.0 * _currentDuration);
                _vlcPlayer!.Time = newPos;
            }
        };

        // VLC播放器事件
        _vlcPlayer!.Playing += (_, _) =>
        {
            _progressTimer.Start();
            _statusTimer.Start();
            this.BeginInvoke(() =>
            {
                _lblStatus.Text = $"正在播放: {_playlistService.CurrentItem?.Title ?? ""}";
                UpdateControlStates();
            });
        };

        _vlcPlayer.Paused += (_, _) =>
        {
            _progressTimer.Stop();
            this.BeginInvoke(() =>
            {
                _lblStatus.Text = "已暂停";
                UpdateControlStates();
            });
        };

        _vlcPlayer.Stopped += (_, _) =>
        {
            _progressTimer.Stop();
            _statusTimer.Stop();
            this.BeginInvoke(() =>
            {
                _progressTrackBar.Value = 0;
                _lblTime.Text = "00:00 / 00:00";
                _lblStatus.Text = "已停止";
                UpdateControlStates();
            });
        };

        // 播放结束 -> 自动播放下一曲
        _vlcPlayer.EndReached += (_, _) =>
        {
            this.BeginInvoke(() =>
            {
                _lblStatus.Text = "播放完毕";
                if (_isRepeat && _playlistService.Items.Count > 1)
                {
                    int nextIdx = _playlistService.GetNextIndex();
                    _playlistService.CurrentIndex = nextIdx;
                    PlayCurrentItem();
                }
                else
                {
                    _progressTimer.Stop();
                    _statusTimer.Stop();
                    UpdateControlStates();
                }
            });
        };

        // 媒体时长变更
        _vlcPlayer.LengthChanged += (_, e) =>
        {
            _currentDuration = e.Length;
        };

        // 播放错误
        _vlcPlayer.EncounteredError += (_, _) =>
        {
            this.BeginInvoke(() =>
            {
                _lblStatus.Text = "播放出错，正在跳过...";
                if (_isRepeat && _playlistService.Items.Count > 1)
                {
                    int nextIdx = _playlistService.GetNextIndex();
                    _playlistService.CurrentIndex = nextIdx;
                    PlayCurrentItem();
                }
            });
        };

        // 窗口关闭
        this.FormClosing += OnFormClosing;
        this.Load += (_, _) =>
        {
            _splitContainer.Panel1MinSize = 400;
            _splitContainer.Panel2MinSize = 200;
            _splitContainer.SplitterDistance = (int)(this.ClientSize.Width * 0.65);
        };
    }

    #region UI辅助方法

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(2),
            Font = new Font("Microsoft YaHei UI", 9),
            FlatStyle = FlatStyle.System
        };
        btn.Click += onClick;
        return btn;
    }

    #endregion

    #region 播放控制

    private void OnPlayClick(object? sender, EventArgs e)
    {
        if (_playlistService.CurrentItem == null && _playlistService.Items.Count > 0)
            _playlistService.CurrentIndex = 0;

        if (_playlistService.CurrentItem == null)
        {
            _lblStatus.Text = "播放列表为空，请先添加媒体文件";
            return;
        }
        PlayCurrentItem();
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        if (_vlcPlayer!.IsPlaying)
            _vlcPlayer.Pause();
        else if (_playlistService.CurrentItem != null)
            _vlcPlayer.Play();
    }

    private void OnStopClick(object? sender, EventArgs e)
    {
        _vlcPlayer!.Stop();
    }

    private void OnPreviousClick(object? sender, EventArgs e)
    {
        int prevIdx = _playlistService.GetPreviousIndex();
        if (prevIdx >= 0)
        {
            _playlistService.CurrentIndex = prevIdx;
            PlayCurrentItem();
        }
    }

    private void OnNextClick(object? sender, EventArgs e)
    {
        int nextIdx = _playlistService.GetNextIndex();
        if (nextIdx >= 0)
        {
            _playlistService.CurrentIndex = nextIdx;
            PlayCurrentItem();
        }
    }

    private void OnRemoveClick(object? sender, EventArgs e)
    {
        if (_playlistListView.SelectedIndices.Count > 0)
        {
            var indices = _playlistListView.SelectedIndices
                .Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int idx in indices)
                _playlistService.RemoveAt(idx);
            RefreshPlaylistItems();
            UpdateControlStates();
        }
    }

    private void OnRepeatToggleClick(object? sender, EventArgs e)
    {
        _isRepeat = _repeatMenuItem.Checked;
    }

    /// <summary>
    /// 播放当前选中的媒体项
    /// 核心播放逻辑：使用LibVLC的Media类加载文件，通过VlcMediaPlayer播放
    /// </summary>
    private void PlayCurrentItem()
    {
        var item = _playlistService.CurrentItem;
        if (item == null || !File.Exists(item.FilePath))
        {
            _lblStatus.Text = item == null ? "无选中项" : $"文件不存在: {item.FilePath}";
            if (item != null) _playlistService.RemoveAt(_playlistService.CurrentIndex);
            return;
        }

        try
        {
            // 使用LibVLC的Media类创建媒体对象
            // Media封装了媒体文件的元数据和编解码器信息
            using var media = new Media(_libVlc, new Uri(item.FilePath));
            _vlcPlayer!.Play(media);
            HighlightCurrentItem();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"播放失败: {ex.Message}";
        }
    }

    #endregion

    #region 文件操作

    private void OnOpenFileClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择媒体文件",
            Multiselect = true,
            Filter = "媒体文件|*.mp3;*.wav;*.wma;*.mp4;*.avi;*.wmv;*.mkv;*.flv;*.mov;*.m4a;*.flac;*.ogg;*.webm|" +
                     "所有文件|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.FileNames.Length > 0)
        {
            _playlistService.AddFiles(dialog.FileNames);
            _lblStatus.Text = $"已添加 {dialog.FileNames.Length} 个文件";
        }
    }

    private void OnOpenFolderClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含媒体文件的文件夹"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var files = Directory.GetFiles(dialog.SelectedPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToArray();
            if (files.Length > 0)
            {
                _playlistService.AddFiles(files);
                _lblStatus.Text = $"已从文件夹导入 {files.Length} 个文件";
            }
            else
                _lblStatus.Text = "所选文件夹中未找到支持的媒体文件";
        }
    }

    private void OnClearPlaylistClick(object? sender, EventArgs e)
    {
        if (_playlistService.Items.Count == 0) return;
        if (MessageBox.Show(this, "确定要清空播放列表吗？", "确认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _vlcPlayer!.Stop();
            _playlistService.Clear();
        }
    }

    #endregion

    #region 播放列表交互

    private void OnPlaylistDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_playlistListView.SelectedIndices.Count == 1)
        {
            _playlistService.CurrentIndex = _playlistListView.SelectedIndices[0];
            PlayCurrentItem();
        }
    }

    private void OnPlaylistKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) OnRemoveClick(sender, e);
    }

    private void OnPlaylistDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnPlaylistDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            var mediaFiles = files.Where(f =>
                SupportedExtensions.Contains(Path.GetExtension(f).ToLower())).ToArray();
            if (mediaFiles.Length > 0)
            {
                _playlistService.AddFiles(mediaFiles);
                _lblStatus.Text = $"已通过拖放添加 {mediaFiles.Length} 个文件";
            }
        }
    }

    #endregion

    #region 定时器更新

    /// <summary>
    /// 进度定时器：每500ms更新进度条和时间显示
    /// </summary>
    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_isUserSeeking || _currentDuration <= 0) return;
        try
        {
            long pos = _vlcPlayer!.Time;
            int sliderVal = (int)(pos / (double)_currentDuration * 10000);
            if (sliderVal >= 0 && sliderVal <= 10000)
                _progressTrackBar.Value = sliderVal;
            _lblTime.Text = $"{FormatTime(pos)} / {FormatTime(_currentDuration)}";
        }
        catch
        {
            // 忽略瞬态查询错误
        }
    }

    /// <summary>
    /// 状态定时器：每200ms检查播放状态，确保UI同步
    /// </summary>
    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_vlcPlayer != null && _vlcPlayer.IsPlaying)
        {
            // 动态获取当前时长（某些流媒体格式需要解析后才知时长）
            if (_currentDuration == 0 && _vlcPlayer.Length > 0)
                _currentDuration = _vlcPlayer.Length;
        }
    }

    /// <summary>
    /// 将毫秒格式化为 mm:ss 或 hh:mm:ss
    /// </summary>
    private static string FormatTime(long milliseconds)
    {
        if (milliseconds < 0) return "00:00";
        var ts = TimeSpan.FromMilliseconds(milliseconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    #endregion

    #region UI刷新

    private void RefreshPlaylistItems()
    {
        _playlistListView.Items.Clear();
        for (int i = 0; i < _playlistService.Items.Count; i++)
        {
            var item = _playlistService.Items[i];
            bool isCurrent = i == _playlistService.CurrentIndex;
            var lvi = new ListViewItem((i + 1).ToString())
            {
                BackColor = isCurrent ? SystemColors.Highlight : SystemColors.Window,
                ForeColor = isCurrent ? SystemColors.HighlightText : SystemColors.WindowText
            };
            lvi.SubItems.Add(item.Title);
            lvi.SubItems.Add(item.Duration);
            _playlistListView.Items.Add(lvi);
        }
    }

    private void HighlightCurrentItem()
    {
        RefreshPlaylistItems();
        if (_playlistService.CurrentIndex >= 0 &&
            _playlistService.CurrentIndex < _playlistListView.Items.Count)
            _playlistListView.EnsureVisible(_playlistService.CurrentIndex);
    }

    private void UpdateControlStates()
    {
        bool hasItems = _playlistService.Items.Count > 0;
        bool isPlaying = _vlcPlayer?.IsPlaying ?? false;
        _btnPlay.Enabled = hasItems && !isPlaying;
        _btnPause.Enabled = hasItems && isPlaying;
        _btnStop.Enabled = hasItems && isPlaying;
        _btnPrevious.Enabled = hasItems;
        _btnNext.Enabled = hasItems;
    }

    #endregion

    #region 服务事件处理

    private void PlaylistService_PlaylistChanged()
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(PlaylistService_PlaylistChanged);
            return;
        }
        RefreshPlaylistItems();
        UpdateControlStates();
    }

    private void PlaylistService_CurrentIndexChanged(int newIndex)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(() => PlaylistService_CurrentIndexChanged(newIndex));
            return;
        }
        HighlightCurrentItem();
    }

    #endregion

    #region 生命周期

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _progressTimer.Stop();
        _statusTimer.Stop();
        try { _vlcPlayer?.Stop(); } catch { }
        try { _vlcPlayer?.Dispose(); } catch { }
        try { _libVlc?.Dispose(); } catch { }
        _playlistService.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _progressTimer?.Dispose();
            _statusTimer?.Dispose();
            try { _vlcPlayer?.Dispose(); } catch { }
            try { _libVlc?.Dispose(); } catch { }
            _playlistService?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}
