using System;
using System.Collections.Generic;
using System.IO;

namespace Rap.Studio.Engine
{
    /// <summary>环形 PCM 缓冲。</summary>
    internal class PcmQueue
    {
        private short[] _buf = new short[48000];
        private int _head;
        private int _count;

        public int Count { get { return _count; } }

        public void Push(short[] data)
        {
            if (data == null || data.Length == 0) return;
            if (_count + data.Length > _buf.Length)
            {
                int need = _count + data.Length + 48000;
                short[] nb = new short[Math.Max(need, _buf.Length * 2)];
                for (int i = 0; i < _count; i++) nb[i] = _buf[(_head + i) % _buf.Length];
                _buf = nb;
                _head = 0;
            }
            int tail = (_head + _count) % _buf.Length;
            for (int i = 0; i < data.Length; i++)
            {
                _buf[tail] = data[i];
                tail++;
                if (tail == _buf.Length) tail = 0;
            }
            _count += data.Length;
        }

        public short[] Take(int n)
        {
            int take = Math.Min(n, _count);
            short[] outArr = new short[take];
            for (int i = 0; i < take; i++) outArr[i] = _buf[(_head + i) % _buf.Length];
            _head = (_head + take) % _buf.Length;
            _count -= take;
            return outArr;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        public void TrimTo(int max)
        {
            if (_count <= max) return;
            int drop = _count - max;
            _head = (_head + drop) % _buf.Length;
            _count = max;
        }
    }

    /// <summary>极简 AVI（Motion-JPEG + PCM）写入器：边录边写，结束时回填索引。</summary>
    public class AviWriter : IDisposable
    {
        private struct IdxEntry
        {
            public int Id;
            public int Flags;
            public long Offset;
            public int Size;
        }

        private readonly BinaryWriter _bw;
        private readonly object _lock = new object();
        private readonly List<IdxEntry> _index = new List<IdxEntry>();
        private readonly PcmQueue _pcm = new PcmQueue();
        private int _width, _height, _fps, _rate;
        private int _frames, _samples, _sampleAcc;
        private long _riffSizePos, _hdrlSizePos, _moviSizePos, _moviFourccPos;
        private long _avihPos, _vidLenPos, _audLenPos;
        private bool _closed;
        public string FilePath { get; private set; }
        public long FramesWritten { get { return _frames; } }

        private static int FCC(string s)
        {
            return s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24);
        }

        public AviWriter(string path, int width, int height, int fps, int sampleRate)
        {
            FilePath = path;
            _width = width & ~1;
            _height = height & ~1;
            if (_width < 16) _width = 16;
            if (_height < 16) _height = 16;
            _fps = Math.Max(1, fps);
            _rate = sampleRate;

            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            _bw = new BinaryWriter(fs);
            WriteHeader();
        }

        private void WriteHeader()
        {
            _bw.Write(FCC("RIFF"));
            _riffSizePos = Pos();
            _bw.Write((int)0);
            _bw.Write(FCC("AVI "));

            _bw.Write(FCC("LIST"));
            _hdrlSizePos = Pos();
            _bw.Write((int)0);
            _bw.Write(FCC("hdrl"));

            // ---- MainAVIHeader ----
            _bw.Write(FCC("avih"));
            _bw.Write(56);
            _avihPos = Pos();
            _bw.Write(1000000 / _fps);   // dwMicroSecPerFrame
            _bw.Write(0);                // dwMaxBytesPerSec（结束时回填）
            _bw.Write(0);                // dwPaddingGranularity
            _bw.Write(0x10);             // AVIF_HASINDEX
            _bw.Write(0);                // dwTotalFrames（回填）
            _bw.Write(0);                // dwInitialFrames
            _bw.Write(2);                // dwStreams
            _bw.Write(0);                // dwSuggestedBufferSize
            _bw.Write(_width);
            _bw.Write(_height);
            _bw.Write(0); _bw.Write(0); _bw.Write(0); _bw.Write(0);

            // ---- 视频流 ----
            _bw.Write(FCC("LIST"));
            long vListSize = Pos();
            _bw.Write((int)0);
            _bw.Write(FCC("strl"));

            _bw.Write(FCC("strh"));
            _bw.Write(56);
            _bw.Write(FCC("vids"));
            _bw.Write(FCC("MJPG"));
            _bw.Write(0);
            _bw.Write((short)0);
            _bw.Write((short)0);
            _bw.Write(0);                 // initialFrames
            _bw.Write(1);                 // scale
            _bw.Write(_fps);              // rate
            _bw.Write(0);                 // start
            _vidLenPos = Pos();
            _bw.Write(0);                 // length（回填）
            _bw.Write(0);                 // suggestedBufferSize
            _bw.Write(-1);                // quality
            _bw.Write(0);                 // sampleSize
            _bw.Write(0); _bw.Write(0); _bw.Write(_width); _bw.Write(_height);

            _bw.Write(FCC("strf"));
            _bw.Write(40);
            _bw.Write(40);
            _bw.Write(_width);
            _bw.Write(_height);
            _bw.Write((short)1);
            _bw.Write((short)24);
            _bw.Write(FCC("MJPG"));
            _bw.Write(_width * _height * 3);
            _bw.Write(0); _bw.Write(0); _bw.Write(0); _bw.Write(0);

            PatchLater(vListSize);

            // ---- 音频流 ----
            _bw.Write(FCC("LIST"));
            long aListSize = Pos();
            _bw.Write((int)0);
            _bw.Write(FCC("strl"));

            _bw.Write(FCC("strh"));
            _bw.Write(56);
            _bw.Write(FCC("auds"));
            _bw.Write(0);
            _bw.Write(0);
            _bw.Write((short)0);
            _bw.Write((short)0);
            _bw.Write(0);
            _bw.Write(1);                  // scale
            _bw.Write(_rate);              // rate
            _bw.Write(0);
            _audLenPos = Pos();
            _bw.Write(0);                  // length（回填，单位：采样数）
            _bw.Write(0);
            _bw.Write(-1);
            _bw.Write(2);                  // sampleSize = blockAlign
            _bw.Write(0); _bw.Write(0); _bw.Write(0); _bw.Write(0);

            _bw.Write(FCC("strf"));
            _bw.Write(18);
            _bw.Write((short)1);           // PCM
            _bw.Write((short)1);           // mono
            _bw.Write(_rate);
            _bw.Write(_rate * 2);
            _bw.Write((short)2);           // blockAlign
            _bw.Write((short)16);
            _bw.Write((short)0);

            PatchLater(aListSize);

            // ---- movi ----
            _bw.Write(FCC("LIST"));
            _moviSizePos = Pos();
            _bw.Write((int)0);
            _moviFourccPos = Pos();
            _bw.Write(FCC("movi"));
        }

