using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SkinLive
{
    internal static class Theme
    {
        public static readonly Color Paper = Color.FromArgb(245, 243, 235);
        public static readonly Color Card = Color.FromArgb(253, 252, 247);
        public static readonly Color Ink = Color.FromArgb(31, 55, 53);
        public static readonly Color Muted = Color.FromArgb(79, 102, 98);
        public static readonly Color Line = Color.FromArgb(211, 219, 209);
        public static readonly Color Accent = Color.FromArgb(34, 99, 86);
        public static readonly Color Gold = Color.FromArgb(248, 193, 98);
        public static readonly Color Error = Color.FromArgb(146, 59, 41);
    }

    internal static class SpriteView
    {
        public static Rectangle Paint(Graphics g, Raster raster, Rectangle area, bool bust)
        {
            Bitmap image=raster.Image;
            if (area.Width < 1 || area.Height < 1) return Rectangle.Empty;
            Rectangle source = bust ? new Rectangle(58, Math.Max(0, 202 - (int)(raster.LiftPixels*.95)), 332, 350) :
                new Rectangle(0, 0, Raster.Width, Raster.Height);
            float scale = Math.Min((float)area.Width / source.Width, (float)area.Height / source.Height);
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle dest = new Rectangle(area.Left + (area.Width - w) / 2,
                bust ? area.Bottom - h : area.Top + (area.Height - h) / 2, w, h);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, dest, source, GraphicsUnit.Pixel);
            return dest;
        }
    }

    internal sealed class Preview : Control
    {
        private readonly Raster raster;
        private readonly Font nameFont = new Font("Microsoft YaHei UI", 12, FontStyle.Bold);
        public bool Bust;
        public event Action JumpRequested;
        public event Action PunchRequested;
        private bool spaceDown;
        public bool SpaceHeld {get{return spaceDown;}}
        public Preview(Raster raster)
        {
            this.raster = raster;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Ink;
            TabStop = true;
            AccessibleName = "角色预览，左键挥动右手，空格跳跃";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            Graphics g = e.Graphics;
            using (LinearGradientBrush bg = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(25, 48, 53), Color.FromArgb(43, 82, 75), 65f))
                g.FillRectangle(bg, ClientRectangle);
            int cell = Math.Max(18, (int)(28 * g.DpiX / 96));
            using (Pen grid = new Pen(Color.FromArgb(16, 199, 232, 210)))
            {
                for (int x = Width / 2 % cell; x < Width; x += cell) g.DrawLine(grid, x, 0, x, Height);
                for (int y = Height % cell; y < Height; y += cell) g.DrawLine(grid, 0, y, Width, y);
            }
            SpriteView.Paint(g, raster, ClientRectangle, Bust);
            int pad = Math.Max(10, cell / 2);
            using (SolidBrush mark = new SolidBrush(Theme.Gold)) g.FillRectangle(mark, pad, pad + 2, 3, nameFont.Height);
            TextRenderer.DrawText(g, "角色预览", nameFont, new Point(pad + 9, pad), Color.FromArgb(242, 237, 216),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 2, Width - 4, Height - 4));
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { Focus(); if (PunchRequested != null) PunchRequested(); }
        }
        protected override bool IsInputKey(Keys keyData)
        {
            return (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space)
            {
                if (!spaceDown && JumpRequested != null) JumpRequested();
                spaceDown = true; e.Handled = true;
            }
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) spaceDown = false;
            base.OnKeyUp(e);
        }
        protected override void OnLostFocus(EventArgs e) { spaceDown = false; base.OnLostFocus(e); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) nameFont.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class OutputWindow : Form
    {
        private readonly Raster raster;
        public bool Bust;
        public OutputWindow(Raster raster, Icon icon)
        {
            this.raster = raster;
            Text = "方块皮肤 · OBS 输出";
            Icon = icon;
            StartPosition = FormStartPosition.WindowsDefaultLocation;
            ClientSize = new Size(Raster.Width, Raster.Height);
            MinimumSize = new Size(160, 160);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        public void SetBackdrop(bool blue)
        {
            BackColor = blue ? Color.FromArgb(0, 0, 255) : Color.FromArgb(0, 255, 0);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            SpriteView.Paint(e.Graphics, raster, ClientRectangle, Bust);
        }
    }

    internal sealed class LevelMeter : Control
    {
        private int lit, threshold;
        public LevelMeter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = "麦克风音量及开口门限";
        }
        public void SetLevel(double db, int gate)
        {
            int value = (int)Math.Round(32 * Motion.Clamp((db + 72) / 72));
            int marker = (int)Math.Round(32 * Motion.Clamp((gate + 72.0) / 72.0));
            if (value == lit && marker == threshold) return;
            lit = value; threshold = marker; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float step = Width / 32f;
            using (SolidBrush on = new SolidBrush(Theme.Accent))
            using (SolidBrush off = new SolidBrush(Theme.Line))
            using (SolidBrush high = new SolidBrush(Theme.Gold))
            using (Pen mark = new Pen(Theme.Ink, 2))
            {
                for (int i = 0; i < 32; i++)
                    g.FillRectangle(i >= lit ? off : (i >= 26 ? high : on),
                        i * step, 3, Math.Max(1, step - 2), Math.Max(2, Height - 6));
                g.DrawLine(mark, threshold * step, 0, threshold * step, Height - 1);
            }
        }
    }
}
