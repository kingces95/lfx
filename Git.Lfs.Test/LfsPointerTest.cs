using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Lfs.Test {

    public abstract class LfsPointerTest {
        public static LfsPointer CreatePointer(
            string hashValue,
            int size,
            LfsPointerType? type = null,
            Uri url = null,
            string hint = null) {

            var pointerText = CreatePointerText(
                Version, LfsHashMethod.Sha256,
                hashValue, size,
                type, url, hint
            );

            return LfsPointer.Parse(pointerText);
        }
        public static string CreatePointerText(
            Uri version,
            LfsHashMethod hashMethod,
            string hashValue,
            int size,
            LfsPointerType? type = null,
            Uri url = null,
            string hint = null) {

            return CreatePointerText(
                version.ToString(),
                hashMethod.ToString().ToLower(),
                hashValue,
                size.ToString(),
                type?.ToString().ToLower(),
                url?.ToString(),
                hint
            );
        }
        public static string CreatePointerText(
            string version,
            string hashMethod,
            string hashValue,
            string size,
            string type,
            string url,
            string hint) {

            return string.Join("\n", new object[] {
                $"version {version}",
                hint != null ? $"hint {hint}" : null,
                $"oid {hashMethod}:{hashValue}",
                $"size {size}",
                type != null ? $"type {type}" : null,
                url != null ? $"url {url}" : null,
                string.Empty
            }.Where(o => o != null).ToArray());
        }

        public static readonly Uri Version = LfsPointer.Version1Uri;
        public static readonly LfsHashMethod Method = LfsHashMethod.Sha256;
        public static readonly string Content = LfsHashTest.Content;
        public static readonly string HashValue = LfsHashTest.HashValue;
        public static readonly int ByteOrderMarkLength = 3;
        public static readonly int Size = LfsHashTest.Content.Length + ByteOrderMarkLength;
        public static readonly int OtherSize = 123120;
        public static readonly Uri Url = @"http://file.server.com/foo/bar.file".ToUrl();
        public static readonly string Hint = "/foo/bar";

        public static readonly string OtherPointerText =
            CreatePointerText(Version, Method, HashValue, OtherSize);

        public static readonly string SimplePointerText =
            CreatePointerText(Version, Method, HashValue, Size);

        public static readonly string CurlPointerText =
            CreatePointerText(Version, Method, HashValue, Size, 
                LfsPointerType.Curl, Url);

        public static readonly string ArchivePointerText =
            CreatePointerText(Version, Method, HashValue, Size,
                LfsPointerType.Archive, Url, Hint);

        public static void TestSamplePointer(LfsPointer pointer, LfsPointerType type) {

            var pointerText = type == LfsPointerType.Simple ? SimplePointerText :
                type == LfsPointerType.Curl ? CurlPointerText :
                ArchivePointerText;

            Assert.AreEqual(type, pointer.Type);
            Assert.AreEqual(Version, pointer.Version);
            Assert.AreEqual(Method, pointer.HashMethod);
            Assert.AreEqual(HashValue, pointer.HashValue);
            Assert.AreEqual(LfsHash.Parse(HashValue), pointer.Hash);
            Assert.AreEqual(Size, pointer.Size);

            if (type == LfsPointerType.Simple) {
                Assert.AreEqual(null, pointer.Url);
                Assert.AreEqual(null, pointer.Hint);
            }

            if (type == LfsPointerType.Curl) {
                Assert.AreEqual(Url, pointer.Url);
                Assert.AreEqual(null, pointer.Hint);
            }

            if (type == LfsPointerType.Archive) {
                Assert.AreEqual(Url, pointer.Url);
                Assert.AreEqual(Hint, pointer.Hint);
            }

            Assert.AreEqual(
                pointerText,
                pointer.ToString()
            );

            // round-trip
            using (var tempFile = new TempFile()) {
                File.WriteAllText(tempFile, pointer.ToString());

                using (var pointerFile = new StreamReader(tempFile)) {
                    var pointerRoundTrip = LfsPointer.Parse(pointerFile);
                    Assert.IsTrue(pointer.Equals(pointerRoundTrip));
                    Assert.IsTrue(pointer.Equals((object)pointerRoundTrip));
                    Assert.AreEqual(pointer.ToString(), pointerRoundTrip.ToString());
                    Assert.AreEqual(pointer, pointerRoundTrip);
                    Assert.AreEqual(pointer.GetHashCode(), pointer.GetHashCode());
                }
            }


            var otherPointer = LfsPointer.Parse(OtherPointerText);
            Assert.AreNotEqual(pointer, otherPointer);
            Assert.AreNotEqual(pointer.GetHashCode(), otherPointer.GetHashCode());
        }
    }

    [TestFixture]
    public sealed class LfsSimplePointerTest : LfsPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            TestSamplePointer(pointer, LfsPointerType.Simple);

            Console.Write(pointer);
        }

        [Test]
        public static void LoadTest() {

            using (var contentFile = new TempFile()) {
                File.WriteAllText(contentFile, LfsHashTest.Content, Encoding.UTF8);
                var pointer = LfsPointer.Create(contentFile);
                TestSamplePointer(pointer, LfsPointerType.Simple);

                using (var pointerFile = new TempFile()) {
                    File.WriteAllText(pointerFile, pointer.ToString());
                    pointer = LfsPointer.Load(pointerFile);
                    TestSamplePointer(pointer, LfsPointerType.Simple);
                }
            }
        }
    }

    [TestFixture]
    public sealed class LfsCurlPointerTest : LfsPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            var curlPointerParsed = LfsPointer.Parse(CurlPointerText);
            TestSamplePointer(curlPointerParsed, LfsPointerType.Curl);

            var curlPointer = pointer.AddUrl(Url);
            TestSamplePointer(curlPointer, LfsPointerType.Curl);

            Assert.AreEqual(curlPointerParsed, curlPointer);

            Console.Write(curlPointer);
        }
    }

    [TestFixture]
    public sealed class LfsArchivePointerTest : LfsPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            var archivePointerParsed = LfsPointer.Parse(ArchivePointerText);
            TestSamplePointer(archivePointerParsed, LfsPointerType.Archive);

            var archivePointer = pointer.AddArchive(Url, Hint);
            TestSamplePointer(archivePointer, LfsPointerType.Archive);

            Assert.AreEqual(archivePointerParsed, archivePointer);

            Console.Write(archivePointer);

        }
    }
}
