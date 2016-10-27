using System;
using System.IO;

namespace Git.Lfx {
    public enum LfxPointerType {
        File,
        Zip,
        Exe
    }
    public struct LfxPointer : IEquatable<LfxPointer> {
        public const int CurrentVersion = 1;

        public static LfxPointer CreateFile(Uri url) {
            return new LfxPointer(LfxPointerType.File, url);
        }
        public static LfxPointer CreateExe(Uri url, string args) {
            return new LfxPointer(LfxPointerType.Exe, url, args: args);
        }
        public static LfxPointer CreateZip(Uri url) {
            return new LfxPointer(LfxPointerType.Zip, url);
        }

        public static LfxPointer CreateFile(Uri url, long size, LfxHash hash) {
            return new LfxPointer(LfxPointerType.File, url, hash, size);
        }
        public static LfxPointer CreateExe(Uri url, long size, LfxHash hash, long contentSize, string args) {
            return new LfxPointer(LfxPointerType.Exe, url, hash, size, contentSize, args);
        }
        public static LfxPointer CreateZip(Uri url, long size, LfxHash hash, long contentSize) {
            return new LfxPointer(LfxPointerType.Zip, url, hash, size, contentSize);
        }

        public static LfxPointer Load(string path) {
            using (var sr = new StreamReader(path)) {

                // type
                var typeLine = sr.ReadLine();
                LfxPointerType type;
                if (!Enum.TryParse(typeLine, ignoreCase: true, result: out type))
                    throw new Exception($"LfxPointer '{path}' has unrecognized type '{typeLine}'.");

                // version
                var versionLine = sr.ReadLine();
                int version;
                if (!int.TryParse(versionLine, out version))
                    throw new Exception($"LfxPointer '{path}' has unrecognized version '{versionLine}'.");

                // url
                var urlLine = sr.ReadLine();
                Uri url;
                if (!Uri.TryCreate(urlLine, UriKind.Absolute, out url))
                    throw new Exception($"LfxPointer '{path}' has unrecognized url '{urlLine}'.");

                // hash
                var hashLine = sr.ReadLine();
                LfxHash hash;
                if (!LfxHash.TryParse(hashLine, out hash))
                    throw new Exception($"LfxPointer '{path}' has unrecognized url '{urlLine}'.");

                // size
                var sizeLine = sr.ReadLine();
                int size;
                if (!int.TryParse(sizeLine, out size))
                    throw new Exception($"LfxPointer '{path}' has unrecognized size '{sizeLine}'.");

                LfxPointer pointer = default(LfxPointer);

                // archive
                if (type != LfxPointerType.File) {

                    // content size
                    var contentSizeLine = sr.ReadLine();
                    int contentSize;
                    if (!int.TryParse(contentSizeLine, out contentSize))
                        throw new Exception($"LfxPointer '{path}' has unrecognized content size '{contentSizeLine}'.");

                    // zip
                    if (type == LfxPointerType.Zip) {
                        pointer = new LfxPointer(LfxPointerType.Zip, url, hash, size, contentSize);
                    }

                    // exe
                    else if (type == LfxPointerType.Exe) {

                        // cmd
                        var cmd = sr.ReadLine();
                        if (cmd == null)
                            throw new Exception($"LfxPointer '{path}' has no cmd specified.");

                        pointer = new LfxPointer(LfxPointerType.Exe, url, hash, size, contentSize, cmd);
                    }
                }

                // eof
                var lastLine = sr.ReadLine();
                if (lastLine != null)
                        throw new Exception($"LfxPointer '{path}' has unrecognized line '{lastLine}'.");

                return pointer;
            }
        }

        private LfxPointerType m_type;
        private Uri m_url;
        private LfxHash m_hash;
        private long m_size;
        private long m_contentSize;
        private string m_args;

        private LfxPointer(
            LfxPointerType type,
            Uri url,
            LfxHash? hash = null,
            long? size = null,
            long? contentSize = null,
            string args = null)
            : this() {

            m_type = type;
            m_url = url;
            m_size = size ?? -1;
            m_hash = hash ?? default(LfxHash);
            m_contentSize = contentSize ?? -1L;
            m_args = args;
        }

        public LfxPointerType Type => m_type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsFile => Type == LfxPointerType.File;

        public int Version => CurrentVersion;
        public Uri Url => m_url;
        public long Size => m_size;
        public LfxHash Hash => m_hash;
        public long ContentSize => m_contentSize;
        public string Args => m_args;

        public void Save(StreamWriter stream) {
            stream.WriteLine(Type);
            stream.WriteLine(Version);
            stream.WriteLine(Url);
            stream.WriteLine(Hash);
            stream.WriteLine(Size);

            if (Type == LfxPointerType.File)
                return;

            stream.WriteLine(ContentSize);

            if (Type == LfxPointerType.Zip)
                return;

            stream.WriteLine(Args);
        }
        public string Value {
            get {
                var ms = new MemoryStream();
                using (var stream = new StreamWriter(ms))
                    Save(stream);

                ms.Position = 0;
                using (var stream = new StreamReader(ms))
                    return stream.ReadToEnd();
            }
        }

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxPointer other) {
            if (Type != other.Type)
                return false;

            if (Version != other.Version)
                return false;

            if (Url != other.Url)
                return false;

            if (Size != other.Size)
                return false;

            if (ContentSize != other.ContentSize)
                return false;

            if (Args != other.Args)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Type.GetHashCode();
            hashcode ^= Version.GetHashCode();
            hashcode ^= Url.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= ContentSize.GetHashCode();
            hashcode ^= Args?.GetHashCode() ?? 0;

            return hashcode;
        }
        public override string ToString() => m_url.ToString();
    }
}
