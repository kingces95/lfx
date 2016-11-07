using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Util;

namespace Git.Lfx {

    public enum LfxType {
        None = 0,
        File,
        Zip,
        Exe,
        Nuget
    }

    public struct LfxUrlId : IEquatable<LfxUrlId> {

        public static bool operator ==(LfxUrlId lhs, LfxUrlId rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxUrlId lhs, LfxUrlId rhs) => !lhs.Equals(rhs);
        public static implicit operator Sha256Hash(LfxUrlId hash) => hash.m_value;
        public static explicit operator LfxUrlId(Sha256Hash hash) => new LfxUrlId(hash);
        public static implicit operator ByteVector(LfxUrlId hash) => hash.m_value;
        public static explicit operator LfxUrlId(ByteVector vector) => (LfxUrlId)(Sha256Hash)vector;

        public static LfxUrlId Create(Uri url) {
            return new LfxUrlId((Sha256Hash)url.ToString().ToLower().ToByteVector(Encoding.UTF8));
        }

        private readonly Sha256Hash m_value;

        private LfxUrlId(Sha256Hash value) {
            m_value = value;
        }

        public override bool Equals(object obj) => obj is LfxUrlId ? Equals((LfxUrlId)obj) : false;
        public bool Equals(LfxUrlId other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }

    public struct LfxArchiveId : IEquatable<LfxArchiveId> {

        public static bool operator ==(LfxArchiveId lhs, LfxArchiveId rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxArchiveId lhs, LfxArchiveId rhs) => !lhs.Equals(rhs);
        public static implicit operator Sha256Hash(LfxArchiveId hash) => hash.m_value;
        public static explicit operator LfxArchiveId(Sha256Hash hash) => new LfxArchiveId(hash);
        public static implicit operator ByteVector(LfxArchiveId hash) => hash.m_value;
        public static explicit operator LfxArchiveId(ByteVector vector) => (LfxArchiveId)(Sha256Hash)vector;

        private readonly Sha256Hash m_value;

        private LfxArchiveId(Sha256Hash value) {
            m_value = value;
        }

        public override bool Equals(object obj) => obj is LfxArchiveId ? Equals((LfxArchiveId)obj) : false;
        public bool Equals(LfxArchiveId other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }

    public struct LfxId : IEquatable<LfxId> {
        public static bool operator ==(LfxId lhs, LfxId rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxId lhs, LfxId rhs) => !lhs.Equals(rhs);

        public static LfxId Parse(string key) {
            var split = key.Split('.');
            var type = (LfxType)Enum.Parse(typeof(LfxType), split[0], ignoreCase: true);
            var version = int.Parse(split[1]);
            var hash = (LfxArchiveId)ByteVector.Parse(split[2]);
            return new LfxId(type, version, hash);
        }

        private readonly LfxType m_type;
        private readonly int m_version;
        private readonly LfxArchiveId m_hash;

        public LfxId(
            LfxType type,
            int version,
            LfxArchiveId hash) {

            m_type = type;
            m_version = version;
            m_hash = hash;
        }

        internal LfxType Type => m_type;
        internal int Version => m_version;
        internal LfxArchiveId Hash => m_hash;

        public override bool Equals(object obj) => obj is LfxId ? Equals((LfxId)obj) : false;
        public bool Equals(LfxId other) {
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
            return new LfxPointer(LfxType.File, url);
        }
        public static LfxPointer CreateExe(Uri url, string args) {
            return new LfxPointer(LfxType.Exe, url, args: args);
        }
        public static LfxPointer CreateZip(Uri url) {
            return new LfxPointer(LfxType.Zip, url);
        }
        public static LfxPointer CreateNuget(Uri url) {
            return new LfxPointer(LfxType.Nuget, url);
        }

        private readonly LfxType m_type;
        private readonly int m_version;
        private readonly Uri m_url;
        private readonly LfxUrlId m_urlHash;
        private readonly string m_args;

        private LfxPointer(
            LfxType type,
            Uri url,
            int version = CurrentVersion,
            string args = null)
            : this() {

            m_type = type;
            m_version = version;
            m_url = url;
            m_urlHash = LfxUrlId.Create(Url);
            m_args = args;
        }

        public int Version => m_version;

        public LfxType Type => m_type;
        public bool IsExe => Type == LfxType.Exe;
        public bool IsZip => Type == LfxType.Zip;
        public bool IsNuget => Type == LfxType.Nuget;
        public bool IsFile => Type == LfxType.File;

        public Uri Url => m_url;
        public LfxUrlId UrlHash => m_urlHash;
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

