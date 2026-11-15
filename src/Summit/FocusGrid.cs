namespace Summit;

/// <summary>A 3×3 picker for the zoom focus point: center, edges and corners.</summary>
internal sealed class FocusGrid : Control
{
    private static readonly Color CellColor = Color.FromArgb(70, 70, 70);
    private static readonly Color ActiveColor = Color.FromArgb(0, 120, 215);

    private int _col = 1, _row = 1;

    public event EventHandler? FocusPointChanged;

    public FocusGrid()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleName = "Foco";
    }

    /// <summary>Focus point with each component in {0, 0.5, 1}.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public PointF FocusPoint
    {
        get => new(_col / 2f, _row / 2f);
        set => Select((int)MathF.Round(value.X * 2), (int)MathF.Round(value.Y * 2), raise: false);
    }

    private void Select(int col, int row, bool raise)
    {
        col = Math.Clamp(col, 0, 2);
        row = Math.Clamp(row, 0, 2);
        if (col == _col && row == _row) return;
        _col = col;
        _row = row;
        Invalidate();
        if (raise) FocusPointChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left && Width > 0 && Height > 0)
            Select(e.X * 3 / Width, e.Y * 3 / Height, raise: true);
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: Select(_col - 1, _row, true); break;
            case Keys.Right: Select(_col + 1, _row, true); break;
            case Keys.Up: Select(_col, _row - 1, true); break;
            case Keys.Down: Select(_col, _row + 1, true); break;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int gap = Math.Max(1, Width / 16);
        float cw = (Width - gap * 2) / 3f, ch = (Height - gap * 2) / 3f;
        using var cell = new SolidBrush(CellColor);
        using var active = new SolidBrush(ActiveColor);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                e.Graphics.FillRectangle(c == _col && r == _row ? active : cell,
                    c * (cw + gap), r * (ch + gap), cw, ch);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
