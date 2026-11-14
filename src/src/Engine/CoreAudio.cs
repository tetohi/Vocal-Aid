using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rap.Studio.Engine
{
    /// <summary>Core Audio（WASAPI）互操作：设备峰值电平 + 系统回环采音。</summary>
    internal static class AudioInterop
    {
        public const int CLSCTX_ALL = 23;
        public const int eRender = 0;
        public const int eConsole = 0;
        public const int AUDCLNT_SHAREMODE_SHARED = 0;
        public const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        public const int AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
        public static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
        public static readonly Guid IID_IAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        public class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
            [PreserveSig]
            int GetDevice(string id, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig]
            int OpenPropertyStore(int access, out IntPtr store);
            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig]
            int GetState(out int state);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioMeterInformation
        {
            [PreserveSig]
            int GetPeakValue(out float peak);
            [PreserveSig]
            int GetMeteringChannelCount(out int count);
            [PreserveSig]
            int GetChannelsPeakValues(int count, out IntPtr peaks);
            [PreserveSig]
            int QueryHardwareSupport(out int mask);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioClient
        {
            [PreserveSig]
            int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
            [PreserveSig]
            int GetBufferSize(out uint frameCount);
            [PreserveSig]
            int GetStreamLatency(out long latency);
            [PreserveSig]
            int GetCurrentPadding(out uint frameCount);
            [PreserveSig]
            int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
            [PreserveSig]
            int GetMixFormat(out IntPtr format);
            [PreserveSig]
            int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
            [PreserveSig]
            int Start();
            [PreserveSig]
            int Stop();
            [PreserveSig]
            int Reset();
            [PreserveSig]
            int SetEventHandle(IntPtr hEvent);
            [PreserveSig]
            int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioCaptureClient
        {
            [PreserveSig]
            int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePos, out long qpcPos);
            [PreserveSig]
            int ReleaseBuffer(int frames);
            [PreserveSig]
            int GetNextPacketSize(out int frames);
        }

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

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr p);
    }

    /// <summary>默认播放设备的实时峰值（0~1），用于判断视频是否正在出声。</summary>
    public static class AudioPeak
    {
        private static AudioInterop.IAudioMeterInformation _meter;
        private static readonly object _lock = new object();
        private static int _failCount;

        public static float Read()
        {
            lock (_lock)
            {
                if (_meter == null)
                {
                    if (_failCount > 8) return 0f;
                    if (!Init()) { _failCount++; return 0f; }
                }
                try
                {
                    float v = 0f;
                    int hr = _meter.GetPeakValue(out v);
                    if (hr != 0) { _meter = null; return 0f; }
                    return v;
                }
                catch { _meter = null; return 0f; }
            }
        }

        private static bool Init()
        {
            try
            {
                AudioInterop.IMMDeviceEnumerator en = (AudioInterop.IMMDeviceEnumerator)
                    new AudioInterop.MMDeviceEnumeratorComObject();
                AudioInterop.IMMDevice dev;
                if (en.GetDefaultAudioEndpoint(AudioInterop.eRender, AudioInterop.eConsole, out dev) != 0 || dev == null) return false;
                Guid iid = AudioInterop.IID_IAudioMeterInformation;
                object o;
                if (dev.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out o) != 0 || o == null) return false;
                _meter = (AudioInterop.IAudioMeterInformation)o;
                _failCount = 0;
                return _meter != null;
            }
            catch { return false; }
        }

        public static bool Available
        {
            get { return Read() >= 0f && _meter != null; }
        }
    }

    /// <summary>线性插值重采样。</summary>
    public class LinearResampler
    {
        private readonly double _ratio;
        private double _pos;
        private short _last;

        public LinearResampler(int from, int to)
        {
            _ratio = (double)from / to;
            _pos = 0;
        }

        public short[] Process(short[] input, int count)
        {
            if (count <= 0) return new short[0];
            int outCount = (int)Math.Floor((count - _pos) / _ratio);
            if (outCount <= 0) { _pos -= count; return new short[0]; }
            short[] output = new short[outCount];
            for (int i = 0; i < outCount; i++)
            {
                double idx = _pos + i * _ratio;
                int i0 = (int)Math.Floor(idx);
                double frac = idx - i0;
                short a, b;
                if (i0 < 0) a = _last; else a = input[Math.Min(i0, count - 1)];
                if (i0 + 1 < 0) b = _last; else b = input[Math.Min(i0 + 1, count - 1)];
                output[i] = (short)(a + (b - a) * frac);
            }
            _pos = _pos + outCount * _ratio - count;
            _last = input[count - 1];
            return output;
        }
    }

    /// <summary>采集系统正在播放的声音（WASAPI 回环），输出单声道 16bit。</summary>
    public class LoopbackCapture : IDisposable
    {
        private AudioInterop.IAudioClient _client;
        private AudioInterop.IAudioCaptureClient _capture;
        private Thread _thread;
        private volatile bool _running;
        private int _targetRate;
        private bool _isFloat;
        private short _channels;
        private int _srcRate;
        private int _blockAlign;
        private LinearResampler _resampler;
        private float _peak;

        public event Action<short[]> Data;

        public float LastPeak { get { return _peak; } }
        public bool CaptureSystemAudio = true;

        public LoopbackCapture(int targetRate)
        {
            _targetRate = targetRate;
        }

        public string LastError;

        public bool Start()
        {
            try
            {
                AudioInterop.IMMDeviceEnumerator en = (AudioInterop.IMMDeviceEnumerator)
                    new AudioInterop.MMDeviceEnumeratorComObject();
                AudioInterop.IMMDevice dev;
                if (en.GetDefaultAudioEndpoint(AudioInterop.eRender, AudioInterop.eConsole, out dev) != 0)
                {
                    LastError = "找不到默认播放设备";
                    return false;
                }
                Guid iid = AudioInterop.IID_IAudioClient;
                object o;
                if (dev.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out o) != 0)
                {
                    LastError = "无法激活音频客户端";
                    return false;
                }
                _client = (AudioInterop.IAudioClient)o;

                IntPtr fmtPtr;
                if (_client.GetMixFormat(out fmtPtr) != 0)
                {
                    LastError = "无法读取设备格式";
                    return false;
                }
                AudioInterop.WAVEFORMATEX fmt = (AudioInterop.WAVEFORMATEX)Marshal.PtrToStructure(fmtPtr, typeof(AudioInterop.WAVEFORMATEX));
                _channels = fmt.nChannels;
                _srcRate = fmt.nSamplesPerSec;
                _blockAlign = fmt.nBlockAlign;
                _isFloat = fmt.wBitsPerSample == 32;
                _resampler = new LinearResampler(_srcRate, _targetRate);

                long buf = 1000000L; // 100ms
                int hr = _client.Initialize(AudioInterop.AUDCLNT_SHAREMODE_SHARED,
                    AudioInterop.AUDCLNT_STREAMFLAGS_LOOPBACK, buf, 0, fmtPtr, IntPtr.Zero);
                AudioInterop.CoTaskMemFree(fmtPtr);
                if (hr != 0)
                {
                    LastError = "回环初始化失败（错误码 " + hr + "）";
                    return false;
                }

                object capObj;
                Guid capIid = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
                if (_client.GetService(ref capIid, out capObj) != 0 || capObj == null)
                {
                    LastError = "无法获取采集接口";
                    return false;
                }
                _capture = (AudioInterop.IAudioCaptureClient)capObj;
                if (_client.Start() != 0)
                {
                    LastError = "回环启动失败";
                    return false;
                }

                _running = true;
                _thread = new Thread(Loop);
                _thread.IsBackground = true;
                _thread.Name = "loopback-capture";
                _thread.Start();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        private void Loop()
        {
            byte[] scratch = new byte[0];
            while (_running)
            {
                try
                {
                    int packet;
                    if (_capture.GetNextPacketSize(out packet) != 0) break;
                    while (packet > 0)
                    {
                        IntPtr data;
                        int frames;
                        int flags;
                        long devPos, qpc;
                        if (_capture.GetBuffer(out data, out frames, out flags, out devPos, out qpc) != 0) break;
                        if (frames > 0)
                        {
                            short[] mono = new short[frames];
                            if ((flags & AudioInterop.AUDCLNT_BUFFERFLAGS_SILENT) != 0)
                            {
                                _peak = 0;
                            }
                            else
                            {
                                int need = frames * _blockAlign;
                                if (scratch.Length < need) scratch = new byte[need];
                                for (int i = 0; i < need; i++) scratch[i] = Marshal.ReadByte(data, i);
                                float peak = 0;
                                if (_isFloat)
                                {
                                    for (int f = 0; f < frames; f++)
                                    {
                                        float sum = 0;
                                        int baseIdx = f * _blockAlign;
                                        for (int c = 0; c < _channels; c++)
                                        {
                                            int off = baseIdx + c * 4;
                                            float v = BitConverter.ToSingle(scratch, off);
                                            sum += v;
                                            float av = Math.Abs(v);
                                            if (av > peak) peak = av;
                                        }
                                        float m = sum / _channels;
                                        mono[f] = (short)Math.Max(-32768, Math.Min(32767, (int)(m * 32000)));
                                    }
                                }
                                else
                                {
                                    for (int f = 0; f < frames; f++)
                                    {
                                        int sum = 0;
                                        int baseIdx = f * _blockAlign;
                                        for (int c = 0; c < _channels; c++)
                                        {
                                            short v = BitConverter.ToInt16(scratch, baseIdx + c * 2);
                                            sum += v;
                                            float av = Math.Abs((float)v) / 32768f;
                                            if (av > peak) peak = av;
                                        }
                                        mono[f] = (short)(sum / _channels);
                                    }
                                }
                                _peak = peak;
                            }
                            _capture.ReleaseBuffer(frames);
                            short[] outSamples = _srcRate == _targetRate ? mono : _resampler.Process(mono, mono.Length);
                            Action<short[]> h = Data;
                            if (h != null && outSamples.Length > 0) h(outSamples);
                        }
                        else
                        {
                            _capture.ReleaseBuffer(0);
                        }
                        if (_capture.GetNextPacketSize(out packet) != 0) break;
                    }
                }
                catch { break; }
                Thread.Sleep(10);
            }
        }

        public void Stop()
        {
            _running = false;
            if (_thread != null)
            {
                try { _thread.Join(300); } catch { }
                _thread = null;
            }
            try { if (_client != null) _client.Stop(); } catch { }
            try { if (_client != null) Marshal.ReleaseComObject(_client); } catch { }
            try { if (_capture != null) Marshal.ReleaseComObject(_capture); } catch { }
            _client = null;
            _capture = null;
            _peak = 0;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
