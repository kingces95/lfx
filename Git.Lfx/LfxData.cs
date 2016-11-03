using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Util;

namespace Git.Lfx {

    public enum LfxPointerType {
        File,
        Zip,
        Exe,
        Nuget
    }

    public struct LfxArchiveId : IEquatable<LfxArchiveId> {
        private const int Length = 64;

        public static bool operator ==(LfxArchiveId lhs, LfxArchiveId rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxArchiveId lhs, LfxArchiveId rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxArchiveId id) => id.ToString();

        public static LfxArchiveId Create(string value, Encoding encoding) {
            var ms = new MemoryStream();
            {
                var sw = new StreamWriter(ms, Encoding.UTF8);
                sw.Write(value);
                sw.Flush();
            }
            ms.Capacity = (int)ms.Position;
            ms.Position = 0;

            return Create(ms);
        }
        public static LfxArchiveId Create(string path) {
            using (var file = File.OpenRead(path))
                return Create(file);
        }
        public static LfxArchiveId Create(Stream stream) {
            return Create(SHA256.Create().ComputeHash(stream));
        }
        public static LfxArchiveId Create(byte[] value) => new LfxArchiveId(Hash.Create(value));

        public static LfxArchiveId Load(string path) {
            using (var sr = new StreamReader(path))
                return Parse(sr.ReadToEnd());
        }

        public static LfxArchiveId Parse(string value) => new LfxArchiveId(Hash.Parse(value, Length));
        public static bool TryParse(string value, out LfxArchiveId hash) {
            Hash rawHash;
            if (!Hash.TryParse(value, Length, out rawHash))
                return false;

            hash = new LfxArchiveId(rawHash);
            return true;
        }

        private readonly Hash m_value;

        private LfxArchiveId(Hash value) {
            m_value = value;
        }

        public bool IsEmpty => m_value == null;
        public byte[] Value => m_value.Value;

        public override bool Equals(object obj) => obj is LfxArchiveId ? Equals((LfxArchiveId)obj) : false;
        public bool Equals(LfxArchiveId other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }

    public struct LfxContentId : IEquatable<LfxContentId> {
        public static bool operator ==(LfxContentId lhs, LfxContentId rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxContentId lhs, LfxContentId rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxContentId id) => id.ToString();

        public static LfxContentId Parse(string key) {
            var split = key.Split('.');
            var type = (LfxPointerType)Enum.Parse(typeof(LfxPointerType), split[0], ignoreCase: true);
            var version = int.Parse(split[1]);
            var hash = LfxArchiveId.Parse(split[2]);
            return new LfxContentId(type, version, hash);
        }

        private readonly LfxPointerType m_type;
        private readonly int m_version;
        private readonly LfxArchiveId m_hash;

        public LfxContentId(
            LfxPointerType type,
            int version,
            LfxArchiveId hash) {

            m_type = type;
            m_version = version;
            m_hash = hash;
        }

        internal LfxPointerType Type => m_type;
        internal int Version => m_version;
        internal LfxArchiveId Hash => m_hash;

        public override bool Equals(object obj) => obj is LfxContentId ? Equals((LfxContentId)obj) : false;
        public bool Equals(LfxContentId other) {
            if (m_type != other.Type)
                return false;

            if (m_version != other.Version)
                return false;

            if (m_hash != other.Hash)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= m_type.GetHashCode();
            hashcode ^= m_version.GetHashCode();
            hashcode ^= m_hash.GetHashCode();

            return hashcode;
        }
        public override string ToString() => $"{Type}.{Version}.{Hash}";
    }

    public struct LfxPointer : IEquatable<LfxPointer> {
        public const int CurrentVersion = 1;

        public static bool operator ==(LfxPointer lhs, LfxPointer rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxPointer lhs, LfxPointer rhs) => !lhs.Equals(rhs);
        public static implicit operator Uri(LfxPointer id) => id.Url;

        public static LfxPointer CreateFile(Uri url) {
            return new LfxPointer(LfxPointerType.File, url);
        }
        public static LfxPointer CreateExe(Uri url, string args) {
            return new LfxPointer(LfxPointerType.Exe, url, args: args);
        }
        public static LfxPointer CreateZip(Uri url) {
            return new LfxPointer(LfxPointerType.Zip, url);
        }
        public static LfxPointer CreateNuget(Uri url) {
            return new LfxPointer(LfxPointerType.Nuget, url);
        }

