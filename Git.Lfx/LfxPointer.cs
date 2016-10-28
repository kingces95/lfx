using System;
using System.IO;
using Util;

namespace Git.Lfx {

    public enum LfxIdType {
        File,
        Zip,
        Exe
    }

    public struct LfxId : IEquatable<LfxId> {
        public const int CurrentVersion = 1;

        public static bool operator==(LfxId lhs, LfxId rhs) => lhs.Equals(rhs);
        public static bool operator!=(LfxId lhs, LfxId rhs) => !lhs.Equals(rhs);
        public static implicit operator Uri(LfxId id) => id.Url;
        public static implicit operator LfxHash(LfxId id) => id.Hash;

        public static LfxId CreateFile(Uri url, LfxHash? hash = null) {
            return new LfxId(LfxIdType.File, url, hash);
        }
        public static LfxId CreateExe(Uri url, string args, LfxHash? hash = null) {
            return new LfxId(LfxIdType.Exe, url, hash, args: args);
        }
        public static LfxId CreateZip(Uri url, LfxHash? hash = null) {
            return new LfxId(LfxIdType.Zip, url, hash);
        }

        private LfxIdType m_type;
        private Uri m_url;
        private LfxHash m_hash;
        private string m_args;

        private LfxId(
            LfxIdType type,
            Uri url,
            LfxHash? hash = null,
            string args = null)
            : this() {

            m_type = type;
            m_url = url;
            m_hash = hash ?? default(LfxHash);
            m_args = args;
        }

        public int Version => CurrentVersion;

        public LfxIdType Type => m_type;
        public bool IsExe => Type == LfxIdType.Exe;
        public bool IsZip => Type == LfxIdType.Zip;
        public bool IsFile => Type == LfxIdType.File;

        public Uri Url => m_url;
        public LfxHash Hash => m_hash;
        public string Args => m_args;

        public override bool Equals(object obj) => obj is LfxId ? Equals((LfxId)obj) : false;
        public bool Equals(LfxId other) {
            if (Type != other.Type)
                return false;

            if (Version != other.Version)
                return false;

            if (Url != other.Url)
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
            hashcode ^= Args?.GetHashCode() ?? 0;

            return hashcode;
        }
        public override string ToString() => m_hash != null ? $"{Hash}" : $"{Url}";
    }

    public struct LfxPointer : IEquatable<LfxPointer> {
        public static bool operator ==(LfxPointer lhs, LfxPointer rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxPointer lhs, LfxPointer rhs) => !lhs.Equals(rhs);
        public static implicit operator Uri(LfxPointer pointer) => pointer.Url;
        public static implicit operator LfxHash(LfxPointer pointer) => pointer.Hash;
        public static implicit operator LfxId(LfxPointer pointer) => pointer.Id;

        public static LfxPointer CreateFile(Uri url, LfxHash hash, long size) {
            return new LfxPointer(LfxId.CreateFile(url, hash), size);
        }
        public static LfxPointer CreateExe(Uri url, string args, LfxHash hash, long size, long contentSize) {
            return new LfxPointer(LfxId.CreateExe(url, args, hash), size, contentSize);
        }
        public static LfxPointer CreateZip(Uri url, LfxHash hash, long size, long contentSize) {
            return new LfxPointer(LfxId.CreateZip(url, hash), size, contentSize);
        }
        public static LfxPointer Create(LfxId id, string cachePath, string compressedPath = null) {
            var url = id.Url;
            var hash = LfxHash.Parse(Path.GetFileName(cachePath));

            // file
            if (id.IsFile)
                return CreateFile(url, hash, cachePath.GetFileSize());

            // archive
            if (compressedPath == null)
                throw new ArgumentException(
                    $"To create an archive pointer supply a compressed path.");

            var dirSize = cachePath.GetDirectorySize();
            var fileSize = compressedPath.GetFileSize();

            // exe
            if (id.IsExe)
                return CreateExe(url, id.Args, hash, fileSize, dirSize);

            // zip
            return CreateZip(url, hash, fileSize, dirSize);
        }

