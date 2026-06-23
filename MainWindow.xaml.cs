using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace shiftUI;

/// <summary>
/// 主窗口 — 可调整大小、可调透明度、支持点击穿透
/// 控制面板嵌入标题栏，穿透使用 WS_EX_TRANSPARENT + 焦点自动切换
/// </summary>
public partial class MainWindow : Window
{
    // ============ Win32 常量 ============

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_HOTKEY = 0x0312;
    private const int HTNOWHERE = 0;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private const double RESIZE_BORDER = 6;

    // 全局热键
    private const int HOTKEY_TOGGLE_PENETRATE = 1;
    private const int HOTKEY_TOGGLE_MARKDOWN = 2;
    private const int HOTKEY_TOGGLE_TOPMOST = 3;
    private const int HOTKEY_TOGGLE_HIDEUI = 4;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_T = 0x54;
    private const uint VK_M = 0x4D;
    private const uint VK_L = 0x4C;
    private const uint VK_H = 0x48;

    // ============ P/Invoke ============

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    // ============ 字段 ============

    private IntPtr _hwnd;
    private bool _isClickThroughEnabled;
    private bool _isMarkdownMode;
    private bool _isUIHidden;

    // ============ 构造 ============

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += (_, _) =>
        {
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_PENETRATE);
                UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_MARKDOWN);
                UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_TOPMOST);
                UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_HIDEUI);
            }
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            if (_hwnd == IntPtr.Zero) return;

            var exStyle = (uint)GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
            if ((exStyle & WS_EX_LAYERED) == 0)
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr((long)(exStyle | WS_EX_LAYERED)));

            // 注册全局热键
            RegisterHotKey(_hwnd, HOTKEY_TOGGLE_PENETRATE, MOD_CONTROL | MOD_SHIFT, VK_T);
            RegisterHotKey(_hwnd, HOTKEY_TOGGLE_MARKDOWN, MOD_CONTROL | MOD_SHIFT, VK_M);
            RegisterHotKey(_hwnd, HOTKEY_TOGGLE_TOPMOST, MOD_CONTROL | MOD_SHIFT, VK_L);
            RegisterHotKey(_hwnd, HOTKEY_TOGGLE_HIDEUI, MOD_CONTROL | MOD_SHIFT, VK_H);

            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SourceInitialized 异常: {ex.Message}");
        }
    }

    // ============ 自适应布局 ============

    /// <summary>
    /// 窗口缩小时从最右侧按钮依次隐藏，而非一次性隐藏整个控制面板
    /// </summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ControlPanelBorder == null) return;

        // UI 隐藏时不调整控制面板（已由 ApplyUIVisibility 管理）
        if (_isUIHidden) return;

        double w = ActualWidth;

        if (w >= 480)
        {
            ControlPanelBorder.Visibility = Visibility.Visible;
            ClickThroughToggle.Visibility = Visibility.Visible;
            Sep2.Visibility = MdToggle.Visibility = Visibility.Visible;
            Sep3.Visibility = TopmostToggle.Visibility = Visibility.Visible;
            Sep4.Visibility = HideUIToggle.Visibility = Visibility.Visible;
        }
        else if (w >= 410)
        {
            ControlPanelBorder.Visibility = Visibility.Visible;
            ClickThroughToggle.Visibility = Visibility.Visible;
            Sep2.Visibility = MdToggle.Visibility = Visibility.Visible;
            Sep3.Visibility = TopmostToggle.Visibility = Visibility.Visible;
            Sep4.Visibility = HideUIToggle.Visibility = Visibility.Collapsed;
        }
        else if (w >= 340)
        {
            ControlPanelBorder.Visibility = Visibility.Visible;
            ClickThroughToggle.Visibility = Visibility.Visible;
            Sep2.Visibility = MdToggle.Visibility = Visibility.Visible;
            Sep3.Visibility = TopmostToggle.Visibility = Visibility.Collapsed;
            Sep4.Visibility = HideUIToggle.Visibility = Visibility.Collapsed;
        }
        else if (w >= 270)
        {
            ControlPanelBorder.Visibility = Visibility.Visible;
            ClickThroughToggle.Visibility = Visibility.Visible;
            Sep2.Visibility = MdToggle.Visibility = Visibility.Collapsed;
            Sep3.Visibility = TopmostToggle.Visibility = Visibility.Collapsed;
            Sep4.Visibility = HideUIToggle.Visibility = Visibility.Collapsed;
        }
        else
        {
            ControlPanelBorder.Visibility = Visibility.Collapsed;
        }
    }

    // ============ 窗口消息处理 ============

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
            {
                try
                {
                    var screenPt = new Point(
                        (short)(lParam.ToInt32() & 0xFFFF),
                        (short)((lParam.ToInt32() >> 16) & 0xFFFF));
                    var wpfPt = PointFromScreen(screenPt);

                    int edge = HitTestResizeEdge(wpfPt);
                    if (edge != HTNOWHERE)
                    {
                        handled = true;
                        return new IntPtr(edge);
                    }
                }
                catch { }
                return IntPtr.Zero;
            }

            case WM_HOTKEY:
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_TOGGLE_PENETRATE)
                    ToggleClickThrough();
                else if (id == HOTKEY_TOGGLE_MARKDOWN)
                    ToggleMarkdownMode();
                else if (id == HOTKEY_TOGGLE_TOPMOST)
                    TopmostToggle.IsChecked = !TopmostToggle.IsChecked;
                else if (id == HOTKEY_TOGGLE_HIDEUI)
                    HideUIToggle.IsChecked = !HideUIToggle.IsChecked;
                handled = true;
                return IntPtr.Zero;
            }

            case WM_GETMINMAXINFO:
            {
                if (lParam != IntPtr.Zero)
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize.X = (int)MinWidth;
                    mmi.ptMinTrackSize.Y = (int)MinHeight;
                    Marshal.StructureToPtr(mmi, lParam, false);
                    // 不设置 handled，让 WPF 也处理以正确约束尺寸
                }
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    private int HitTestResizeEdge(Point pt)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return HTNOWHERE;

        bool left = pt.X <= RESIZE_BORDER;
        bool right = pt.X >= w - RESIZE_BORDER;
        bool top = pt.Y <= RESIZE_BORDER;
        bool bottom = pt.Y >= h - RESIZE_BORDER;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        return HTNOWHERE;
    }

    // ============ 焦点事件：自动切换穿透 ============

    private void Window_Activated(object sender, EventArgs e)
    {
        if (_isClickThroughEnabled)
        {
            // 窗口激活时暂时移除穿透，让用户可操作
            SetWSExTransparent(false);
            UpdateStatusText("点击穿透：暂时关闭（窗口激活中 · Ctrl+Shift+T 切换）");
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_isClickThroughEnabled)
        {
            SetWSExTransparent(true);
            UpdateStatusText("点击穿透：已开启（Ctrl+Shift+T 关闭）");
        }
    }

    // ============ 标题栏拖动 ============

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    /// <summary>
    /// 阻止控制面板区域的点击触发窗口拖动
    /// </summary>
    private void ControlPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // 阻止事件冒泡到标题栏
    }

    // ============ 关闭按钮 ============

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ============ 隐藏UI（专注模式） ============

    private void HideUIToggle_Changed(object sender, RoutedEventArgs e)
    {
        _isUIHidden = HideUIToggle.IsChecked == true;
        ApplyUIVisibility();
    }

    private void ApplyUIVisibility()
    {
        bool show = !_isUIHidden;
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        var editorBg = show
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x31, 0x32, 0x44))
            : System.Windows.Media.Brushes.Transparent;

        if (TitleBarBorder != null)
            TitleBarBorder.Visibility = visibility;

        if (StatusBarBorder != null)
            StatusBarBorder.Visibility = visibility;

        if (MainBorder != null)
        {
            MainBorder.Background = show
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E))
                : System.Windows.Media.Brushes.Transparent;
            MainBorder.BorderThickness = show ? new System.Windows.Thickness(1) : new System.Windows.Thickness(0);
        }

        // 编辑区域背景
        if (ContentBorder != null)
            ContentBorder.Background = show
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x31, 0x32, 0x44))
                : System.Windows.Media.Brushes.Transparent;

        if (MarkdownEditor != null)
            MarkdownEditor.Background = editorBg;

        if (MarkdownPreview != null)
            MarkdownPreview.Background = editorBg;

        // 控制面板：隐藏时强制隐藏，显示时由 SizeChanged 管理
        if (!show && ControlPanelBorder != null)
            ControlPanelBorder.Visibility = Visibility.Collapsed;

        // 恢复时重新触发按钮渐隐布局
        if (show)
            MainWindow_SizeChanged(this, null!);
    }

    // ============ 点击穿透 ============

    /// <summary>
    /// 切换穿透（供热键 Ctrl+Shift+T 调用）
    /// 通过翻转按钮状态触发 Checked/Unchecked 事件，统一处理
    /// </summary>
    private void ToggleClickThrough()
    {
        // 穿透按钮被禁用时（普通模式），忽略快捷键
        if (!ClickThroughToggle.IsEnabled) return;
        ClickThroughToggle.IsChecked = !ClickThroughToggle.IsChecked;
    }

    /// <summary>
    /// 开启穿透 — 立即生效（仅置顶模式下可用）
    /// </summary>
    private void ClickThroughToggle_Checked(object sender, RoutedEventArgs e)
    {
        // 普通模式下拒绝穿透请求
        if (!Topmost)
        {
            ClickThroughToggle.IsChecked = false;
            return;
        }
        _isClickThroughEnabled = true;
        SetWSExTransparent(true);
        UpdateStatusText("点击穿透：已开启（Ctrl+Shift+T 关闭）");
    }

    /// <summary>
    /// 关闭穿透 — 立即生效
    /// </summary>
    private void ClickThroughToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _isClickThroughEnabled = false;
        SetWSExTransparent(false);
        UpdateStatusText("点击穿透：已关闭");
    }

    private void SetWSExTransparent(bool enable)
    {
        if (_hwnd == IntPtr.Zero) return;

        var exStyle = (uint)GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();

        if (enable)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;

        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr((long)exStyle));
    }

    // ============ 窗口置顶 ============

    private void TopmostToggle_Checked(object sender, RoutedEventArgs e)
    {
        Topmost = true;
        ClickThroughToggle.IsEnabled = true;
    }

    private void TopmostToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        Topmost = false;
        // 普通模式：强制关闭穿透并禁用按钮
        if (_isClickThroughEnabled)
        {
            ClickThroughToggle.IsChecked = false;
        }
        ClickThroughToggle.IsEnabled = false;
    }

    // ============ Markdown ============

    private void MdToggle_Checked(object sender, RoutedEventArgs e)
    {
        // 按钮事件：状态已由 ToggleButton 自动同步
        _isMarkdownMode = true;
        ApplyMarkdownMode();
    }

    private void MdToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _isMarkdownMode = false;
        ApplyMarkdownMode();
    }

    /// <summary>供热键 Ctrl+Shift+M 调用，同时同步按钮</summary>
    private void ToggleMarkdownMode()
    {
        _isMarkdownMode = !_isMarkdownMode;
        MdToggle.IsChecked = _isMarkdownMode; // 不触发事件循环，因为 _isMarkdownMode 已更新
        ApplyMarkdownMode();
    }

    private void ApplyMarkdownMode()
    {
        if (_isMarkdownMode)
        {
            MarkdownEditor.Visibility = Visibility.Collapsed;
            MarkdownPreview.Visibility = Visibility.Visible;
            MdStatusText.Text = "Markdown 预览";
            RefreshPreview();
        }
        else
        {
            MarkdownEditor.Visibility = Visibility.Visible;
            MarkdownPreview.Visibility = Visibility.Collapsed;
            MdStatusText.Text = "编辑模式";
        }
    }

    private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isMarkdownMode)
            RefreshPreview();
    }

    private void RefreshPreview()
    {
        MarkdownPreview.Document = MarkdownParser.Parse(MarkdownEditor.Text);
    }

    // ============ 辅助 ============

    private void UpdateStatusText(string message)
    {
        if (StatusText != null)
            StatusText.Text = message;
    }
}