        public static LfxMetadata Create(LfxArchiveId hash, long downloadSize, long? expandedSize = null) {
            return new LfxMetadata(hash, downloadSize, expandedSize);
        }
        public static LfxMetadata Create(LfxArchiveId hash, string cachePath, string compressedPath = null) {

            // file
            if (cachePath.PathExistsAsFile())
                return new LfxMetadata(hash, cachePath.GetFileSize());

            // directory
            var dirSize = cachePath.GetDirectorySize();
            var fileSize = compressedPath.GetFileSize();
            return new LfxMetadata(hash, fileSize, dirSize);
        }

        private readonly LfxArchiveId m_hash;
        private readonly long m_downloadSize;
        private readonly long? m_expandedSize;

        private LfxMetadata(
            LfxArchiveId hash,
            long downloadSize,
            long? expandedSize = null)
            : this() {

            m_hash = hash;
            m_downloadSize = downloadSize;
            m_expandedSize = expandedSize;
        }

        // metadata
        public LfxArchiveId Hash => m_hash;
        public long DownloadSize => m_downloadSize;
        public long Size => m_expandedSize ?? m_downloadSize;

        public override bool Equals(object obj) => obj is LfxMetadata ? Equals((LfxMetadata)obj) : false;
        public bool Equals(LfxMetadata other) {
            if (m_downloadSize != other.m_downloadSize)
                return false;

            if (m_expandedSize != other.m_expandedSize)
                return false;

            if (m_hash != other.m_hash)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= m_hash.GetHashCode();
            hashcode ^= m_downloadSize.GetHashCode();
            hashcode ^= m_expandedSize?.GetHashCode() ?? 0;

            return hashcode;
        }
        public override string ToString() => Hash.ToString();
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
                LfxType type;
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
                if (type == LfxType.Exe) {

                    // cmd
                    args = sr.ReadLine();
                    if (args == null)
                        throw new Exception($"LfxPointer '{path}'failed to parse args.");
                }

                // pointer
                var pointer =
                    type == LfxType.File ? LfxPointer.CreateFile(url) :
                    type == LfxType.Zip ? LfxPointer.CreateZip(url) :
                    type == LfxType.Nuget ? LfxPointer.CreateNuget(url) :
                    LfxPointer.CreateExe(url, args);

                // pointer only?
                var hashLine = sr.ReadLine();
                if (hashLine == null)
                    return Create(pointer);

                // hash
                ByteVector byteVector;
                if (!ByteVector.TryParse(hashLine, out byteVector))
                    throw new Exception($"LfxPointer '{path}' failed to parse hash '{urlLine}'.");
                var hash = (LfxArchiveId)byteVector;

                // download size
                var downloadSizeLine = sr.ReadLine();
                long downloadSize;
                if (!long.TryParse(downloadSizeLine, out downloadSize))
                    throw new Exception($"LfxPointer '{path}' failed to parse size '{downloadSizeLine}'.");

                // file
                LfxMetadata metadata = default(LfxMetadata);
                if (type == LfxType.File)
                    metadata = LfxMetadata.Create(hash, downloadSize);

                // archive
                else {

                    // expanded size
                    var expandedSizeLine = sr.ReadLine();
                    int expandedSize;
                    if (!int.TryParse(expandedSizeLine, out expandedSize))
                        throw new Exception($"LfxPointer '{path}' failed to parse content size '{expandedSizeLine}'.");

                    metadata = LfxMetadata.Create(hash, downloadSize, expandedSize);
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
        public LfxType Type => Pointer.Type;
        public bool IsFile => Type == LfxType.File;
        public bool IsArchive => !IsFile;
        public bool IsExe => Type == LfxType.Exe;
        public bool IsZip => Type == LfxType.Zip;
        public bool IsNuget => Type == LfxType.Nuget;

        // poitner
        public LfxPointer Pointer => m_pointer;
        public int Version => Pointer.Version;
        public Uri Url => Pointer.Url;
        public LfxUrlId UrlHash => Pointer.UrlHash;
        public string Args => Pointer.Args;

        // metadata
        public bool HasMetadata => m_metadata != null;
        public LfxMetadata? Metadata => m_metadata;
        public LfxArchiveId? Hash => m_metadata?.Hash;
        public long? DownloadSize => m_metadata?.DownloadSize;
        public long? Size => m_metadata?.Size;

        // id
        public LfxId? Id {
            get {
                if (!HasMetadata)
                    return null;

                return new LfxId(Type, Version, (LfxArchiveId)Hash);
            }
        }

        public override bool Equals(object obj) => obj is LfxInfo ? Equals((LfxInfo)obj) : false;
        public bool Equals(LfxInfo other) {
            if (Pointer != other.Pointer)
                return false;

            if (HasMetadata != other.HasMetadata)
                return false;

            if (!HasMetadata)
                return true;

            if (Hash != other.Hash)
                return false;

            if (DownloadSize != other.DownloadSize)
                return false;

            if (Size != other.Size)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Pointer.GetHashCode();
            if (!HasMetadata)
                return hashcode;

            hashcode ^= Hash.GetHashCode();
            hashcode ^= DownloadSize.GetHashCode();
            hashcode ^= Size.GetHashCode();

            return hashcode;
        }
        public override string ToString() {
            var sb = new StringBuilder();

            sb.AppendLine(Type);
            sb.AppendLine(Version);
            sb.AppendLine(Url);
            if (Type == LfxType.Exe)
                sb.AppendLine(Args);

            if (HasMetadata) {
                sb.AppendLine(Hash);
                sb.AppendLine(DownloadSize);
                if (Type != LfxType.File)
                    sb.AppendLine(Size);
            }

            return sb.ToString();
        }
    }

