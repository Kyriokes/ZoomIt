using Microsoft.Win32;
using Summit.Capture;
using Summit.Core;

namespace Summit;

internal sealed class MainForm : Form
{
    private static readonly Color BarColor = Color.FromArgb(32, 32, 32);
    private static readonly Color FieldColor = Color.FromArgb(51, 51, 51);
    private static readonly Color TextColor = Color.FromArgb(235, 235, 235);
    private static readonly Color DimTextColor = Color.FromArgb(150, 150, 150);

    private sealed record FpsOption(int Fps)
    {
        public override string ToString() => Fps == 0 ? "Máx" : Fps.ToString();
    }

    /// <summary>The drawing surface: Direct3D owns every pixel, so Windows must not paint it.</summary>
    private sealed class ViewSurface : Control
    {
        public event EventHandler? RedrawNeeded;

        public ViewSurface()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override void OnPaint(PaintEventArgs e) => RedrawNeeded?.Invoke(this, EventArgs.Empty);
    }

    private readonly Settings _settings;
    private readonly string _settingsPath;

    private readonly ComboBox _monitorBox = new();
    private readonly TrackBar _zoomBar = new();
    private readonly Label _zoomLabel = new();
    private readonly FocusGrid _focusGrid = new();
    private readonly ComboBox _fpsBox = new();
    private readonly Label _rateLabel = new();
    private readonly ViewSurface _view = new();
    private readonly System.Windows.Forms.Timer _rateTimer = new() { Interval = 1000 };

    private Mirror? _mirror;
    private int _lastFrameCount;
    private bool _loading;

    public MainForm(Settings settings, string settingsPath)
    {
        _settings = settings;
        _settingsPath = settingsPath;

        Text = "Summit";
        BackColor = Color.Black;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(640, 360);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildControls();
        PlaceOnStartupScreen();

        _rateTimer.Tick += (_, _) => UpdateRate();
    }

    // ---- Layout ----

