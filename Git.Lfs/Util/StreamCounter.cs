using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Git.Lfs {

    public sealed class StreamCounter : Stream {
        private readonly Stream m_stream;
        private long m_position;
        private long m_available;

        public StreamCounter(Stream stream, long available = long.MaxValue) {
            m_stream = stream;
            m_available = available;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length {
            get { throw new InvalidOperationException(); }
        }
        public override long Position {
            get { return m_position; }
            set { throw new InvalidOperationException(); }
        }
        public override void Flush() => m_stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) {
            var consumed = (long)m_stream.Read(buffer, offset, count);
            m_available -= consumed;
            if (m_available < 0)
                consumed += m_available;

            m_position += consumed;
            return (int)consumed;
        }
        public override long Seek(long offset, SeekOrigin origin) {
            throw new InvalidOperationException();
        }
        public override void SetLength(long value) {
            throw new InvalidOperationException();
        }
        public override void Write(byte[] buffer, int offset, int count) {
            throw new InvalidOperationException();
        }
    }
}