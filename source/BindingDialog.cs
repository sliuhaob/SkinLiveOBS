using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkinLive
{
    internal sealed class BindingDialog : Form
    {
        readonly Label instruction;
        int pending;
        public int SelectedKey { get; private set; }

        public BindingDialog(AvatarAction action, Font font)
        {
            Text = "设置按键 · " + ActionBindings.Name(action);
            Font = font; BackColor = Theme.Card; ForeColor = Theme.Ink; ImeMode = ImeMode.Disable;
            AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(370,124); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            MinimizeBox = MaximizeBox = false;
            instruction = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Text = "请按想绑定的键，或在此处点击鼠标键。\r\n按 Esc 取消。", UseMnemonic = false };
            Controls.Add(instruction);
            instruction.MouseDown += delegate(object sender, MouseEventArgs e) { SelectKey(ActionBindings.MouseCode(e.Button), true); };
            instruction.MouseUp += delegate(object sender, MouseEventArgs e) { SelectKey(ActionBindings.MouseCode(e.Button), false); };
        }
        void SelectKey(int key, bool down)
        {
            if (key == (int)Keys.Escape) { DialogResult = DialogResult.Cancel; return; }
            if (down && pending == 0 && ActionBindings.CanBind(key)) {
                pending = key;
                instruction.Text = "松开「" + ActionBindings.KeyName(key) + "」完成绑定。\r\n按 Esc 取消。";
            }
            if (!down && pending != 0 && key == pending) {
                SelectedKey = ActionBindings.Canonical(key); DialogResult = DialogResult.OK;
            }
        }
        bool CaptureKey(ref Message m)
        {
            int key; bool down;
            if (!ActionBindings.KeyboardMessage(ref m, out key, out down)) return false;
            SelectKey(key, down); return true;
        }
        public override bool PreProcessMessage(ref Message msg)
        {
            return CaptureKey(ref msg) || base.PreProcessMessage(ref msg);
        }
        protected override void WndProc(ref Message m)
        {
            if (!CaptureKey(ref m)) base.WndProc(ref m);
        }
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible && DialogResult == DialogResult.None) DialogResult = DialogResult.Cancel;
        }
    }
}
