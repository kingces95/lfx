using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Git.Lfx {

    public static class Extensions {
        public static LfxHash GetHash(this string value, Encoding encoding) => LfxHash.Create(value, encoding);
        public static LfxHash GetHash(this byte[] bytes) => LfxHash.Create(bytes);
    }
}