    public struct LfxContent : IEquatable<LfxContent> {
        public static bool operator ==(LfxContent lhs, LfxContent rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxContent lhs, LfxContent rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxContent content) => content.m_path;

        public static LfxContent Create(LfxInfo info, string path) => new LfxContent(info, path);

        private readonly LfxInfo m_info;
        private readonly string m_path;

        public LfxContent(LfxInfo info, string infoPath) {
            m_info = info;
            m_path = infoPath;
        }

        public string Path => m_path;

        // compose info
        public LfxInfo Info => m_info;

        // type
        public LfxType Type => m_info.Type;
        public bool IsExe => m_info.IsExe;
        public bool IsZip => m_info.IsZip;
        public bool IsFile => m_info.IsFile;

        // compose pointer
        public LfxPointer Pointer => m_info.Pointer;
        public int Version => m_info.Version;
        public Uri Url => m_info.Url;
        public LfxUrlId UrlHash => m_info.UrlHash;
        public string Args => m_info.Args;

        // metdata
        public bool HasMetadata => m_info.HasMetadata;
        public LfxMetadata? Metadata => m_info.Metadata;
        public LfxArchiveId? Hash => m_info.Hash;
        public long? DownloadSize => m_info.DownloadSize;
        public long? Size => m_info.Size;

        public override bool Equals(object obj) => obj is LfxContent ? Equals((LfxContent)obj) : false;
        public bool Equals(LfxContent other) {
            if (m_info != other.m_info)
                return false;

            if (m_path != other.m_path)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= m_info.GetHashCode();
            hashcode ^= m_path.GetHashCode();

            return hashcode;
        }
        public override string ToString() => $"{m_path}";
    }

    public struct LfxPath : IEquatable<LfxPath> {

        public struct LfxInfoPath {
            public static LfxInfoPath Create(LfxLoader loader, string infoPath) {

                if (!infoPath.PathExistsAsFile())
                    return default(LfxInfoPath);

                var info = LfxInfo.Load(infoPath);

                LfxContent content;
                if (loader.TryGetContent(info, out content))
                    info = content.Info;

                return new LfxInfoPath(
                    infoPath: infoPath,
                    info: info,
                    action: loader.GetLoadAction(info), 
                    cachePath: content.Path
                );
            }

            private readonly LfxInfo? m_info;
            private readonly string m_infoPath;
            private readonly LfxLoadAction m_action;
            private readonly string m_cachePath;

            public LfxInfoPath(
                string infoPath,
                LfxInfo info,
                LfxLoadAction action,
                string cachePath) {

                m_infoPath = infoPath;
                m_info = info;
                m_action = action;
                m_cachePath = cachePath;
            }

            public LfxInfo? Info => m_info;
            public string InfoPath => m_infoPath;
            public LfxLoadAction Action => m_action;
            public string CachePath => m_cachePath;
        }

        public static implicit operator string(LfxPath path) => path.m_path;

        public static LfxPath Create(string rootContentDir, string rootInfoDir, string path = null) {
            if (rootContentDir == null)
                throw new ArgumentNullException(nameof(rootContentDir));

            if (path == null)
                path = rootContentDir;

            LfxPath result;
            if (!TryCreate(rootContentDir, rootInfoDir, path, out result))
                throw new ArgumentException(
                    $"Path '{path}' has no lfx info.");

            return result;
        }
        public static bool TryCreate(string rootContentDir, string rootInfoDir, string path, out LfxPath lfxPath) {
            lfxPath = default(LfxPath);

            if (!path.PathIsRooted())
                return false;

            if (path == null || rootContentDir == null)
                return false;

            // special case
            if (path.ToDir().EqualPath(rootContentDir))
                path = rootContentDir;

            // too shallow?
            if (!path.IsSubPathOf(rootContentDir))
                return false;

            var relPath = rootContentDir.GetRelativePath(path);
            var infoPath = rootInfoDir.PathCombine(relPath);

            // too deep?
            if (!infoPath.PathExists() && !infoPath.GetDir().PathExists())
                return false;

            lfxPath = new LfxPath(path, infoPath);
            return true;
        }

