using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace SkinLive
{
    internal enum AvatarAction { None = -1, Jump, RightSwing, LeftSwing, Crouch, RepeatRight }

    internal sealed class ActionBindings
    {
        public static readonly AvatarAction[] Actions = {
            AvatarAction.Jump, AvatarAction.RightSwing, AvatarAction.LeftSwing, AvatarAction.Crouch
        };
        internal static readonly MouseButtons[] MouseOrder = {
            MouseButtons.Left, MouseButtons.Right, MouseButtons.Middle, MouseButtons.XButton1, MouseButtons.XButton2
        };
        static readonly int[] Defaults = { (int)Keys.Space, (int)Keys.LButton, (int)Keys.RButton, (int)Keys.ControlKey };
        static readonly string[] Names = { "跳跃", "右手挥动", "左手挥动", "下蹲（按住）", "右手连挥" };
        const int DefaultRepeatKey = (int)Keys.LButton;
        readonly int[] codes = (int[])Defaults.Clone();
        int repeatKey = DefaultRepeatKey;
        public bool RepeatRightEnabled { get; set; }

        public static string Name(AvatarAction action) { return Names[(int)action]; }
        public int KeyFor(AvatarAction action) { return action == AvatarAction.RepeatRight ? repeatKey : codes[(int)action]; }
        public void Reset() { Array.Copy(Defaults, codes, codes.Length); repeatKey = DefaultRepeatKey; }

        public static int Canonical(int code)
        {
            switch ((Keys)code) {
                case Keys.LControlKey: case Keys.RControlKey: return (int)Keys.ControlKey;
                case Keys.LShiftKey: case Keys.RShiftKey: return (int)Keys.ShiftKey;
                case Keys.LMenu: case Keys.RMenu: return (int)Keys.Menu;
                default: return code;
            }
        }
        // Keep modifiers physically distinct until both sides are released.
        public static int KeyboardCode(int vk, int scan, bool extended)
        {
            switch ((Keys)vk) {
                case Keys.ControlKey: return extended ? (int)Keys.RControlKey : (int)Keys.LControlKey;
                case Keys.ShiftKey: return scan == 0x36 ? (int)Keys.RShiftKey : (int)Keys.LShiftKey;
                case Keys.Menu: return extended ? (int)Keys.RMenu : (int)Keys.LMenu;
                default: return vk;
            }
        }
        public static bool KeyboardMessage(ref Message msg, out int code, out bool down)
        {
            code = 0; down = msg.Msg == 0x0100 || msg.Msg == 0x0104;
            if (!down && msg.Msg != 0x0101 && msg.Msg != 0x0105) return false;
            long bits = msg.LParam.ToInt64();
            code = KeyboardCode((int)msg.WParam.ToInt64() & 255, (int)((bits >> 16) & 255), (bits & (1L << 24)) != 0);
            return true;
        }
        public static int MouseCode(MouseButtons button)
        {
            switch (button) {
                case MouseButtons.Left: return (int)Keys.LButton;
                case MouseButtons.Right: return (int)Keys.RButton;
                case MouseButtons.Middle: return (int)Keys.MButton;
                case MouseButtons.XButton1: return (int)Keys.XButton1;
                case MouseButtons.XButton2: return (int)Keys.XButton2;
                default: return 0;
            }
        }
        public static bool CanBind(int key)
        {
            return key == 1 || key == 2 || (key >= 4 && key <= 6) ||
                (key >= 8 && key < 255 && key != (int)Keys.Escape && key != (int)Keys.ProcessKey && key != (int)Keys.Packet);
        }
        public static string KeyName(int key)
        {
            switch ((Keys)Canonical(key)) {
                case Keys.LButton: return "鼠标左键";
                case Keys.RButton: return "鼠标右键";
                case Keys.MButton: return "鼠标中键";
                case Keys.XButton1: return "鼠标侧键 1";
                case Keys.XButton2: return "鼠标侧键 2";
                case Keys.ControlKey: return "Ctrl";
                case Keys.ShiftKey: return "Shift";
                case Keys.Menu: return "Alt";
                case Keys.Space: return "空格";
                case Keys.Return: return "Enter";
                case Keys.Back: return "Backspace";
                default:
                    if (key >= (int)Keys.D0 && key <= (int)Keys.D9) return ((char)key).ToString();
                    return ((Keys)key).ToString();
            }
        }
        public AvatarAction Find(int physicalCode)
        {
            return (AvatarAction)Array.IndexOf(codes, Canonical(physicalCode));
        }
        public bool RepeatsRight(int physicalCode)
        {
            return RepeatRightEnabled && Canonical(physicalCode) == repeatKey;
        }
        // Swapping allows users to exchange two defaults without clearing a third action.
        public AvatarAction Assign(AvatarAction action, int physicalCode)
        {
            int code = Canonical(physicalCode);
            if (!CanBind(code)) throw new ArgumentOutOfRangeException("physicalCode");
            // The hold gesture can share a primary binding, including the default left click.
            if (action == AvatarAction.RepeatRight) { repeatKey = code; return AvatarAction.None; }
            AvatarAction previous = Find(code);
            if (previous != AvatarAction.None) codes[(int)previous] = KeyFor(action);
            codes[(int)action] = code;
            return previous;
        }
        public string Serialize()
        {
            return string.Join(",", Array.ConvertAll(codes, k => k.ToString(CultureInfo.InvariantCulture)));
        }
        public void Load(string saved)
        {
            string[] fields = saved.Split(',');
            if (fields.Length != codes.Length) return;
            int[] parsed = new int[codes.Length];
            for (int i = 0; i < parsed.Length; i++) {
                int key;
                if (!int.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out key)) return;
                parsed[i] = Canonical(key);
                if (!CanBind(parsed[i]) || Array.IndexOf(parsed, parsed[i], 0, i) >= 0) return;
            }
            Array.Copy(parsed, codes, codes.Length);
        }
    }

    internal sealed class ActionState
    {
        readonly ActionBindings bindings;
        readonly HashSet<int> pressed = new HashSet<int>();
        public event Action<AvatarAction> Started;

        public ActionState(ActionBindings bindings) { this.bindings = bindings; }
        public void Clear() { pressed.Clear(); }
        public bool HasPressed { get { return pressed.Count != 0; } }
        public void ReleaseMissingMouseButtons(MouseButtons buttons)
        {
            foreach (MouseButtons button in ActionBindings.MouseOrder)
                if ((buttons & button) == 0) pressed.Remove(ActionBindings.MouseCode(button));
        }
        public bool Held(AvatarAction action)
        {
            return (action != AvatarAction.RepeatRight || bindings.RepeatRightEnabled) && HeldKey(bindings.KeyFor(action));
        }
        bool HeldKey(int wanted)
        {
            foreach (int key in pressed) if (ActionBindings.Canonical(key) == wanted) return true;
            return false;
        }
        public bool Change(int physicalCode, bool down)
        {
            AvatarAction action = bindings.Find(physicalCode);
            bool repeat = bindings.RepeatsRight(physicalCode);
            if (!down) return pressed.Remove(physicalCode) || action != AvatarAction.None || repeat;
            if (action == AvatarAction.None && !repeat) return false;
            bool wasHeld = HeldKey(ActionBindings.Canonical(physicalCode));
            if (pressed.Add(physicalCode) && !wasHeld && Started != null) {
                if (action != AvatarAction.None) Started(action);
                // A shared hand binding already began a swing. Let it finish before repeating.
                if (repeat && action != AvatarAction.RightSwing && action != AvatarAction.LeftSwing)
                    Started(AvatarAction.RightSwing);
            }
            return true;
        }
    }
}
