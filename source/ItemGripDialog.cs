using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed class GripPicker : Control
    {
        readonly Bitmap image;
        public Point Grip { get; private set; }
        public event EventHandler Changed;

        public GripPicker(CustomItem item)
        {
            image = SkinMap.ToBitmap(item.Pixels, CustomItem.Size, CustomItem.Size);
            Grip = new Point(item.GripX, item.GripY);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Dock = DockStyle.Fill; TabStop = true; Cursor = Cursors.Cross; BackColor = Theme.Paper;
            AccessibleName = "物品握点，点击像素或用方向键移动";
        }
        Rectangle GridBox()
        {
            int cell = Math.Max(0, (Math.Min(ClientSize.Width, ClientSize.Height) - 12) / CustomItem.Size);
            int size = cell * CustomItem.Size;
            return new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        }
        public void SelectGrip(int x, int y)
        {
            var point = new Point(Math.Max(0, Math.Min(15, x)), Math.Max(0, Math.Min(15, y)));
            if (point == Grip) return;
            Grip = point; Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            Rectangle box = GridBox();
            if (box.Contains(e.Location)) SelectGrip((e.X - box.X) * 16 / box.Width, (e.Y - box.Y) * 16 / box.Height);
        }
        protected override bool IsInputKey(Keys keys)
        {
            Keys key = keys & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keys);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode) {
                case Keys.Left: SelectGrip(Grip.X - 1, Grip.Y); break;
                case Keys.Right: SelectGrip(Grip.X + 1, Grip.Y); break;
                case Keys.Up: SelectGrip(Grip.X, Grip.Y - 1); break;
                case Keys.Down: SelectGrip(Grip.X, Grip.Y + 1); break;
                default: return;
            }
            e.Handled = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle box = GridBox();
            int cell = box.Width / 16;
            if (cell < 1) return;
            Graphics g = e.Graphics;
            using (var light = new SolidBrush(Color.FromArgb(243, 244, 237)))
            using (var dark = new SolidBrush(Color.FromArgb(203, 214, 205)))
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    g.FillRectangle(((x + y) % 2 == 0) ? light : dark, box.X + x * cell, box.Y + y * cell, cell, cell);
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, box, 0, 0, 16, 16, GraphicsUnit.Pixel);
            Rectangle mark = new Rectangle(box.X + Grip.X * cell, box.Y + Grip.Y * cell, cell - 1, cell - 1);
            using (var pen = new Pen(Color.Black, 3)) g.DrawRectangle(pen, mark);
            using (var pen = new Pen(Theme.Gold, 1)) g.DrawRectangle(pen, mark);
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(1, 1, Width - 2, Height - 2));
        }
        protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class ItemGripDialog : Form
    {
        readonly CustomItem original;
        readonly Raster raster = new Raster();
        readonly GripPicker picker;
        readonly Control handPreview;
        readonly NumericUpDown angle;
        readonly ComboBox pose;
        readonly TextBox itemName;
        readonly Label coordinates;
        Rectangle viewport;
        public CustomItem Result { get; private set; }

        static Label Caption(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        }
        public ItemGripDialog(CustomItem item, Bitmap atlas, Font font)
        {
            original = item; Text = "设置右手物品"; Font = font; BackColor = Theme.Card; ForeColor = Theme.Ink;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(630, 412); MinimumSize = new Size(480, 350);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = MaximizeBox = false; ShowInTaskbar = false;
            raster.SetAtlas(atlas);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            foreach (int height in new[] { 38, 32 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            Controls.Add(root);
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            itemName = new TextBox { Text = item.Name, MaxLength = CustomItem.MaxName, Dock = DockStyle.Fill, AccessibleName = "物品名称" };
            angle = new NumericUpDown { Minimum = -180, Maximum = 180, Increment = 5, Value = item.Angle,
                Dock = DockStyle.Fill, AccessibleName = "物品贴图顺时针角度" };
            fields.Controls.Add(Caption("名称"), 0, 0); fields.Controls.Add(itemName, 1, 0);
            fields.Controls.Add(Caption("旋转角度（°）"), 2, 0); fields.Controls.Add(angle, 3, 0);
            root.Controls.Add(fields, 0, 0); root.SetColumnSpan(fields, 2);
            root.Controls.Add(Caption("物品图 · 点击要握住的像素"), 0, 1);
            var previewHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            pose = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "右手预览姿势" };
            pose.Items.AddRange(new object[] { "待机", "挥右手", "下蹲" }); pose.SelectedIndex = 0;
            previewHeader.Controls.Add(Caption("右手预览"), 0, 0); previewHeader.Controls.Add(pose, 1, 0);
            root.Controls.Add(previewHeader, 1, 1);
            picker = new GripPicker(item);
            handPreview = new Panel { BackColor = Color.FromArgb(47, 67, 66), Dock = DockStyle.Fill, AccessibleName = "装备效果" };
            handPreview.Paint += delegate(object sender, PaintEventArgs e) {
                if (viewport.IsEmpty) return;
                Rectangle area = handPreview.ClientRectangle;
                float fit = Math.Min((float)area.Width / viewport.Width, (float)area.Height / viewport.Height);
                int w = (int)Math.Round(viewport.Width * fit), h = (int)Math.Round(viewport.Height * fit);
                if (w < 1 || h < 1) return;
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(raster.Image, new Rectangle((area.Width - w)/2, (area.Height - h)/2, w, h),
                    viewport, GraphicsUnit.Pixel);
            };
            handPreview.Resize += delegate { handPreview.Invalidate(); };
            root.Controls.Add(picker, 0, 2); root.Controls.Add(handPreview, 1, 2);
            coordinates = Caption(""); root.Controls.Add(coordinates, 0, 3);
            var restore = new Button { Text = "恢复标准握姿", AutoSize = true, Anchor = AnchorStyles.Left };
            restore.Click += delegate { picker.SelectGrip(ItemMesh.DefaultGripX, ItemMesh.DefaultGripY); angle.Value = 0; };
            root.Controls.Add(restore, 1, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var accept = new Button { Text = "保存并装备", AutoSize = true };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            accept.Click += delegate {
                try { Result = Design(itemName.Text); DialogResult = DialogResult.OK; }
                catch (System.IO.InvalidDataException ex) { coordinates.Text = ex.Message; itemName.Focus(); }
            };
            buttons.Controls.Add(accept); buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 4); root.SetColumnSpan(buttons, 2);
            AcceptButton = accept; CancelButton = cancel;
            picker.Changed += delegate { RefreshPose(); }; angle.ValueChanged += delegate { RefreshPose(); };
            pose.SelectedIndexChanged += delegate { RefreshPose(); };
            RefreshPose();
        }
        CustomItem Design(string name)
        {
            return new CustomItem(name, original.Pixels, picker.Grip.X, picker.Grip.Y, (int)angle.Value);
        }
        void RefreshPose()
        {
            var draft = Design(original.Name);
            int[] sprite = draft.Sprite();
            Pose stance = Pose.Standard(0, pose.SelectedIndex == 1 ? .33 : 0, 0, true, 0, 0, pose.SelectedIndex == 2 ? 1 : 0);
            raster.SetItem(sprite); raster.Draw(stance, 0);
            viewport = raster.ImageRegion(ItemMesh.ForSprite(sprite).HandBounds(stance, sprite));
            coordinates.Text = string.Format("16×16 像素 · 握点（{0}, {1}）", draft.GripX, draft.GripY);
            handPreview.Invalidate();
        }
        protected override void Dispose(bool disposing) { if (disposing) raster.Dispose(); base.Dispose(disposing); }
    }
}
