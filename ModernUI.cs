using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SqlDeployerGui
{
    // Shared helpers + custom-painted modern controls (rounded corners, hover/focus
    // states) so the WinForms app gets a Fluent-ish look without a WPF migration.
    internal static class Gfx
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Flat, rounded, modern button with hover/press states and an optional outline.
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderThickness { get; set; } = 0;
        public Color HoverColor { get; set; } = Color.Empty;
        public Color PressColor { get; set; } = Color.Empty;
        public Color DisabledBackColor { get; set; } = Color.FromArgb(50, 50, 53);
        public Color DisabledForeColor { get; set; } = Color.FromArgb(120, 120, 124);

        private bool _hover;
        private bool _press;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.FromArgb(45, 45, 48);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnBackColorChanged(EventArgs e) { Invalidate(); base.OnBackColorChanged(e); }
        protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.FromArgb(28, 28, 30));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill = BackColor;
            if (!Enabled) fill = DisabledBackColor;
            else if (_press && PressColor != Color.Empty) fill = PressColor;
            else if (_hover && HoverColor != Color.Empty) fill = HoverColor;

            using (var path = Gfx.RoundedRect(rect, CornerRadius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (BorderThickness > 0 && BorderColor != Color.Transparent && Enabled)
                {
                    using var pen = new Pen(BorderColor, BorderThickness);
                    g.DrawPath(pen, path);
                }
            }

            Color textColor = Enabled ? ForeColor : DisabledForeColor;
            TextRenderer.DrawText(g, Text, Font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Rounded surface used for cards / containers.
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 14;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderThickness { get; set; } = 0;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(42, 42, 44);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.FromArgb(28, 28, 30));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Gfx.RoundedRect(rect, CornerRadius);
            using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
            if (BorderThickness > 0 && BorderColor != Color.Transparent)
            {
                using var pen = new Pen(BorderColor, BorderThickness);
                g.DrawPath(pen, path);
            }
        }
    }

    // Rounded input field: a borderless TextBox hosted inside a rounded panel,
    // with a focus ring. Exposes the TextBox members the app already uses.
    public class RoundedTextBox : Panel
    {
        private readonly TextBox _inner = new TextBox();

        public int CornerRadius { get; set; } = 10;
        public Color IdleBorderColor { get; set; } = Color.FromArgb(58, 58, 60);
        public Color FocusBorderColor { get; set; } = Color.FromArgb(94, 151, 214);
        public Color EyeColor { get; set; } = Color.FromArgb(150, 150, 154);

        private bool _focused;
        private bool _revealed;
        private bool _showToggle;
        private bool _eyeHover;
        private Rectangle _eyeRect;

        public RoundedTextBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(36, 36, 38);
            Size = new Size(420, 40);

            _inner.BorderStyle = BorderStyle.None;
            _inner.AutoSize = false;
            _inner.BackColor = BackColor;
            _inner.ForeColor = Color.FromArgb(236, 236, 236);
            _inner.Font = new Font("Segoe UI", 10.5F);
            _inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
            _inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };

            Controls.Add(_inner);
            LayoutInner();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); LayoutInner(); }

        // Shows the eye toggle and starts the field masked.
        public bool EnablePasswordToggle
        {
            get => _showToggle;
            set
            {
                _showToggle = value;
                if (value)
                {
                    _inner.UseSystemPasswordChar = true;
                    _revealed = false;
                }
                LayoutInner();
                Invalidate();
            }
        }

        private void TogglePasswordReveal()
        {
            _revealed = !_revealed;
            _inner.UseSystemPasswordChar = !_revealed;
            _inner.Focus();
            Invalidate();
        }

        private void LayoutInner()
        {
            int pad = 14;
            int rightReserve = _showToggle ? 42 : pad;

            int ih = _inner.PreferredHeight;
            _inner.Height = ih;
            _inner.Left = pad;
            _inner.Width = Math.Max(10, Width - pad - rightReserve);
            _inner.Top = Math.Max(0, (Height - ih) / 2);

            _eyeRect = new Rectangle(Width - 38, (Height - 24) / 2, 26, 24);
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutInner(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_showToggle && _eyeRect.Contains(e.Location))
                TogglePasswordReveal();
            else
                _inner.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = _showToggle && _eyeRect.Contains(e.Location);
            if (over != _eyeHover)
            {
                _eyeHover = over;
                Cursor = over ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_eyeHover) { _eyeHover = false; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.FromArgb(28, 28, 30));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.RoundedRect(rect, CornerRadius))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using var pen = new Pen(_focused ? FocusBorderColor : IdleBorderColor, _focused ? 2 : 1);
                g.DrawPath(pen, path);
            }

            if (_showToggle)
                DrawEye(g, _eyeRect, _eyeHover ? FocusBorderColor : EyeColor, _revealed);
        }

        // Vector eye icon (no font dependency): almond outline + pupil, with a slash when revealed.
        private static void DrawEye(Graphics g, Rectangle r, Color color, bool revealed)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            using var pen = new Pen(color, 1.6f);
            using var brush = new SolidBrush(color);

            var eye = new Rectangle(cx - 10, cy - 6, 20, 12);
            g.DrawEllipse(pen, eye);
            g.FillEllipse(brush, cx - 3, cy - 3, 6, 6);

            if (revealed)
                g.DrawLine(pen, cx - 11, cy + 7, cx + 11, cy - 7);
        }

        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
        }

        public string PlaceholderText
        {
            get => _inner.PlaceholderText;
            set => _inner.PlaceholderText = value;
        }

        public bool UseSystemPasswordChar
        {
            get => _inner.UseSystemPasswordChar;
            set => _inner.UseSystemPasswordChar = value;
        }

        public Color TextColor
        {
            get => _inner.ForeColor;
            set => _inner.ForeColor = value;
        }

        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                if (_inner != null) _inner.BackColor = value;
                Invalidate();
            }
        }
    }

    // Two named palettes the form switches between at runtime.
    public class ThemePalette
    {
        public bool IsDark;
        public Color Page, Surface, Card, Line, TextPrimary, TextMuted, InputBg;
        public Color Primary, PrimaryHover, PrimaryPress, OnPrimary;
        public Color Danger, DangerHover, Success, ProgressTrack;
        public Color DisabledBg, DisabledText;

        public static ThemePalette Dark => new ThemePalette
        {
            IsDark = true,
            Page = Color.FromArgb(28, 28, 30),
            Surface = Color.FromArgb(45, 45, 48),
            Card = Color.FromArgb(36, 36, 38),
            Line = Color.FromArgb(58, 58, 60),
            TextPrimary = Color.FromArgb(236, 236, 236),
            TextMuted = Color.FromArgb(150, 150, 154),
            InputBg = Color.FromArgb(36, 36, 38),
            Primary = Color.FromArgb(94, 151, 214),
            PrimaryHover = Color.FromArgb(116, 168, 224),
            PrimaryPress = Color.FromArgb(79, 132, 196),
            OnPrimary = Color.White,
            Danger = Color.FromArgb(232, 106, 102),
            DangerHover = Color.FromArgb(52, 32, 32),
            Success = Color.FromArgb(120, 200, 150),
            ProgressTrack = Color.FromArgb(45, 45, 48),
            DisabledBg = Color.FromArgb(48, 48, 51),
            DisabledText = Color.FromArgb(110, 110, 114),
        };

        public static ThemePalette Light => new ThemePalette
        {
            IsDark = false,
            Page = Color.FromArgb(243, 243, 245),
            Surface = Color.FromArgb(255, 255, 255),
            Card = Color.FromArgb(255, 255, 255),
            Line = Color.FromArgb(214, 216, 220),
            TextPrimary = Color.FromArgb(28, 28, 30),
            TextMuted = Color.FromArgb(110, 112, 118),
            InputBg = Color.FromArgb(255, 255, 255),
            Primary = Color.FromArgb(56, 120, 190),
            PrimaryHover = Color.FromArgb(78, 142, 210),
            PrimaryPress = Color.FromArgb(44, 100, 168),
            OnPrimary = Color.White,
            Danger = Color.FromArgb(206, 70, 66),
            DangerHover = Color.FromArgb(250, 234, 234),
            Success = Color.FromArgb(40, 140, 90),
            ProgressTrack = Color.FromArgb(224, 226, 230),
            DisabledBg = Color.FromArgb(228, 228, 231),
            DisabledText = Color.FromArgb(168, 168, 172),
        };
    }
}
