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
        Exe
    }

    public struct LfxHash : IEquatable<LfxHash> {
        private const int Length = 64;

        public static bool operator ==(LfxHash lhs, LfxHash rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxHash lhs, LfxHash rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxHash hash) => hash.ToString();

        public static LfxHash Create(string value, Encoding encoding) {
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
        public static LfxHash Create(string path) {
            using (var file = File.OpenRead(path))
                return Create(file);
        }
        public static LfxHash Create(Stream stream) {
            return Create(SHA256.Create().ComputeHash(stream));
        }
        public static LfxHash Create(byte[] value) => new LfxHash(Hash.Create(value));

        public static LfxHash Parse(string value) => new LfxHash(Hash.Parse(value, Length));
        public static bool TryParse(string value, out LfxHash hash) {
            Hash rawHash;
            if (!Hash.TryParse(value, Length, out rawHash))
                return false;

            hash = new LfxHash(rawHash);
            return true;
        }

        private readonly Hash m_value;

        private LfxHash(Hash value) {
            m_value = value;
        }

        public byte[] Value => m_value.Value;

        public override bool Equals(object obj) => obj is LfxHash ? ((LfxHash)obj).m_value == m_value : false;
        public bool Equals(LfxHash other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
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

    public struct LfxCheckedPointer : IEquatable<LfxCheckedPointer> {
        public static bool operator ==(LfxCheckedPointer lhs, LfxCheckedPointer rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxCheckedPointer lhs, LfxCheckedPointer rhs) => !lhs.Equals(rhs);
        public static implicit operator Uri(LfxCheckedPointer pointer) => pointer.Url;
        public static implicit operator LfxHash(LfxCheckedPointer pointer) => pointer.Hash;
        public static implicit operator LfxPointer(LfxCheckedPointer pointer) => pointer.Pointer;

        public static LfxCheckedPointer Create(LfxPointer pointer, LfxHash hash) => new LfxCheckedPointer(pointer, hash);

        private readonly LfxPointer m_pointer;
        private readonly LfxHash m_hash;

        private LfxCheckedPointer(
            LfxPointer pointer,
            LfxHash hash)
            : this() {

            m_pointer = pointer;
            m_hash = hash;
        }

        public LfxPointer Pointer => m_pointer;

        public LfxPointerType Type => Pointer.Type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsFile => Type == LfxPointerType.File;

        public int Version => Pointer.Version;
        public Uri Url => Pointer.Url;
        public LfxHash Hash => m_hash;
        public string Args => Pointer.Args;

        public override bool Equals(object obj) => obj is LfxCheckedPointer ? Equals((LfxCheckedPointer)obj) : false;
        public bool Equals(LfxCheckedPointer other) {
            if (Pointer != other.Pointer)
                return false;

            if (Hash != other.Hash)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Pointer.GetHashCode();
            hashcode ^= Hash.GetHashCode();

            return hashcode;
        }
        public override string ToString() => $"{Pointer}";
    }

    [DebuggerDisplay("{Url}")]
    public struct LfxInfo : IEquatable<LfxInfo> {
        public static bool operator ==(LfxInfo lhs, LfxInfo rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxInfo lhs, LfxInfo rhs) => !lhs.Equals(rhs);

        public static LfxInfo Create(LfxCheckedPointer pointer, long size, long? contentSize = null) {
            return new LfxInfo(pointer, size, contentSize);
        }
        public static LfxInfo Create(LfxPointer pointer, string cachePath, string compressedPath = null) {
            var url = pointer.Url;
            var hash = LfxHash.Parse(Path.GetFileName(cachePath));
            var checkedPointer = LfxCheckedPointer.Create(pointer, hash);

            // file
            if (pointer.IsFile)
                return Create(checkedPointer, cachePath.GetFileSize());

            // archive
            if (compressedPath == null)
                throw new ArgumentException(
                    $"To create an archive pointer supply a compressed path.");

            var dirSize = cachePath.GetDirectorySize();
            var fileSize = compressedPath.GetFileSize();

            // exe
            if (pointer.IsExe)
                return Create(checkedPointer, fileSize, dirSize);

            // zip
            return Create(checkedPointer, fileSize, dirSize);
        }

        public static LfxInfo Load(string path) {
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

                // exe
                string args = null;
                if (type == LfxPointerType.Exe) {

                    // cmd
                    args = sr.ReadLine();
                    if (args == null)
                        throw new Exception($"LfxPointer '{path}' has no args specified.");
                }

                // pointer
                var pointer =
                    type == LfxPointerType.File ? LfxPointer.CreateFile(url) :
                    type == LfxPointerType.Zip ? LfxPointer.CreateZip(url) :
                    LfxPointer.CreateExe(url, args);

                // hash
                var hashLine = sr.ReadLine();
                LfxHash hash;
                if (!LfxHash.TryParse(hashLine, out hash))
                    throw new Exception($"LfxPointer '{path}' has unrecognized url '{urlLine}'.");

                // checked pointer
                var checkedPointer = LfxCheckedPointer.Create(pointer, hash);

                // size
                var sizeLine = sr.ReadLine();
                long size;
                if (!long.TryParse(sizeLine, out size))
                    throw new Exception($"LfxPointer '{path}' has unrecognized size '{sizeLine}'.");

                // file
                LfxInfo info = default(LfxInfo);
                if (type != LfxPointerType.File)
                    info = Create(checkedPointer, size);

                // archive
                else {

                    // content size
                    var contentSizeLine = sr.ReadLine();
                    int contentSize;
                    if (!int.TryParse(contentSizeLine, out contentSize))
                        throw new Exception($"LfxPointer '{path}' has unrecognized content size '{contentSizeLine}'.");

                    info = Create(checkedPointer, size, contentSize);
                }

                // eof
                var lastLine = sr.ReadLine();
                if (lastLine != null)
                        throw new Exception($"LfxPointer '{path}' has unrecognized line '{lastLine}'.");

                return info;
            }
        }

        private readonly LfxCheckedPointer m_pointer;
        private readonly long m_size;
        private readonly long m_contentSize;

        private LfxInfo(
            LfxCheckedPointer pointer,
            long size,
            long? contentSize = null)
            : this() {

            m_pointer = pointer;
            m_size = size;
            m_contentSize = contentSize ?? -1L;
        }

        public LfxCheckedPointer Pointer => m_pointer;

        public LfxPointerType Type => Pointer.Type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsFile => Type == LfxPointerType.File;

        public int Version => Pointer.Version;
        public Uri Url => Pointer.Url;
        public LfxHash Hash => Pointer.Hash;
        public string Args => Pointer.Args;

        public long Size => m_size;
        public long ContentSize => m_contentSize;

        public void Save(StreamWriter stream) {
            stream.WriteLine(Type);
            stream.WriteLine(Version);
            stream.WriteLine(Url);
            if (Type == LfxPointerType.Exe)
                stream.WriteLine(Args);
            stream.WriteLine(Hash);
            stream.WriteLine(Size);
            if (Type != LfxPointerType.File)
                stream.WriteLine(ContentSize);
        }

        public override bool Equals(object obj) => obj is LfxInfo ? Equals((LfxInfo)obj) : false;
        public bool Equals(LfxInfo other) {
            if (Pointer != other.Pointer)
                return false;

            if (Size != other.Size)
                return false;

            if (ContentSize != other.ContentSize)
                return false;

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;

            hashcode ^= Pointer.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= ContentSize.GetHashCode();

            return hashcode;
        }
        public override string ToString() {
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
