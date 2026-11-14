using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rap.Studio.Engine
{
    internal static class WinMM
    {
        public const int WAVE_MAPPER = -1;
        public const int WIM_DATA = 0x3C0;
        public const int CALLBACK_FUNCTION = 0x00030000;
        public const int WHDR_DONE = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WAVEINCAPS
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public int dwFormats;
            public short wChannels;
            public short wReserved1;
        }

        public delegate void WaveInProc(IntPtr hwi, int msg, IntPtr instance, IntPtr p1, IntPtr p2);

        [DllImport("winmm.dll")]
        public static extern int waveInGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int waveInGetDevCaps(IntPtr deviceId, out WAVEINCAPS caps, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInOpen(out IntPtr hwi, int deviceId, ref WAVEFORMATEX format,
            WaveInProc callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        public static extern int waveInPrepareHeader(IntPtr hwi, ref WAVEHDR hdr, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInUnprepareHeader(IntPtr hwi, ref WAVEHDR hdr, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInAddBuffer(IntPtr hwi, ref WAVEHDR hdr, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInStart(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInStop(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInReset(IntPtr hwi);

        [DllImport("winmm.dll")]
        public static extern int waveInClose(IntPtr hwi);
    }

    public class MicDeviceInfo
    {
        public int Index;
        public string Name;

        public override string ToString()
        {
            return Name;
        }
    }

    public static class MicDevices
    {
        public static List<MicDeviceInfo> List()
        {
            List<MicDeviceInfo> list = new List<MicDeviceInfo>();
            try
            {
                int n = WinMM.waveInGetNumDevs();
                for (int i = 0; i < n; i++)
                {
                    WinMM.WAVEINCAPS caps;
                    if (WinMM.waveInGetDevCaps((IntPtr)i, out caps, Marshal.SizeOf(typeof(WinMM.WAVEINCAPS))) == 0)
                    {
                        MicDeviceInfo info = new MicDeviceInfo();
                        info.Index = i;
                        info.Name = string.IsNullOrEmpty(caps.szPname) ? ("录音设备 " + (i + 1)) : caps.szPname.Trim();
                        list.Add(info);
                    }
                }
            }
            catch { }
            return list;
        }
    }

    /// <summary>麦克风录音（waveIn），20ms 一个小缓冲，实时回调单声道 16bit 数据。</summary>
    public class MicRecorder : IDisposable
    {
        private IntPtr _hwi = IntPtr.Zero;
        private WinMM.WaveInProc _proc;         // 必须存活，避免回调被回收
        private readonly List<IntPtr> _mem = new List<IntPtr>();
        private readonly List<WinMM.WAVEHDR> _hdrs = new List<WinMM.WAVEHDR>();
        private volatile bool _running;
        private int _bufferBytes;
        private short[] _carry = new short[0];
        private int _channels = 1;

        public int SampleRate { get; private set; }
        public float LastPeak { get; private set; }
        public string LastError;

        public event Action<short[]> Data;

        public bool Start(int deviceId, int sampleRate)
        {
            Stop();
            int[][] tries = new int[][] {
                new int[] { sampleRate, 1 },
                new int[] { 44100, 1 },
                new int[] { sampleRate, 2 },
                new int[] { 44100, 2 }
            };
            foreach (int[] t in tries)
            {
                if (TryOpen(deviceId, t[0], t[1])) return true;
            }
            if (LastError == null) LastError = "无法打开麦克风设备";
            return false;
        }

        private bool TryOpen(int deviceId, int rate, int channels)
        {
            try
            {
                WinMM.WAVEFORMATEX fmt = new WinMM.WAVEFORMATEX();
                fmt.wFormatTag = 1; // PCM
                fmt.nChannels = (short)channels;
                fmt.nSamplesPerSec = rate;
                fmt.wBitsPerSample = 16;
                fmt.nBlockAlign = (short)(channels * 2);
                fmt.nAvgBytesPerSec = rate * fmt.nBlockAlign;
                fmt.cbSize = 0;

                _channels = (short)channels;
                _bufferBytes = (fmt.nAvgBytesPerSec / 50); // 20ms
                if (_bufferBytes < 320) _bufferBytes = 320;
                if (_bufferBytes % fmt.nBlockAlign != 0) _bufferBytes += fmt.nBlockAlign - (_bufferBytes % fmt.nBlockAlign);

                _proc = new WinMM.WaveInProc(OnWaveIn);

                IntPtr h;
                int hr = WinMM.waveInOpen(out h, deviceId < 0 ? WinMM.WAVE_MAPPER : deviceId, ref fmt, _proc, IntPtr.Zero, WinMM.CALLBACK_FUNCTION);
                if (hr != 0)
                {
                    LastError = "打开录音设备失败（错误码 " + hr + "）";
                    return false;
                }
                _hwi = h;
                SampleRate = rate;

                _mem.Clear();
                _hdrs.Clear();
                for (int i = 0; i < 8; i++)
                {
                    IntPtr data = Marshal.AllocHGlobal(_bufferBytes);
                    for (int b = 0; b < _bufferBytes; b++) Marshal.WriteByte(data, b, 0);
                    WinMM.WAVEHDR hdr = new WinMM.WAVEHDR();
                    hdr.lpData = data;
                    hdr.dwBufferLength = _bufferBytes;
                    hdr.dwUser = (IntPtr)i;
                    WinMM.waveInPrepareHeader(_hwi, ref hdr, Marshal.SizeOf(typeof(WinMM.WAVEHDR)));
                    WinMM.waveInAddBuffer(_hwi, ref hdr, Marshal.SizeOf(typeof(WinMM.WAVEHDR)));
                    _mem.Add(data);
                    _hdrs.Add(hdr);
                }

                if (WinMM.waveInStart(_hwi) != 0)
                {
                    LastError = "无法启动录音";
                    Close();
                    return false;
                }
                _running = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Close();
                return false;
            }
        }

        private void OnWaveIn(IntPtr hwi, int msg, IntPtr instance, IntPtr p1, IntPtr p2)
        {
            if (msg != WinMM.WIM_DATA || !_running) return;
            try
            {
                WinMM.WAVEHDR hdr = (WinMM.WAVEHDR)Marshal.PtrToStructure(p1, typeof(WinMM.WAVEHDR));
                int bytes = hdr.dwBytesRecorded;
                short[] samples;
                if (_channels == 1)
                {
                    samples = new short[bytes / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = (short)Marshal.ReadInt16(hdr.lpData, i * 2);
                }
                else
                {
                    samples = new short[bytes / 4];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        short a = (short)Marshal.ReadInt16(hdr.lpData, i * 4);
                        short b = (short)Marshal.ReadInt16(hdr.lpData, i * 4 + 2);
                        samples[i] = (short)((a + b) / 2);
                    }
                }
                float peak = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Math.Abs((float)samples[i]) / 32768f;
                    if (v > peak) peak = v;
                }
                LastPeak = peak;
                Action<short[]> h = Data;
                if (h != null && samples.Length > 0) h(samples);

                WinMM.waveInAddBuffer(_hwi, ref hdr, Marshal.SizeOf(typeof(WinMM.WAVEHDR)));
            }
            catch { }
        }

        public void Stop()
        {
            _running = false;
            Close();
        }

        private void Close()
        {
            if (_hwi != IntPtr.Zero)
            {
                try { WinMM.waveInStop(_hwi); } catch { }
                try { WinMM.waveInReset(_hwi); } catch { }
                for (int i = 0; i < _hdrs.Count; i++)
                {
                    WinMM.WAVEHDR hdr = _hdrs[i];
                    try { WinMM.waveInUnprepareHeader(_hwi, ref hdr, Marshal.SizeOf(typeof(WinMM.WAVEHDR))); } catch { }
                }
                try { WinMM.waveInClose(_hwi); } catch { }
                _hwi = IntPtr.Zero;
            }
            for (int i = 0; i < _mem.Count; i++)
            {
                try { Marshal.FreeHGlobal(_mem[i]); } catch { }
            }
            _mem.Clear();
            _hdrs.Clear();
            _proc = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
