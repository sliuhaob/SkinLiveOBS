using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed class Preferences
    {
        public int Gate = -48;
        public bool Keys = true, Bust = true, Blue, DesktopHidden = true;
        public LookMode Look = LookMode.Auto;
        public readonly ActionBindings Bindings = new ActionBindings();
        public string LastSkin = "";
        public string ItemId;
        public int ArmLayout;
        public string Device = "";
        private static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SkinLiveOBS", "preferences.txt"); }
        }
        public static Preferences Load()
        {
            Preferences prefs = new Preferences();
            try
            {
                if (!File.Exists(FilePath)) return prefs;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int split = line.IndexOf('=');
                    if (split < 0) continue;
                    string value = line.Substring(split + 1);
                    switch (line.Substring(0, split))
                    {
                        case "gate":
                            int n;
                            if (int.TryParse(value, out n)) prefs.Gate = Math.Max(-66, Math.Min(-24, n));
                            break;
                        case "keys": prefs.Keys = value == "1"; break;
                        case "bindings": prefs.Bindings.Load(value); break;
                        case "bust": prefs.Bust = value == "1"; break;
                        case "blue": prefs.Blue = value == "1"; break;
                        case "desktopHidden": prefs.DesktopHidden = value == "1"; break;
                        case "look":
                            LookMode mode;
                            if (Enum.TryParse(value, out mode) && Enum.IsDefined(typeof(LookMode), mode)) prefs.Look = mode;
                            break;
                        case "device": prefs.Device = value; break;
                        case "skin": prefs.LastSkin = value; break;
                        case "rightHand": prefs.ItemId = value; break;
                        case "layout": int index; if(int.TryParse(value,out index)) prefs.ArmLayout=Math.Max(0,Math.Min(2,index)); break;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return prefs;
        }
        public bool Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try {
                    File.WriteAllLines(temp, new string[] { "gate=" + Gate, "keys=" + (Keys ? "1" : "0"),
                        "bust=" + (Bust ? "1" : "0"), "blue=" + (Blue ? "1" : "0"), "device=" + Device, "skin=" + LastSkin, "layout=" + ArmLayout,
                        "rightHand=" + ItemId, "look=" + Look, "desktopHidden=" + (DesktopHidden ? "1" : "0"),
                        "bindings=" + Bindings.Serialize() },
                        new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else {
                        try { File.Move(temp, path); }
                        catch (IOException) { if (!File.Exists(path)) throw; File.Replace(temp, path, null); }
                    }
                } finally { if (File.Exists(temp)) File.Delete(temp); }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    internal sealed partial class Studio : Form
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Preferences prefs = Preferences.Load();
        private readonly Motion motion = new Motion();
        private readonly Raster raster = new Raster();
        private readonly GameInput gameInput = new GameInput();
        private readonly ActionState localActions, backgroundActions;
        private readonly HeadTracking tracking = new HeadTracking();
        private readonly Microphone microphone = new Microphone();
        private readonly Timer tick = new Timer();
        private readonly Font uiFont = new Font("Microsoft YaHei UI", 9f);
        private readonly Font headerFont = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        private readonly Font sectionFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        private readonly ToolTip tip = new ToolTip();
        private readonly Icon appIcon;
        private Preview preview;
        private OutputWindow output;
        private Button outputButton;
        private ComboBox deviceBox, shotBox, backgroundBox, lookBox;
        private CheckBox micOn, keysOn, hideOutput;
        private TrackBar gate;
        private Label gateLabel, status;
        private LevelMeter level;
        private bool ready, syncing;
        private double lastCleanup;
        private double Now { get { return clock.Elapsed.TotalSeconds; } }
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint kind);

        public Studio(string path)
        {
            localActions = new ActionState(prefs.Bindings);
            backgroundActions = new ActionState(prefs.Bindings);
            localActions.Started += StartAction;
            backgroundActions.Started += StartAction;
            gameInput.ButtonChanged += BackgroundButton;
            Text = "方块皮肤 · 直播控制台";
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = uiFont;
            BackColor = Theme.Paper; ForeColor = Theme.Ink;
            ClientSize = new Size(985, 720);
            MinimumSize = new Size(770, 500);
            StartPosition = FormStartPosition.CenterScreen;
            appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Icon = appIcon;
            BuildLayout();
            PrepareSkin(path);
            ReloadDevices();
            PopulateSavedSettings();
            tick.Interval = 15;
            tick.Tick += OnTick;
        }
        private Label LabelFor(string text, Font font, Color color)
        {
            return new Label { Text = text, Font = font, ForeColor = color,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false, AutoEllipsis = true, Margin = Padding.Empty };
        }
        private static Button ActionButton(string text, bool primary)
        {
            Button button = new Button {
                Text = text, UseMnemonic = false, AutoSize = true, MinimumSize = new Size(74, 30),
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(9, 1, 9, 1), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Theme.Accent : Theme.Card,
                ForeColor = primary ? Color.White : Theme.Ink, Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            button.FlatAppearance.BorderColor = primary ? Theme.Accent : Theme.Line;
            return button;
        }
        private static CheckBox Toggle(string text)
        {
            return new CheckBox { Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty,
                AutoSize = false, UseMnemonic = false, ForeColor = Theme.Ink };
        }
        private static ComboBox Picker(string name)
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
                AccessibleName = name, Margin = new Padding(0, 3, 0, 0),
                IntegralHeight = true, DropDownWidth = 245 };
        }
        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 5), ColumnCount = 2,
                RowCount = 3, Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 324));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            Controls.Add(root);

            root.Controls.Add(LabelFor("方块皮肤 · 直播控制台", headerFont, Theme.Ink), 0, 0);
            outputButton = ActionButton("打开直播窗口", true);
            outputButton.Anchor = AnchorStyles.Right;
            outputButton.Margin = new Padding(0, 0, 0, 0);
            outputButton.Click += delegate { ToggleOutput(); };
            var outputActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
                RowCount = 1, Margin = Padding.Empty };
            outputActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outputActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            outputActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            hideOutput = Toggle("桌面隐形");
            hideOutput.AccessibleName = "直播输出窗口在桌面隐形并穿透鼠标";
            tip.SetToolTip(hideOutput, "保留直播窗口供 WGC 窗口采集。取消勾选即可在桌面查看同一窗口。");
            hideOutput.CheckedChanged += delegate { if (ready && !syncing) ApplyOutputVisibility(); };
            outputActions.Controls.Add(hideOutput, 0, 0);
            outputActions.Controls.Add(outputButton, 1, 0);
            root.Controls.Add(outputActions, 1, 0);

            TableLayoutPanel scene = new TableLayoutPanel {
                Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 0, 12, 0)
            };
            scene.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            scene.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            preview = new Preview(raster, localActions) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            scene.Controls.Add(preview, 0, 0);
            scene.Controls.Add(ActionPreviewButtons(), 0, 1);
            root.Controls.Add(scene, 0, 1);
            root.Controls.Add(SettingsPanel(), 1, 1);

            status = LabelFor("准备就绪 · 麦克风已关闭", uiFont, Theme.Muted);
            status.AccessibleName = "运行状态";
            root.Controls.Add(status, 0, 2); root.SetColumnSpan(status, 2);
        }
        private static void Row(TableLayoutPanel table, Control control, int height)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(control, 0, row);
        }
        private static void Divider(TableLayoutPanel table)
        {
            Panel line = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Line, Margin = new Padding(0, 8, 0, 8) };
            Row(table, line, 17);
        }
        private static TableLayoutPanel Pair(int leftWidth)
        {
            TableLayoutPanel p = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, leftWidth));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return p;
        }
        private Control SettingsPanel()
        {
            Panel scroll = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, AutoScroll = true,
                BackColor = Theme.Card, Padding = new Padding(14, 9, 14, 8) };
            TableLayoutPanel table = new TableLayoutPanel { ColumnCount = 1, RowCount = 0,
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(table);

            SkinControls(table);
            Row(table, LabelFor("语音嘴型", sectionFont, Theme.Ink), 31);
            TableLayoutPanel deviceRow = new TableLayoutPanel { Dock = DockStyle.Fill,
                RowCount = 1, ColumnCount = 2, Margin = Padding.Empty };
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            deviceRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            deviceBox = Picker("选择麦克风");
            deviceBox.SelectedIndexChanged += delegate {
                if (syncing || !ready || deviceBox.SelectedItem == null) return;
                prefs.Device = ((MicDevice)deviceBox.SelectedItem).Name;
                if (micOn.Checked) ApplyMic();
            };
            deviceRow.Controls.Add(deviceBox, 0, 0);
            Button refresh = ActionButton("刷新", false);
            refresh.MinimumSize = Size.Empty; refresh.Padding = Padding.Empty;
            refresh.Margin = new Padding(5, 2, 0, 3); refresh.Dock = DockStyle.Fill;
            refresh.Click += delegate { ReloadDevices(); if (micOn.Checked) ApplyMic(); };
            deviceRow.Controls.Add(refresh, 1, 0);
            Row(table, deviceRow, 34);
            micOn = Toggle("启用麦克风");
            micOn.CheckedChanged += delegate { if (!syncing && ready) ApplyMic(); };
            Row(table, micOn, 31);
            level = new LevelMeter { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
            Row(table, level, 18);
            gateLabel = LabelFor("", uiFont, Theme.Muted); Row(table, gateLabel, 27);
            gate = new TrackBar { Dock = DockStyle.Fill, Minimum = -66, Maximum = -24,
                LargeChange = 6, SmallChange = 1, TickStyle = TickStyle.None, AutoSize = false,
                BackColor = Theme.Card, Margin = Padding.Empty, AccessibleName = "开口门限，分贝" };
            gate.ValueChanged += delegate { prefs.Gate = gate.Value; UpdateGate(); };
            Row(table, gate, 26);
            tip.SetToolTip(gate, "安静时也动嘴就往右调；说话时不动就往左调。黑线表示门限。");
            Row(table, LabelFor("误触往右调，轻声不开口往左调。", uiFont, Theme.Muted), 24);
            Divider(table);

            Row(table, LabelFor("游戏动作", sectionFont, Theme.Ink), 29);
            keysOn = Toggle("在其他窗口也响应动作按键");
            keysOn.CheckedChanged += delegate { if (ready && !syncing) ApplyGameInput(); };
            Row(table, keysOn, 29);
            AddBindingControls(table);
            var lookRow = Pair(64);
            lookBox = Picker("头部跟随模式");
            lookBox.Items.AddRange(new object[] { "自动（桌面 / FPS）", "桌面鼠标", "FPS 鼠标", "关闭" });
            lookBox.SelectedIndexChanged += delegate { if (ready && !syncing) ApplyGameInput(); };
            lookRow.Controls.Add(LabelFor("头部跟随", uiFont, Theme.Muted), 0, 0);
            lookRow.Controls.Add(lookBox, 1, 0); Row(table, lookRow, 35);
            tip.SetToolTip(lookBox, "桌面跟随指针位置。FPS 根据鼠标移动方向转头，松手后逐渐回正。");
            Divider(table);

            Row(table, LabelFor("直播画面", sectionFont, Theme.Ink), 30);
            shotBox = Picker("取景");
            shotBox.Items.AddRange(new object[] { "半身", "全身" });
            shotBox.SelectedIndexChanged += delegate {
                prefs.Bust = shotBox.SelectedIndex == 0;
                preview.Bust = prefs.Bust;
                if (output != null) { output.Bust = prefs.Bust; output.Invalidate(); }
            };
            TableLayoutPanel shot = Pair(55);
            shot.Controls.Add(LabelFor("取景", uiFont, Theme.Muted), 0, 0); shot.Controls.Add(shotBox, 1, 0);
            Row(table, shot, 33);
            backgroundBox = Picker("直播背景色");
            backgroundBox.Items.AddRange(new object[] { "绿幕", "蓝幕" });
            backgroundBox.SelectedIndexChanged += delegate {
                prefs.Blue = backgroundBox.SelectedIndex == 1;
                if (output != null) output.SetBackdrop(prefs.Blue);
            };
            TableLayoutPanel bg = Pair(55);
            bg.Controls.Add(LabelFor("背景", uiFont, Theme.Muted), 0, 0); bg.Controls.Add(backgroundBox, 1, 0);
            Row(table, bg, 33);
            return scroll;
        }
        private void PopulateSavedSettings()
        {
            keysOn.Checked = prefs.Keys;
            lookBox.SelectedIndex = (int)prefs.Look;
            hideOutput.Checked = prefs.DesktopHidden;
            gate.Value = prefs.Gate;
            UpdateGate();
            shotBox.SelectedIndex = prefs.Bust ? 0 : 1;
            backgroundBox.SelectedIndex = prefs.Blue ? 1 : 0;
        }
        private void UpdateGate()
        {
            gateLabel.Text = "开口门限   " + prefs.Gate + " dB";
            level.SetLevel(Microphone.Silence, prefs.Gate);
        }
        private void ReloadDevices()
        {
            List<MicDevice> devices = Microphone.Devices();
            int selected = 0;
            syncing = true;
            deviceBox.BeginUpdate();
            try
            {
                deviceBox.Items.Clear();
                foreach (MicDevice device in devices)
                {
                    int i = deviceBox.Items.Add(device);
                    if (device.Name == prefs.Device) selected = i;
                }
                deviceBox.SelectedIndex = selected;
            }
            finally { deviceBox.EndUpdate(); syncing = false; }
        }
        private void Message(string text, bool error)
        {
            status.ForeColor = error ? Theme.Error : Theme.Muted;
            status.Text = text;
            tip.SetToolTip(status, text);
        }
        private void ApplyMic()
        {
            if (!micOn.Checked)
            {
                microphone.Stop();
                Message("麦克风已关闭", false);
                return;
            }
            MicDevice selected = (MicDevice)deviceBox.SelectedItem;
            try
            {
                microphone.Start(selected.Id, Now);
                Message("麦克风已开启 · " + selected.Name, false);
            }
            catch (InvalidOperationException ex) { MicError(ex); }
        }
        private void MicError(Exception ex)
        {
            microphone.Stop();
            syncing = true;
            try { micOn.Checked = false; } finally { syncing = false; }
            Message("麦克风未启用 · " + ex.Message, true);
        }
        private void ApplyGameInput()
        {
            LookMode mode = (LookMode)lookBox.SelectedIndex;
            try
            {
                gameInput.Configure(Handle, keysOn.Checked, mode == LookMode.Auto || mode == LookMode.Fps);
                if (prefs.Keys != keysOn.Checked) { backgroundActions.Clear(); motion.ClearQueue(); }
                prefs.Keys = keysOn.Checked;
                tracking.SetMode(mode); prefs.Look = mode;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                syncing = true;
                try {
                    keysOn.Checked = prefs.Keys = gameInput.ActionsEnabled;
                    lookBox.SelectedIndex = (int)(prefs.Look = tracking.Mode);
                } finally { syncing = false; }
                Message("后台输入设置未生效 · " + ex.Message, true);
            }
        }
        private void ApplyOutputVisibility()
        {
            bool hidden = hideOutput.Checked;
            try {
                if (output != null) {
                    output.DesktopHidden = hidden;
                    output.Invalidate();
                    Message(hidden ? "输出窗口已在桌面隐形 · 可用 Windows 图形捕获采集" : "输出窗口已在桌面显示", false);
                }
                prefs.DesktopHidden = hidden;
            } catch (System.ComponentModel.Win32Exception ex) {
                if (output != null) {
                    try { output.DesktopHidden = prefs.DesktopHidden; }
                    catch (System.ComponentModel.Win32Exception) { output.Close(); }
                }
                syncing = true;
                try { hideOutput.Checked = prefs.DesktopHidden; } finally { syncing = false; }
                Message("切换桌面显示失败 · " + ex.Message, true);
            }
        }
        private void ToggleOutput()
        {
            if (output != null) { output.Close(); return; }
            output = new OutputWindow(raster, appIcon) { Bust = prefs.Bust };
            if (prefs.Bust) output.ClientSize = new Size(448, 452);
            output.SetBackdrop(prefs.Blue);
            output.FormClosed += delegate { output = null; outputButton.Text = "打开直播窗口"; };
            // A separate, unowned window remains available when the controller is minimized.
            try {
                output.DesktopHidden = prefs.DesktopHidden;
                output.Show();
            } catch (System.ComponentModel.Win32Exception ex) {
                OutputWindow failed = output; output = null;
                if (failed != null) failed.Dispose();
                Message("打开直播窗口失败 · " + ex.Message + " 可取消「桌面隐形」后重开。", true);
                return;
            }
            outputButton.Text = "关闭直播窗口";
            Message(prefs.DesktopHidden ? "直播窗口已打开 · 桌面隐形，可取消勾选查看" : "直播窗口已打开", false);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            if (Width > work.Width || Height > work.Height)
            {
                MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width), Math.Min(MinimumSize.Height, work.Height));
                Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
            }
            ready = true;
            ApplyGameInput();
            preview.Focus();
            OnTick(this, EventArgs.Empty);
            tick.Start();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == GameInput.WmInput && ready)
            {
                int dx, dy;
                gameInput.Read(m.LParam, out dx, out dy);
                if (dx != 0 || dy != 0) tracking.MoveMouse(dx, dy, Now);
            }
            base.WndProc(ref m);
        }
        private bool ControllerHasForeground()
        {
            IntPtr foreground=GetForegroundWindow();
            return foreground==Handle || GetAncestor(foreground,3)==Handle;
        }
        private void OnTick(object sender, EventArgs args)
        {
            double now = Now, db = Microphone.Silence;
            try { db = microphone.ReadLevel(now); }
            catch (InvalidOperationException ex) { MicError(ex); }
            if (microphone.Closing && now - lastCleanup > 1) { microphone.Stop(); lastCleanup = now; }
            level.SetLevel(db, prefs.Gate);
            if (preview.Focused && !preview.Capture && localActions.HasPressed)
                localActions.ReleaseMissingMouseButtons(Control.MouseButtons);
            bool background = keysOn.Checked && !editingBindings && !ControllerHasForeground();
            bool jumpHeld = localActions.Held(AvatarAction.Jump) || (background && backgroundActions.Held(AvatarAction.Jump));
            bool crouchHeld = previewCrouch || localActions.Held(AvatarAction.Crouch) || (background && backgroundActions.Held(AvatarAction.Crouch));
            motion.Tick(now, db, prefs.Gate, jumpHeld, crouchHeld);
            bool previewVisible = WindowState != FormWindowState.Minimized;
            bool outputVisible = output != null && output.WindowState != FormWindowState.Minimized;
            if (!previewVisible && !outputVisible) return;
            Point cursor = Point.Empty;
            Rectangle display = Rectangle.Empty;
            if (tracking.Mode == LookMode.Auto || tracking.Mode == LookMode.Desktop) {
                cursor = Cursor.Position;
                display = Screen.FromPoint(cursor).Bounds;
            }
            tracking.Update(now, cursor, display);
            raster.Draw(Pose.Standard(motion.Height,motion.Swing,now*20,equipped!=VanillaItems.Empty,tracking.X,tracking.Y,motion.Crouch),motion.Mouth);
            if (previewVisible) preview.Invalidate();
            if (outputVisible) {
                output.Invalidate();
                if (output.DesktopHidden) output.Update();
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            tick.Stop();
            ready = false;
            try { gameInput.Configure(IntPtr.Zero, false, false); } catch (System.ComponentModel.Win32Exception) { }
            if (output != null) output.Close();
            microphone.Stop();
            prefs.Save();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose(); microphone.Dispose(); tip.Dispose(); raster.Dispose();
            }
            base.Dispose(disposing);
            if (disposing)
            {
                uiFont.Dispose(); headerFont.Dispose(); sectionFont.Dispose();
                if (appIcon != null) appIcon.Dispose();
            }
        }
    }
}