    private void BuildControls()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = BarColor,
            Padding = new Padding(8, 5, 8, 5),
        };

        StyleCombo(_monitorBox, 230);
        _monitorBox.AccessibleName = "Monitor";

        _zoomBar.Minimum = (int)(ViewMath.MinZoom * 10);
        _zoomBar.Maximum = (int)(ViewMath.MaxZoom * 10);
        _zoomBar.SmallChange = 1;
        _zoomBar.LargeChange = 5;
        _zoomBar.TickStyle = TickStyle.None;
        _zoomBar.AutoSize = false;
        _zoomBar.Size = new Size(260, 26);
        _zoomBar.BackColor = BarColor;
        _zoomBar.Margin = new Padding(0, 1, 0, 0);
        _zoomBar.AccessibleName = "Zoom";

        _zoomLabel.AutoSize = false;
        _zoomLabel.Size = new Size(44, 26);
        _zoomLabel.TextAlign = ContentAlignment.MiddleLeft;
        _zoomLabel.Margin = new Padding(0, 0, 18, 0);

        _focusGrid.Size = new Size(26, 26);
        _focusGrid.BackColor = BarColor;
        _focusGrid.Margin = new Padding(0, 1, 18, 0);

        StyleCombo(_fpsBox, 64);
        _fpsBox.AccessibleName = "FPS";
        foreach (int fps in Settings.FpsOptions) _fpsBox.Items.Add(new FpsOption(fps));

        _rateLabel.AutoSize = false;
        _rateLabel.Size = new Size(70, 26);
        _rateLabel.TextAlign = ContentAlignment.MiddleLeft;
        _rateLabel.ForeColor = DimTextColor;

        bar.Controls.AddRange(
        [
            Caption("Monitor"), _monitorBox,
            Caption("Zoom"), _zoomBar, _zoomLabel,
            Caption("Foco"), _focusGrid,
            Caption("FPS"), _fpsBox, _rateLabel,
        ]);

        _view.Dock = DockStyle.Fill;
        _view.Resize += (_, _) => _mirror?.Resize(_view.ClientSize.Width, _view.ClientSize.Height);
        _view.RedrawNeeded += (_, _) => _mirror?.Redraw();

        Controls.Add(_view);
        Controls.Add(bar);

        _monitorBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            // Only a choice made by hand is remembered; an automatic fallback (monitor unplugged)
            // must not replace it, so the chosen monitor comes back when it does.
            if (_monitorBox.SelectedItem is MonitorInfo chosen) _settings.Monitor = chosen.DeviceName;
            ApplyMonitor();
        };
        _zoomBar.ValueChanged += (_, _) => { UpdateZoomLabel(); if (!_loading) ApplyView(); };
        _focusGrid.FocusPointChanged += (_, _) => ApplyView();
        _fpsBox.SelectedIndexChanged += (_, _) => { if (!_loading) ApplyFps(); };
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(TextRenderer.MeasureText(text, new Font("Segoe UI", 9f)).Width + 4, 26),
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = DimTextColor,
        Margin = new Padding(0, 0, 4, 0),
    };

    private static void StyleCombo(ComboBox box, int width)
    {
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = FieldColor;
        box.ForeColor = TextColor;
        box.Width = width;
        box.Margin = new Padding(0, 2, 18, 0);
    }

    private void PlaceOnStartupScreen()
    {
        // Prefer the screen used last time; otherwise a secondary one, so the primary stays free.
        var screens = Screen.AllScreens;
        var screen = screens.FirstOrDefault(s => s.DeviceName == _settings.WindowScreen)
                     ?? screens.FirstOrDefault(s => !s.Primary)
                     ?? Screen.PrimaryScreen
                     ?? screens[0];

        var area = screen.WorkingArea;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(area.X + area.Width / 8, area.Y + area.Height / 8, area.Width * 3 / 4, area.Height * 3 / 4);
        WindowState = FormWindowState.Maximized;
    }

    // ---- Lifecycle ----

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Dark title bar, to match the control bar. Ignored on systems that don't support it.
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        int enabled = 1;
        _ = DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        _loading = true;
        _zoomBar.Value = Math.Clamp((int)MathF.Round(_settings.Zoom * 10), _zoomBar.Minimum, _zoomBar.Maximum);
        UpdateZoomLabel();
        _focusGrid.FocusPoint = new PointF(_settings.AnchorX, _settings.AnchorY);
        _fpsBox.SelectedIndex = Math.Max(0, Array.IndexOf(Settings.FpsOptions, _settings.Fps));
        _loading = false;

        try
        {
            _mirror = new Mirror(_view.Handle, _view.ClientSize.Width, _view.ClientSize.Height);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo iniciar la placa de video.\n\n" + ex.Message, "Summit",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        ApplyView();
        ApplyFps();
        RefreshMonitors();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _rateTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _rateTimer.Stop();

        if (_mirror is not null)
        {
            _settings.WindowScreen = Screen.FromControl(this).DeviceName;
            try { _settings.Save(_settingsPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing the preferences is not worth blocking the close.
            }
        }

        _mirror?.Dispose();
        _mirror = null;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(RefreshMonitors);
    }

    // ---- Controls → mirror ----

    private void RefreshMonitors()
    {
        var monitors = Monitors.All();
        string? choice = MonitorChoice.Pick(
            _settings.Monitor, monitors.Select(m => m.DeviceName).ToList(), Screen.FromControl(this).DeviceName);

        _loading = true;
        _monitorBox.Items.Clear();
        foreach (var m in monitors) _monitorBox.Items.Add(m);
        _monitorBox.SelectedItem = monitors.FirstOrDefault(m => m.DeviceName == choice);
        _loading = false;

        ApplyMonitor();
    }

    private void ApplyMonitor()
    {
        if (_monitorBox.SelectedItem is not MonitorInfo monitor)
        {
            _mirror?.SetMonitor(IntPtr.Zero);
            return;
        }

        try
        {
            _mirror?.SetMonitor(monitor.Handle);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo mostrar ese monitor.\n\n" + ex.Message, "Summit",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyView()
    {
        _settings.Zoom = _zoomBar.Value / 10f;
        _settings.AnchorX = _focusGrid.FocusPoint.X;
        _settings.AnchorY = _focusGrid.FocusPoint.Y;
        _mirror?.SetView(_settings.Zoom, _settings.AnchorX, _settings.AnchorY);
    }

    private void ApplyFps()
    {
        if (_fpsBox.SelectedItem is not FpsOption option) return;
        _settings.Fps = option.Fps;
        _mirror?.SetFps(option.Fps);
    }

    private void UpdateZoomLabel() => _zoomLabel.Text = $"{_zoomBar.Value / 10f:0.0}x";

    private void UpdateRate()
    {
        if (_mirror is null) return;
        int count = _mirror.FramesShown;
        _rateLabel.Text = $"{count - _lastFrameCount} fps";
        _lastFrameCount = count;
    }
}
