using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SkinLive
{
    internal enum Gesture { None, Jump, Punch }

    // Raw Input receives a second copy of hardware events while a game owns focus.
    // No hook, key suppression, synthetic input, or event history is used.
    internal sealed class GameInput
    {
        public const int WmInput = 0x00ff;
        private const uint InputSink = 0x0100, Remove = 1, RidInput = 0x10000003;
        private bool enabled, spaceDown;
        public bool SpaceHeld {get{return enabled && spaceDown;}}

        [StructLayout(LayoutKind.Sequential)]
        private struct Device { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header { public uint Type, Size; public IntPtr Device, WParam; }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct Body
        {
            [FieldOffset(2)] public ushort KeyFlags;
            [FieldOffset(4)] public ushort MouseButtons;
            [FieldOffset(6)] public ushort VirtualKey;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Packet { public Header Header; public Body Data; }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices([In] Device[] devices, uint count, uint size);
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr input, uint command, out Packet packet, ref uint size, uint headerSize);
        private static readonly uint DeviceSize = (uint)Marshal.SizeOf(typeof(Device));
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));
        private static readonly uint PacketSize = (uint)Marshal.SizeOf(typeof(Packet));

        public void SetEnabled(IntPtr window, bool on)
        {
            if (!on && !enabled) return;
            uint flags = on ? InputSink : Remove;
            IntPtr target = on ? window : IntPtr.Zero;
            Device[] devices = {
                new Device { Page = 1, Usage = 6, Flags = flags, Target = target },
                new Device { Page = 1, Usage = 2, Flags = flags, Target = target }
            };
            if (!RegisterRawInputDevices(devices, 2, DeviceSize)) throw new Win32Exception(Marshal.GetLastWin32Error());
            enabled = on;
            spaceDown = false;
        }
        public Gesture Read(IntPtr handle)
        {
            if (!enabled) return Gesture.None;
            Packet p;
            uint size = PacketSize;
            uint copied = GetRawInputData(handle, RidInput, out p, ref size, HeaderSize);
            if (copied == uint.MaxValue || copied < HeaderSize + 8) return Gesture.None;
            if (p.Header.Type == 0 && (p.Data.MouseButtons & 1) != 0) return Gesture.Punch;
            if (p.Header.Type == 1 && p.Data.VirtualKey == 0x20)
            {
                bool down = (p.Data.KeyFlags & 1) == 0;
                bool first = down && !spaceDown;
                spaceDown = down;
                if (first) return Gesture.Jump;
            }
            return Gesture.None;
        }
    }

    internal sealed class MicDevice
    {
        public readonly uint Id;
        public readonly string Name;
        public MicDevice(uint id, string name) { Id = id; Name = name; }
        public override string ToString() { return Name; }
    }

    internal sealed class Microphone : IDisposable
    {
        public const double Silence = -96;
        private const uint Done = 1, BadFormat = 32;
        private const int BufferCount = 8;
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(WaveHeader));
        private static readonly int FlagsOffset = (int)Marshal.OffsetOf(typeof(WaveHeader), "Flags");
        private static readonly int BytesOffset = (int)Marshal.OffsetOf(typeof(WaveHeader), "BytesRecorded");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Capabilities
        {
            public ushort Manufacturer, Product;
            public uint DriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Formats;
            public ushort Channels, Reserved;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormat
        {
            public ushort Tag, Channels;
            public uint Rate, BytesPerSecond;
            public ushort Align, Bits, Extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint Length, BytesRecorded;
            public UIntPtr User;
            public uint Flags, Loops;
            public IntPtr Next;
            public UIntPtr Reserved;
        }
        private sealed class AudioBuffer { public IntPtr Data, Header; public bool Prepared; }
        [DllImport("winmm.dll")] private static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint waveInGetDevCapsW(UIntPtr id, out Capabilities caps, uint size);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint waveInGetErrorTextW(uint result, StringBuilder text, uint length);
        [DllImport("winmm.dll")] private static extern uint waveInOpen(out IntPtr input, uint device,
            ref WaveFormat format, IntPtr callback, IntPtr context, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveInPrepareHeader(IntPtr input, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInUnprepareHeader(IntPtr input, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInAddBuffer(IntPtr input, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInStart(IntPtr input);
        [DllImport("winmm.dll")] private static extern uint waveInReset(IntPtr input);
        [DllImport("winmm.dll")] private static extern uint waveInClose(IntPtr input);

        private IntPtr input;
        private readonly List<AudioBuffer> buffers = new List<AudioBuffer>(BufferCount);
        private short[] samples;
        private int nextBuffer;
        private double lastReceived, lastDb = Silence;
        public bool Running { get; private set; }
        public bool Closing { get { return !Running && input != IntPtr.Zero; } }
        public static List<MicDevice> Devices()
        {
            List<MicDevice> list = new List<MicDevice>();
            list.Add(new MicDevice(uint.MaxValue, "系统默认录音设备"));
            uint count = waveInGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                Capabilities caps;
                if (waveInGetDevCapsW(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(Capabilities))) == 0)
                    list.Add(new MicDevice(i, caps.Name));
            }
            return list;
        }
        private static void Check(uint result)
        {
            if (result == 0) return;
            StringBuilder detail = new StringBuilder(200);
            waveInGetErrorTextW(result, detail, (uint)detail.Capacity);
            throw new InvalidOperationException("录音设备返回 " + result + " · " + detail);
        }
        public void Start(uint device, double now)
        {
            Stop();
            if (Closing) throw new InvalidOperationException("上一个设备正在释放，请稍后再启用。");
            uint status = BadFormat;
            int sampleCount = 0;
            foreach (ushort channels in new ushort[] { 1, 2 })
            {
                foreach (uint rate in new uint[] { 48000, 44100, 16000 })
                {
                    WaveFormat format = new WaveFormat {
                        Tag = 1, Channels = channels, Rate = rate, Bits = 16,
                        Align = (ushort)(2 * channels), BytesPerSecond = rate * 2u * channels
                    };
                    IntPtr opened;
                    status = waveInOpen(out opened, device, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                    if (status == 0)
                    {
                        input = opened;
                        sampleCount = (int)(rate / 50 * channels); // 20 ms per capture buffer
                        break;
                    }
                    if (status != BadFormat) Check(status);
                }
                if (status == 0) break;
            }
            Check(status);
            try
            {
                samples = new short[sampleCount];
                for (int i = 0; i < BufferCount; i++)
                {
                    AudioBuffer b = new AudioBuffer();
                    buffers.Add(b);
                    b.Data = Marshal.AllocHGlobal(sampleCount * sizeof(short));
                    b.Header = Marshal.AllocHGlobal((int)HeaderSize);
                    WaveHeader header = new WaveHeader { Data = b.Data, Length = (uint)(sampleCount * sizeof(short)) };
                    Marshal.StructureToPtr(header, b.Header, false);
                    Check(waveInPrepareHeader(input, b.Header, HeaderSize));
                    b.Prepared = true;
                    Check(waveInAddBuffer(input, b.Header, HeaderSize));
                }
                Check(waveInStart(input));
                nextBuffer = 0;
                lastReceived = now;
                lastDb = Silence;
                Running = true;
            }
            catch { Stop(); throw; }
        }
        public double ReadLevel(double now)
        {
            if (!Running) return Silence;
            double largest = Silence;
            bool received = false;
            for (int i = 0; i < buffers.Count; i++)
            {
                AudioBuffer b = buffers[nextBuffer];
                int flags = Marshal.ReadInt32(b.Header, FlagsOffset);
                if ((flags & Done) == 0) break;
                int count = Math.Min(samples.Length, Marshal.ReadInt32(b.Header, BytesOffset) / sizeof(short));
                if (count > 0)
                {
                    received = true;
                    Marshal.Copy(b.Data, samples, 0, count);
                    double energy = 0;
                    for (int n = 0; n < count; n++) energy += (double)samples[n] * samples[n];
                    double rms = Math.Sqrt(energy / count) / 32768.0;
                    largest = Math.Max(largest, 20 * Math.Log10(Math.Max(rms, .00001585)));
                }
                Marshal.WriteInt32(b.Header, BytesOffset, 0);
                Marshal.WriteInt32(b.Header, FlagsOffset, flags & ~(int)Done);
                Check(waveInAddBuffer(input, b.Header, HeaderSize));
                nextBuffer = (nextBuffer + 1) % buffers.Count;
            }
            if (received) { lastReceived = now; lastDb = largest; }
            if (now - lastReceived > 3.5) throw new InvalidOperationException("设备没有返回音频，请重新连接或改选麦克风。");
            return now - lastReceived < .16 ? lastDb : Silence;
        }
        public void Stop()
        {
            Running = false;
            if (input == IntPtr.Zero) return;
            waveInReset(input);
            foreach (AudioBuffer b in buffers)
                if (b.Prepared && waveInUnprepareHeader(input, b.Header, HeaderSize) == 0) b.Prepared = false;
            // A slow driver can temporarily retain a header. Retry on a later timer tick
            // and never free an address while the device still owns it.
            if (waveInClose(input) != 0) return;
            input = IntPtr.Zero;
            foreach (AudioBuffer b in buffers)
            {
                if (b.Header != IntPtr.Zero) Marshal.FreeHGlobal(b.Header);
                if (b.Data != IntPtr.Zero) Marshal.FreeHGlobal(b.Data);
            }
            buffers.Clear();
            samples = null;
            lastDb = Silence;
        }
        public void Dispose() { Stop(); }
    }
}