        public static LfxPointer Load(string path) {
            using (var sr = new StreamReader(path)) {

                // type
                var typeLine = sr.ReadLine();
                LfxIdType type;
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

                // file
                if (type != LfxIdType.File) 
                    pointer = CreateFile(url, hash, size);

                // archive
                else {

                    // content size
                    var contentSizeLine = sr.ReadLine();
                    int contentSize;
                    if (!int.TryParse(contentSizeLine, out contentSize))
                        throw new Exception($"LfxPointer '{path}' has unrecognized content size '{contentSizeLine}'.");

                    // zip
                    if (type == LfxIdType.Zip)
                        pointer = CreateZip(url, hash, size, contentSize);

                    // exe
                    else if (type == LfxIdType.Exe) {

                        // cmd
                        var args = sr.ReadLine();
                        if (args == null)
                            throw new Exception($"LfxPointer '{path}' has no args specified.");

                        pointer = CreateExe(url, args, hash, size, contentSize);
                    }
                }

                // eof
                var lastLine = sr.ReadLine();
                if (lastLine != null)
                        throw new Exception($"LfxPointer '{path}' has unrecognized line '{lastLine}'.");

                return pointer;
            }
        }

        private readonly LfxId m_id;
        private readonly long m_size;
        private readonly long m_contentSize;

        private LfxPointer(
            LfxId id,
            long? size = null,
            long? contentSize = null)
            : this() {

            m_id = id;
            m_size = size ?? -1;
            m_contentSize = contentSize ?? -1L;
        }

        public LfxId Id => m_id;

        public LfxIdType Type => Id.Type;
        public bool IsExe => Type == LfxIdType.Exe;
        public bool IsZip => Type == LfxIdType.Zip;
        public bool IsFile => Type == LfxIdType.File;

        public int Version => Id.Version;
        public Uri Url => Id.Url;
        public LfxHash Hash => Id.Hash;
        public string Args => Id.Args;

        public long Size => m_size;
        public long ContentSize => m_contentSize;

        public void Save(StreamWriter stream) {
            stream.WriteLine(Type);
            stream.WriteLine(Version);
            stream.WriteLine(Url);
            stream.WriteLine(Hash);
            stream.WriteLine(Size);

            if (Type == LfxIdType.File)
                return;

            stream.WriteLine(ContentSize);

            if (Type == LfxIdType.Zip)
                return;

            stream.WriteLine(Args);
        }
        public string Value {
            get {
                var ms = new MemoryStream();
                using (var sw = new StreamWriter(ms)) {
                    Save(sw);
                    sw.Flush();

                    ms.Position = 0;
                    using (var sr = new StreamReader(ms))
                        return sr.ReadToEnd();
                }
            }
        }

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxPointer other) {
            if (Id != other.Id)
                return false;

            if (Size != other.Size)
                return false;

            if (ContentSize != other.ContentSize)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Id.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= ContentSize.GetHashCode();

            return hashcode;
        }
        public override string ToString() => Url.ToString();
    }

    public struct LfxEntry : IEquatable<LfxEntry> {
        public static bool operator ==(LfxEntry lhs, LfxEntry rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxEntry lhs, LfxEntry rhs) => !lhs.Equals(rhs);
        public static implicit operator Uri(LfxEntry entry) => entry.Url;
        public static implicit operator LfxHash(LfxEntry entry) => entry.Hash;
        public static implicit operator string(LfxEntry entry) => entry.Path;
        public static implicit operator LfxId(LfxEntry entry) => entry.Id;
        public static implicit operator LfxPointer(LfxEntry entry) => entry.Pointer;

        private readonly LfxPointer m_pointer;
        private readonly string m_path;

        internal LfxEntry(LfxPointer pointer, string path) {
            m_pointer = pointer;
            m_path = path;
        }

        public LfxPointer Pointer => m_pointer;
        public LfxId Id => Pointer.Id;

        public LfxIdType Type => Id.Type;
        public bool IsExe => Type == LfxIdType.Exe;
        public bool IsZip => Type == LfxIdType.Zip;
        public bool IsFile => Type == LfxIdType.File;

        public Uri Url => Id.Url;
        public LfxHash Hash => Id.Hash;
        public string Args => Id.Args;

        public long Size => Pointer.Size;
        public long ContentSize => Pointer.ContentSize;

        public bool IsEmpty => m_path == null;
        public string Path => m_path;

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxEntry other) {
            if (Pointer != other.Pointer)
                return false;

            if (Path != other.Path)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Pointer.GetHashCode();
            hashcode ^= Path.GetHashCode();

            return hashcode;
        }
        public override string ToString() => Path;
    }
}
