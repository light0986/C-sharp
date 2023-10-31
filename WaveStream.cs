using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace SoundPlayer調音量.Models
{
    public class WaveStream : Stream
    {
        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanRead
        {
            get { return !IsClosed; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        private bool IsClosed
        {
            get { return reader == null; }
        }

        public override long Position
        {
            get { CheckDisposed(); throw new NotSupportedException(); }
            set { CheckDisposed(); throw new NotSupportedException(); }
        }

        public override long Length
        {
            get { CheckDisposed(); throw new NotSupportedException(); }
        }

        public int Volume
        {
            get { CheckDisposed(); return volume; }
            set
            {
                CheckDisposed();

                if (value < 0 || MaxVolume < value)
                {
                    throw new ArgumentOutOfRangeException("Volume", value, "請設定0到100之間的數字");
                }
                   
                volume = value;
            }
        }

        public WaveStream(Stream baseStream)
        {
            if (baseStream == null)
                throw new ArgumentNullException("baseStream");
            if (!baseStream.CanRead)
                throw new ArgumentException("", "baseStream");

            this.reader = new BinaryReader(baseStream);

            ReadHeader();
        }

        public override void Close()
        {
            if (reader != null)
            {
                reader.Close();
                reader = null;
            }
        }

        private void ReadHeader()
        {
            using (var headerStream = new MemoryStream())
            {
                var writer = new BinaryWriter(headerStream);

                // RIFFヘッダ
                var riffHeader = reader.ReadBytes(12);

                writer.Write(riffHeader);

                // dataチャンクまでの内容をwriterに書き写す
                for (; ; )
                {
                    var chunkHeader = reader.ReadBytes(8);

                    writer.Write(chunkHeader);

                    var fourcc = BitConverter.ToInt32(chunkHeader, 0);
                    var size = BitConverter.ToInt32(chunkHeader, 4);

                    if (fourcc == 0x61746164) // 'data'
                        break;

                    writer.Write(reader.ReadBytes(size));
                }

                writer.Close();

                header = headerStream.ToArray();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (buffer == null) { throw new ArgumentNullException("buffer"); }
            if (offset < 0) { throw new ArgumentOutOfRangeException("offset", offset, "，請設定0以上"); }    
            if (count < 0) { throw new ArgumentOutOfRangeException("count", count, "，請設定0以上"); }
            if (buffer.Length - count < offset) { throw new ArgumentException("資料格式不合", "offset"); }

            if (header == null)
            {
                var samplesToRead = count / 2;
                var bytesToRead = samplesToRead * 2;
                var len = reader.Read(buffer, offset, bytesToRead);

                if (len == 0) { return 0; }

                for (var sample = 0; sample < samplesToRead; sample++)
                {
                    short s = (short)(buffer[offset] | (buffer[offset + 1] << 8));

                    s = (short)(((int)s * volume) / MaxVolume);

                    buffer[offset] = (byte)(s & 0xff);
                    buffer[offset + 1] = (byte)((s >> 8) & 0xff);

                    offset += 2;
                }

                return len;
            }
            else
            {
                var bytesToRead = Math.Min(header.Length - headerOffset, count);

                Buffer.BlockCopy(header, headerOffset, buffer, offset, bytesToRead);

                headerOffset += bytesToRead;

                if (headerOffset == header.Length) { header = null; }       

                return bytesToRead;
            }
        }

        public override void SetLength(long @value)
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        public override void Flush()
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        private void CheckDisposed()
        {
            if (IsClosed) { throw new ObjectDisposedException(GetType().FullName); }
        }

        private BinaryReader reader;
        private byte[] header;
        private int headerOffset = 0;
        private int volume = MaxVolume;
        private const int MaxVolume = 100;
    }
}