        private LfxPointerType m_type;
        private Uri m_url;
        private string m_args;

        private LfxPointer(
            LfxPointerType type,
            Uri url,
            string args = null)
            : this() {

            m_type = type;
            m_url = url;
            m_args = args;
        }

        public int Version => CurrentVersion;

        public LfxPointerType Type => m_type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsNuget => Type == LfxPointerType.Nuget;
        public bool IsFile => Type == LfxPointerType.File;

        public Uri Url => m_url;
        public string Args => m_args;

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxPointer other) {
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
        public override string ToString() => $"{Url}";
    }

    public struct LfxMetadata : IEquatable<LfxMetadata> {

        public static bool operator ==(LfxMetadata lhs, LfxMetadata rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxMetadata lhs, LfxMetadata rhs) => !lhs.Equals(rhs);

        public static LfxMetadata Create(LfxArchiveId hash, long size, long? contentSize = null) {
            return new LfxMetadata(hash, size, contentSize);
        }
        public static LfxMetadata Create(LfxArchiveId hash, string cachePath, string compressedPath = null) {

            // file
            if (cachePath.PathIsFile())
                return new LfxMetadata(hash, cachePath.GetFileSize());

            // directory
            var dirSize = cachePath.GetDirectorySize();
            var fileSize = compressedPath.GetFileSize();
            return new LfxMetadata(hash, fileSize, dirSize);
        }

        private readonly LfxArchiveId m_hash;
        private readonly long m_size;
        private readonly long? m_contentSize;

        private LfxMetadata(
            LfxArchiveId hash,
            long size,
            long? contentSize = null)
            : this() {

            m_hash = hash;
            m_size = size;
            m_contentSize = contentSize;
        }

        // metadata
        public LfxArchiveId Hash => m_hash;
        public long Size => m_size;
        public long? ContentSize => m_contentSize;

        public override bool Equals(object obj) => obj is LfxMetadata ? Equals((LfxMetadata)obj) : false;
        public bool Equals(LfxMetadata other) {
            if (Size != other.Size)
                return false;

            if (ContentSize != other.ContentSize)
                return false;

            if (Hash != other.Hash)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Hash.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= ContentSize?.GetHashCode() ?? 0;

            return hashcode;
        }
        public override string ToString() => Hash;
    }

    [DebuggerDisplay("{Url}")]
    public struct LfxInfo : IEquatable<LfxInfo> {
        public static bool operator ==(LfxInfo lhs, LfxInfo rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxInfo lhs, LfxInfo rhs) => !lhs.Equals(rhs);

        public static LfxInfo Create(LfxPointer pointer, LfxMetadata? metadata = null) {
            return new LfxInfo(pointer, metadata);
        }

        public static LfxInfo Load(string path) {
            using (var sr = new StreamReader(path)) {

                // type
                var typeLine = sr.ReadLine();
                LfxPointerType type;
                if (!Enum.TryParse(typeLine, ignoreCase: true, result: out type))
                    throw new Exception($"LfxPointer '{path}' failed to parse type '{typeLine}'.");

                // version
                var versionLine = sr.ReadLine();
                int version;
                if (!int.TryParse(versionLine, out version))
                    throw new Exception($"LfxPointer '{path}' failed to parse version '{versionLine}'.");

                // url
                var urlLine = sr.ReadLine();
                Uri url;
                if (!Uri.TryCreate(urlLine, UriKind.Absolute, out url))
                    throw new Exception($"LfxPointer '{path}' failed to parse url '{urlLine}'.");

                // exe
                string args = null;
                if (type == LfxPointerType.Exe) {

                    // cmd
                    args = sr.ReadLine();
                    if (args == null)
                        throw new Exception($"LfxPointer '{path}'failed to parse args.");
                }

                // pointer
                var pointer =
                    type == LfxPointerType.File ? LfxPointer.CreateFile(url) :
                    type == LfxPointerType.Zip ? LfxPointer.CreateZip(url) :
                    type == LfxPointerType.Nuget ? LfxPointer.CreateNuget(url) :
                    LfxPointer.CreateExe(url, args);

                // pointer only?
                var hashLine = sr.ReadLine();
                if (hashLine == null)
                    return Create(pointer);

                // hash
                LfxArchiveId hash;
                if (!LfxArchiveId.TryParse(hashLine, out hash))
                    throw new Exception($"LfxPointer '{path}' failed to parse hash '{urlLine}'.");

                // size
                var sizeLine = sr.ReadLine();
                long size;
                if (!long.TryParse(sizeLine, out size))
                    throw new Exception($"LfxPointer '{path}' failed to parse size '{sizeLine}'.");

                // file
                LfxMetadata metadata = default(LfxMetadata);
                if (type == LfxPointerType.File)
                    metadata = LfxMetadata.Create(hash, size);

                // archive
                else {

                    // content size
                    var contentSizeLine = sr.ReadLine();
                    int contentSize;
                    if (!int.TryParse(contentSizeLine, out contentSize))
                        throw new Exception($"LfxPointer '{path}' failed to parse content size '{contentSizeLine}'.");

                    metadata = LfxMetadata.Create(hash, size, contentSize);
                }

                // eof
                var lastLine = sr.ReadLine();
                if (lastLine != null)
                        throw new Exception($"LfxPointer '{path}' has extra line '{lastLine}'.");

                return Create(pointer, metadata);
            }
        }