        private readonly string m_path;
        private readonly string m_infoPath;
        private readonly LfxInfo? m_info;

        private LfxPath(
            string path,
            string infoPath) : this() {

            m_path = path;
            m_infoPath = infoPath;
            if (m_infoPath.PathExistsAsFile())
                m_info = LfxInfo.Load(infoPath);
        }
        private IEnumerable<LfxPath> AllPaths() {
            if (IsExtra)
                yield break;

            foreach (var path in Paths()) {
                yield return path;

                if (path.IsDirectory) {
                    foreach (var subPath in path.AllPaths())
                        yield return subPath;
                }
            }
        }

        // identity
        public string Name => m_path.GetPathName();
        public string Path => IsFile ? m_path : m_path.ToDir();
        public string Directory => m_path.GetDir();

        // type
        public LfxInfo? Info => m_info;
        public bool HasMetadata => m_info?.HasMetadata == true;
        public bool IsContent => m_info != null;
        public bool IsExe => m_info?.IsExe ?? false;
        public bool IsZip => m_info?.IsZip ?? false;
        public bool IsArchive => m_info?.IsArchive ?? false;
        public bool IsFile => m_info?.IsFile ?? m_path.PathExistsAsFile();
        public bool IsDirectory {
            get {
                if (m_infoPath.PathExistsAsDirectory())
                    return true;

                if (!IsContent && m_path.PathExistsAsDirectory())
                    return true;

                return false;
            }
        }

        // state
        public bool IsExtra => !m_infoPath.PathExists();
        public bool IsMissing => !m_path.PathExists();

        // metadata
        public LfxArchiveId? Hash => m_info?.Hash;
        public long? DownloadSize => m_info?.DownloadSize;
        public long? Size => m_info?.Size;

        // enumeration
        public IEnumerable<LfxPath> Paths(bool recurse = false) {

            if (recurse)
                return AllPaths();

            if (IsExtra || IsFile)
                return Enumerable.Empty<LfxPath>();

            var comparer = StringComparer.InvariantCultureIgnoreCase;

            // content directory and file names
            var result = Enumerable.Empty<string>();
            if (m_path.PathExists())
                result = m_path.GetPaths().Select(o => o.GetPathName());
            
            // info directory and file names
            if (m_infoPath.PathExists())
                result = result.Union(m_infoPath.GetPaths().Select(o => o.GetPathName()), comparer);

            // merge content/info directory/file into LfxPath
            var dis = this;
            return result.Select(o => new LfxPath(
                dis.m_path.PathCombine(o),
                dis.m_infoPath.PathCombine(o)
            )).OrderBy(o => o.Name, comparer);
        }

        // link alias to content
        public void MakeAliasOf(LfxContent content) {

            // update repo info file with metadata
            if (m_info != content.Info)
                m_infoPath.WriteAllText(content.Info.ToString());

            // create hardlink or junction
            content.Path.AliasPath(m_path);
        }

        public override bool Equals(object obj) => obj is LfxContent ? Equals((LfxContent)obj) : false;
        public bool Equals(LfxPath other) {
            if (m_path != other.m_path)
                return false;

            if (m_infoPath != other.m_infoPath)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= m_path.GetHashCode();
            hashcode ^= m_infoPath.GetHashCode();

            return hashcode;
        }
        public override string ToString() => m_path;
    }

    public struct LfxCount {
        public static LfxCount operator +(LfxCount lhs, LfxCount rhs) {
            if (lhs.Action != rhs.Action)
                throw new InvalidOperationException();
            return new LfxCount(lhs.Action, lhs.Bytes + rhs.Bytes, lhs.Count + rhs.Count);
        }

        private readonly LfxLoadAction m_action;
        private readonly long m_bytes;
        private readonly int m_count;

        public LfxCount(LfxLoadAction action) : this(action, 0, 0) { }
        public LfxCount(LfxLoadAction action, long bytes, int count = 1) {
            m_action = action;
            m_bytes = bytes;
            m_count = count;
        }

        public LfxLoadAction Action => m_action;
        public long Bytes => m_bytes;
        public int Count => m_count;

        public override string ToString() {
            return $"{m_action} {m_count} in {m_bytes.ToFileSize() ?? "?"}";
        }
    }
}
