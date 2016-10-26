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
            return new LfxPointer(LfxPointerType.File) {
                m_version = CurrentVersion,
                m_url = url,
            };
        }
        public static LfxPointer CreateFile(int version, Uri url, int size, LfxHash hash, string name, DateTime timeStamp) {
            return new LfxPointer(LfxPointerType.File) {
                m_version = version,
                m_url = url,
                m_size = size,
                m_hash = hash,
                m_name = name,
                m_timeStamp = timeStamp
            };
        }
        public static LfxPointer CreateExe(int version, Uri url, int size, LfxHash hash, int contentSize, string args) {
            return new LfxPointer(LfxPointerType.Exe) {
                m_version = version,
                m_url = url,
                m_size = size,
                m_hash = hash,
                m_args = args,
            };
        }
        public static LfxPointer CreateZip(int version, Uri url, int size, LfxHash hash, int contentSize) {
            return new LfxPointer(LfxPointerType.Zip) {
                m_version = version,
                m_url = url,
                m_size = size,
                m_hash = hash,
            };
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
                if (!int.TryParse(versionLine, out version) || version != CurrentVersion)
                    throw new Exception($"LfxPointer '{path}' has unrecognized version '{versionLine}'.");

                // url
                var urlLine = sr.ReadLine();
                Uri url;
                if (!Uri.TryCreate(urlLine, UriKind.Absolute, out url))
                    throw new Exception($"LfxPointer '{path}' has unrecognized url '{urlLine}'.");

                // size
                var sizeLine = sr.ReadLine();
                int size;
                if (!int.TryParse(sizeLine, out size))
                    throw new Exception($"LfxPointer '{path}' has unrecognized size '{sizeLine}'.");

                // hash
                var hashLine = sr.ReadLine();
                LfxHash hash;
                if (!LfxHash.TryParse(hashLine, out hash))
                    throw new Exception($"LfxPointer '{path}' has unrecognized url '{urlLine}'.");

                LfxPointer pointer = default(LfxPointer);

                // file
                if (type == LfxPointerType.File) {

                    // name
                    var name = sr.ReadLine();
                    if (string.IsNullOrEmpty(name))
                        throw new Exception($"LfxPointer '{path}' has no name specified.");

                    // timeStamp
                    var timeStampLine = sr.ReadLine();
                    DateTime timeStamp;
                    if (!DateTime.TryParse(timeStampLine, out timeStamp))
                        throw new Exception($"LfxPointer '{path}' has unrecognized timestamp '{timeStamp}'.");

                    pointer = CreateFile(version, url, size, hash, name, timeStamp);
                } 
                
                // archive
                else {

                    // content size
                    var contentSizeLine = sr.ReadLine();
                    int contentSize;
                    if (!int.TryParse(contentSizeLine, out contentSize))
                        throw new Exception($"LfxPointer '{path}' has unrecognized content size '{contentSizeLine}'.");

                    // zip
                    if (type == LfxPointerType.Zip) {
                        pointer = CreateZip(version, url, size, hash, contentSize);
                    }

                    // exe
                    else if (type == LfxPointerType.Exe) {

                        // cmd
                        var cmd = sr.ReadLine();
                        if (cmd == null)
                            throw new Exception($"LfxPointer '{path}' has no cmd specified.");

                        pointer = CreateExe(version, url, size, hash, contentSize, cmd);
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
        private int m_version;
        private Uri m_url;
        private int m_size;
        private LfxHash m_hash;
        private string m_args;
        private string m_name;
        private DateTime m_timeStamp;

        private LfxPointer(LfxPointerType type) : this() {
            m_type = type;
        }

        public LfxPointerType Type => m_type;
        public bool IsExe => Type == LfxPointerType.Exe;
        public bool IsZip => Type == LfxPointerType.Zip;
        public bool IsFile => Type == LfxPointerType.File;

        public int Size => m_size;
        public LfxHash Hash => m_hash;
        public int Version => m_version; 
        public Uri Url => m_url;
        public string Args => m_args;
        public string Name => m_name;
        public DateTime TimeStamp => m_timeStamp;

        public void Save(string path) {
            using (var sw = new StreamWriter(path)) {
                sw.WriteLine(Type);
                sw.WriteLine(Version);
                sw.WriteLine(Url);
                sw.WriteLine(Size);
                sw.WriteLine(Hash);

                if (Type == LfxPointerType.Exe) {
                    sw.WriteLine(Args);
                }

                if (Type == LfxPointerType.File) {
                    sw.WriteLine(Name);
                    sw.WriteLine(TimeStamp.ToString("O"));
                }
            }
        }

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxPointer other) {
            if (Type != other.Type)
                return false;

            if (Version != other.Version)
                return false;

            if (Size != other.Size)
                return false;

            if (Url != other.Url)
                return false;

            if (Type == LfxPointerType.Exe) {

                if (Args != other.Args)
                    return false;
            }

            else if (Type == LfxPointerType.File) {

                if (Name != other.m_name)
                    return false;

                if (TimeStamp != other.TimeStamp)
                    return false;
            }

            return true;
        }
        public override int GetHashCode() {
            var hashcode = 0;
            hashcode ^= Type.GetHashCode();
            hashcode ^= Version.GetHashCode();
            hashcode ^= Size.GetHashCode();
            hashcode ^= Url.GetHashCode();

            if (Type == LfxPointerType.Exe)
                hashcode ^= Args.GetHashCode();

            else if (Type == LfxPointerType.File) {
                hashcode ^= Name.GetHashCode();
                hashcode ^= TimeStamp.GetHashCode();
            }

            return hashcode;
        }
        public override string ToString() => m_url.ToString();
    }
}