        private void PatchLater(long sizePos)
        {
            long end = Pos();
            long size = end - (sizePos + 4);
            _bw.Seek((int)sizePos, SeekOrigin.Begin);
            _bw.Write((int)size);
            _bw.Seek((int)end, SeekOrigin.Begin);
        }

        private long Pos()
        {
            return _bw.BaseStream.Position;
        }

        public void PushAudio(short[] mono)
        {
            lock (_lock) { _pcm.Push(mono); }
        }

        private void WriteChunk(string id, byte[] data, bool index)
        {
            long pos = Pos();
            int fcc = FCC(id);
            _bw.Write(fcc);
            _bw.Write(data.Length);
            _bw.Write(data);
            if ((data.Length & 1) != 0) _bw.Write((byte)0);
            if (index)
            {
                IdxEntry e;
                e.Id = fcc;
                e.Flags = 0x10;   // AVIIF_KEYFRAME
                e.Offset = pos - _moviFourccPos;
                e.Size = data.Length;
                _index.Add(e);
            }
        }

        public void AddFrame(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0) return;
            lock (_lock)
            {
                if (_closed) return;
                _sampleAcc += _rate;
                int need = _sampleAcc / _fps;
                _sampleAcc -= need * _fps;
                WriteChunk("00dc", jpeg, true);

                short[] got = _pcm.Take(need);
                short[] full = new short[need];
                if (got.Length > 0) Array.Copy(got, full, Math.Min(got.Length, need));
                byte[] abytes = new byte[need * 2];
                for (int i = 0; i < need; i++)
                {
                    abytes[i * 2] = (byte)(full[i] & 0xFF);
                    abytes[i * 2 + 1] = (byte)((full[i] >> 8) & 0xFF);
                }
                WriteChunk("01wb", abytes, true);
                _frames++;
                _samples += need;
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                if (_closed) return;
                _closed = true;
                try
                {
                    long moviEnd = Pos();
                    long moviSize = moviEnd - (_moviSizePos + 4);
                    _bw.Seek((int)_moviSizePos, SeekOrigin.Begin);
                    _bw.Write((int)moviSize);
                    _bw.Seek((int)moviEnd, SeekOrigin.Begin);

                    long idxPos = Pos();
                    _bw.Write(FCC("idx1"));
                    _bw.Write(_index.Count * 16);
                    for (int i = 0; i < _index.Count; i++)
                    {
                        IdxEntry e = _index[i];
                        _bw.Write(e.Id);
                        _bw.Write(e.Flags);
                        _bw.Write((int)e.Offset);
                        _bw.Write(e.Size);
                    }

                    long hdrlSize = idxPos - (_hdrlSizePos + 4);
                    _bw.Seek((int)_hdrlSizePos, SeekOrigin.Begin);
                    _bw.Write((int)hdrlSize);

                    _bw.Seek((int)_avihPos, SeekOrigin.Begin);
                    _bw.Write(1000000 / _fps);
                    long bytesPerSec = (long)((moviSize + 8) / Math.Max(1.0, (double)_frames / _fps));
                    _bw.Write((int)Math.Min(bytesPerSec, int.MaxValue));
                    _bw.Write(0);
                    _bw.Write(0x10);
                    _bw.Write(_frames);

                    _bw.Seek((int)_vidLenPos, SeekOrigin.Begin);
                    _bw.Write(_frames);
                    _bw.Seek((int)_audLenPos, SeekOrigin.Begin);
                    _bw.Write(_samples);

                    long endPos = Pos();
                    _bw.Seek((int)_riffSizePos, SeekOrigin.Begin);
                    _bw.Write((int)(endPos - 8));
                    _bw.Seek((int)endPos, SeekOrigin.Begin);

                    _bw.Flush();
                    _bw.Close();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
