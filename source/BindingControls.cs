using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed partial class Studio
    {
        readonly Button[] bindingButtons = new Button[ActionBindings.Actions.Length];
        bool editingBindings, previewCrouch;

        void StartAction(AvatarAction action) { motion.Start(action, Now); }
        void BackgroundButton(int key, bool down)
        {
            if (prefs.Bindings.Find(key) == AvatarAction.None || editingBindings) return;
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
            previewCrouch = false;
        }
        Control ActionPreviewButtons()
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4,
                Padding = new Padding(0, 6, 0, 2), Margin = Padding.Empty };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            string[] labels = { "跳跃", "挥右手", "挥左手", "按住下蹲" };
            foreach (AvatarAction action in ActionBindings.Actions) {
                int i = (int)action;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                Button b = ActionButton(labels[i], false);
                b.Dock = DockStyle.Fill; b.AutoSize = false; b.MinimumSize = Size.Empty;
                b.Padding = Padding.Empty; b.Margin = new Padding(0, 0, i == 3 ? 0 : 5, 0);
                row.Controls.Add(b, i, 0);
                if (action != AvatarAction.Crouch) {
                    AvatarAction selected = action;
                    b.Click += delegate { StartAction(selected); };
                } else {
                    b.MouseDown += delegate(object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) previewCrouch = true; };
                    b.MouseUp += delegate(object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) previewCrouch = false; };
                    b.MouseCaptureChanged += delegate { if (!b.Capture) previewCrouch = false; };
                    b.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Space) previewCrouch = true; };
                    b.KeyUp += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Space) previewCrouch = false; };
                    b.LostFocus += delegate { previewCrouch = false; };
                }
            }
            return row;
        }
        void AddBindingControls(TableLayoutPanel table)
        {
            foreach (AvatarAction action in ActionBindings.Actions) {
                var row = Pair(108);
                var b = ActionButton("", false);
                b.Dock = DockStyle.Fill; b.AutoSize = false; b.MinimumSize = Size.Empty;
                b.Padding = Padding.Empty; b.Margin = new Padding(0, 2, 0, 2);
                b.AccessibleName = "设置" + ActionBindings.Name(action) + "按键";
                tip.SetToolTip(b, "点击后按下新键。选择其他动作的按键会交换两项绑定。");
                AvatarAction selected = action;
                b.Click += delegate { EditBinding(selected); };
                row.Controls.Add(LabelFor(ActionBindings.Name(action), uiFont, Theme.Ink), 0, 0);
                row.Controls.Add(b, 1, 0); bindingButtons[(int)action] = b;
                Row(table, row, 32);
            }
            var reset = ActionButton("恢复默认按键", false);
            reset.Anchor = AnchorStyles.Right; reset.Margin = new Padding(0, 2, 0, 3);
            reset.Click += delegate { prefs.Bindings.Reset(); SaveBindings("已恢复四个动作的默认按键"); };
            Row(table, reset, 37);
            ShowBindings();
        }
        void ShowBindings()
        {
            foreach (AvatarAction action in ActionBindings.Actions)
                bindingButtons[(int)action].Text = ActionBindings.KeyName(prefs.Bindings.KeyFor(action));
        }
        void SaveBindings(string statusText)
        {
            localActions.Clear(); backgroundActions.Clear();
            ShowBindings();
            bool saved = prefs.Save();
            Message(saved ? statusText : "本次改键已生效，设置文件暂时无法保存。", !saved);
        }
        void EditBinding(AvatarAction action)
        {
            localActions.Clear(); backgroundActions.Clear(); previewCrouch = false;
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
