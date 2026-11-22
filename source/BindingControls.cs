using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed partial class Studio
    {
        readonly Button[] bindingButtons = new Button[(int)AvatarAction.RepeatRight + 1];
        bool editingBindings, previewCrouch, previewRightHeld;

        void StartAction(AvatarAction action) { motion.Start(action, Now); }
        void BackgroundButton(int key, bool down)
        {
            if (editingBindings || (prefs.Bindings.Find(key) == AvatarAction.None && !prefs.Bindings.RepeatsRight(key))) return;
            if (!down) {
                // A release may arrive outside the preview after it loses mouse capture.
                backgroundActions.Change(key, false);
                localActions.Change(key, false);
            } else if (!ControllerHasForeground()) backgroundActions.Change(key, true);
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (backgroundActions != null) backgroundActions.Clear();
        }
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (localActions != null) localActions.Clear();
            previewCrouch = previewRightHeld = false;
        }
        Control ActionPreviewButtons()
        {
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Padding = new Padding(0, 6, 0, 2), Margin = Padding.Empty };
            string[] labels = { "跳跃", "挥右手", "挥左手", "按住下蹲" };
            foreach (AvatarAction action in ActionBindings.Actions) {
                int i = (int)action;
                Button b = ActionButton(labels[i], false);
                b.MinimumSize = new Size(92, 30);
                b.Margin = new Padding(0, 0, i == labels.Length - 1 ? 0 : 5, 3);
                row.Controls.Add(b);
                if (action == AvatarAction.Crouch) BindPreviewHold(b, held => previewCrouch = held);
                else if (action == AvatarAction.RightSwing) BindRightPreview(b);
                else {
                    AvatarAction selected = action;
                    b.Click += delegate { StartAction(selected); };
                }
            }
            return row;
        }
        static void BindPreviewHold(Button button, Action<bool> changed)
        {
            bool mouseDown = false, spaceDown = false;
            Action<bool> mouse = down => { mouseDown = down; changed(mouseDown || spaceDown); };
            Action<bool> space = down => { spaceDown = down; changed(mouseDown || spaceDown); };
            button.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) mouse(true); };
            button.MouseUp += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) mouse(false); };
            button.MouseCaptureChanged += delegate { if (!button.Capture) mouse(false); };
            button.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space) space(true); };
            button.KeyUp += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space) space(false); };
            button.LostFocus += delegate { mouseDown = spaceDown = false; changed(false); };
        }
        void BindRightPreview(Button button)
        {
            bool handledPress = false;
            BindPreviewHold(button, delegate(bool down) {
                bool held = prefs.Bindings.RepeatRightEnabled && down;
                if (held && !previewRightHeld) {
                    handledPress = true;
                    StartAction(AvatarAction.RightSwing);
                }
                previewRightHeld = held;
                // WinForms can raise Click during the release message. Consume that click once.
                if (!held && handledPress && button.IsHandleCreated && !button.Disposing)
                    button.BeginInvoke((MethodInvoker)delegate { if (!previewRightHeld) handledPress = false; });
            });
            button.Click += delegate { if (!handledPress) StartAction(AvatarAction.RightSwing); };
        }
        void AddBindingButton(TableLayoutPanel table, AvatarAction action)
        {
            bool repeat = action == AvatarAction.RepeatRight;
            var b = ActionButton("", false);
            b.Dock = DockStyle.Fill; b.Margin = Padding.Empty;
            b.Padding = new Padding(3, 1, 3, 1);
            b.AccessibleName = "设置" + ActionBindings.Name(action) + "按键";
            tip.SetToolTip(b, repeat ? "可与右手挥动共用一个键，也可单独设置。" : "点击后按下新键。选择其他动作的按键会交换两项绑定。");
            b.Click += delegate { EditBinding(action); };
            bindingButtons[(int)action] = b;
            LabeledRow(table, repeat ? "连挥按键" : ActionBindings.Name(action), b, Theme.Ink);
        }
        void AddBindingControls(TableLayoutPanel table)
        {
            foreach (AvatarAction action in ActionBindings.Actions) AddBindingButton(table, action);
            var repeat = Toggle("长按连挥右手");
            repeat.Checked = prefs.Bindings.RepeatRightEnabled;
            repeat.CheckedChanged += delegate {
                prefs.Bindings.RepeatRightEnabled = repeat.Checked;
                SaveBindings(repeat.Checked ? "已开启长按连挥右手" : "已关闭长按连挥右手");
            };
            Row(table, repeat);
            AddBindingButton(table, AvatarAction.RepeatRight);
            var reset = ActionButton("恢复默认按键", false);
            reset.Anchor = AnchorStyles.Right; reset.Margin = new Padding(0, 2, 0, 3);
            reset.Click += delegate { prefs.Bindings.Reset(); SaveBindings("已恢复默认动作按键和连挥按键"); };
            Row(table, reset);
            ShowBindings();
        }
        void ShowBindings()
        {
            for (int i = 0; i < bindingButtons.Length; i++)
                bindingButtons[i].Text = ActionBindings.KeyName(prefs.Bindings.KeyFor((AvatarAction)i));
        }
        void SaveBindings(string statusText)
        {
            localActions.Clear(); backgroundActions.Clear();
            previewRightHeld = false;
            ShowBindings();
            bool saved = prefs.Save();
            Message(saved ? statusText : "本次设置已生效，暂时无法写入设置文件。", !saved);
        }
        void EditBinding(AvatarAction action)
        {
            localActions.Clear(); backgroundActions.Clear(); previewCrouch = previewRightHeld = false;
            editingBindings = true;
            try {
                using (var dialog = new BindingDialog(action, uiFont)) {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    AvatarAction other = prefs.Bindings.Assign(action, dialog.SelectedKey);
                    string text = ActionBindings.Name(action) + "已绑定到「" + ActionBindings.KeyName(dialog.SelectedKey) + "」";
                    if (other != AvatarAction.None && other != action)
                        text += "，" + ActionBindings.Name(other) + "已改为「" + ActionBindings.KeyName(prefs.Bindings.KeyFor(other)) + "」";
                    SaveBindings(text);
                }
            } finally { editingBindings = false; }
        }
    }
}