        private readonly LfxPointer m_pointer;
        private readonly LfxMetadata? m_metadata;

        private LfxInfo(
            LfxPointer pointer,
            LfxMetadata? metadata)
            : this() {

            m_pointer = pointer;
            m_metadata = metadata;
        }

        // type
        public LfxPointerType Type => Pointer.Type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsNuget => Type == LfxPointerType.Nuget;
        public bool IsFile => Type == LfxPointerType.File;

        // poitner
        public LfxPointer Pointer => m_pointer;
        public int Version => Pointer.Version;
        public Uri Url => Pointer.Url;
        public string Args => Pointer.Args;

        // metadata
        public bool HasMetadata => m_metadata != null;
        public LfxMetadata Metadata {
            get {
                if (m_metadata == null)
                    throw new InvalidOperationException(
                        $"Info for pointer '{m_pointer}' has no metadata.");

                return m_metadata.Value;
            }
        }
        public LfxArchiveId Hash => Metadata.Hash;
        public long Size => Metadata.Size;
        public long? ExpandedSize => Metadata.ContentSize;
        public long ContentSize => ExpandedSize ?? Size;

        public override bool Equals(object obj) => obj is LfxInfo ? Equals((LfxInfo)obj) : false;
        public bool Equals(LfxInfo other) {
            if (Pointer != other.Pointer)
                return false;

            if (HasMetadata != other.HasMetadata)
                return false;

            if (!HasMetadata)
                return true;

            if (Size != other.Size)
                return false;

            if (ExpandedSize != other.ExpandedSize)
                return false;

            if (Hash != other.Hash)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Hash.GetHashCode();
            hashcode ^= Pointer.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= ExpandedSize.GetHashCode();

            return hashcode;
        }
        public override string ToString() {
            var sb = new StringBuilder();

            sb.AppendLine(Type);
            sb.AppendLine(Version);
            sb.AppendLine(Url);
            if (Type == LfxPointerType.Exe)
                sb.AppendLine(Args);
            sb.AppendLine(Hash);
            sb.AppendLine(Size);
            if (Type != LfxPointerType.File)
                sb.AppendLine(ExpandedSize);

            return sb.ToString();
        }
    }

    public struct LfxEntry : IEquatable<LfxEntry> {
        public static bool operator ==(LfxEntry lhs, LfxEntry rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxEntry lhs, LfxEntry rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxEntry pointer) => pointer.Path;

        public static LfxEntry Create(LfxInfo info, string path) => new LfxEntry(info, path);

        private readonly LfxInfo m_info;
        private readonly string m_path;

        public LfxEntry(LfxInfo info, string path) {
            m_info = info;
            m_path = path;
        }

        public LfxInfo Info => m_info;
        public string Path => m_path;

        public override bool Equals(object obj) => obj is LfxEntry ? Equals((LfxEntry)obj) : false;
        public bool Equals(LfxEntry other) {
            if (Info != other.Info)
                return false;

            if (Path != other.Path)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Info.GetHashCode();
            hashcode ^= Path.GetHashCode();

            return hashcode;
        }
        public override string ToString() => $"{Path}";
    }
}
