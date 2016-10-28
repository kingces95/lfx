using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Lfx.Test {

    public abstract class LfxPointerTest {
        public static LfxPointer CreatePointer(
            string hashValue,
            int size,
            LfxIdType? type = null,
            Uri url = null,
            string hint = null) {

            var pointerText = CreatePointerText(
                Version, LfxHashMethod.Sha256,
                hashValue, size,
                type, url, hint
            );

            return LfxPointer.Parse(pointerText);
        }
        public static string CreatePointerText(
            Uri version,
            LfxHashMethod hashMethod,
            string hashValue,
            int size,
            LfxIdType? type = null,
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

        public static readonly Uri Version = LfxPointer.Version1Uri;
        public static readonly LfxHashMethod Method = LfxHashMethod.Sha256;
        public static readonly string Content = LfxHashTest.Content;
        public static readonly string HashValue = LfxHashTest.HashValue;
        public static readonly int ByteOrderMarkLength = 3;
        public static readonly int Size = LfxHashTest.Content.Length + ByteOrderMarkLength;
        public static readonly int OtherSize = 123120;
        public static readonly Uri Url = @"http://file.server.com/foo/bar.file".ToUrl();
        public static readonly string ArchiveHint = "/foo/bar";

        public static readonly string OtherPointerText =
            CreatePointerText(Version, Method, HashValue, OtherSize);

        public static readonly string SimplePointerText =
            CreatePointerText(Version, Method, HashValue, Size);

        public static readonly string CurlPointerText =
            CreatePointerText(Version, Method, HashValue, Size, 
                LfxIdType.File, Url);

        public static readonly string ArchivePointerText =
            CreatePointerText(Version, Method, HashValue, Size,
                LfxIdType.Zip, Url, ArchiveHint);

        public static void TestSamplePointer(LfxPointer pointer, LfxIdType type) {

            var pointerText = type == LfxIdType.Simple ? SimplePointerText :
                type == LfxIdType.File ? CurlPointerText :
                ArchivePointerText;

            Assert.AreEqual(type, pointer.Type);
            Assert.AreEqual(Version, pointer.Version);
            Assert.AreEqual(Method, pointer.HashMethod);
            Assert.AreEqual(HashValue, pointer.HashValue);
            Assert.AreEqual(LfxHash.Parse(HashValue), pointer.Hash);
            Assert.AreEqual(Size, pointer.Size);

            if (type == LfxIdType.Simple) {
                Assert.AreEqual(null, pointer.Url);
                Assert.AreEqual(null, pointer.ArchiveHint);
            }

            if (type == LfxIdType.File) {
                Assert.AreEqual(Url, pointer.Url);
                Assert.AreEqual(null, pointer.ArchiveHint);
            }

            if (type == LfxIdType.Zip) {
                Assert.AreEqual(Url, pointer.Url);
                Assert.AreEqual(ArchiveHint, pointer.ArchiveHint);
            }

            Assert.AreEqual(
                pointerText,
                pointer.ToString()
            );

            // round-trip
            using (var tempFile = new TempFile()) {
                File.WriteAllText(tempFile, pointer.ToString());

                using (var pointerFile = new StreamReader(tempFile)) {
                    var pointerRoundTrip = LfxPointer.Parse(pointerFile);
                    Assert.IsTrue(pointer.Equals(pointerRoundTrip));
                    Assert.IsTrue(pointer.Equals((object)pointerRoundTrip));
                    Assert.AreEqual(pointer.ToString(), pointerRoundTrip.ToString());
                    Assert.AreEqual(pointer, pointerRoundTrip);
                    Assert.AreEqual(pointer.GetHashCode(), pointer.GetHashCode());
                }
            }


            var otherPointer = LfxPointer.Parse(OtherPointerText);
            Assert.AreNotEqual(pointer, otherPointer);
            Assert.AreNotEqual(pointer.GetHashCode(), otherPointer.GetHashCode());
        }
    }

    [TestFixture]
    public sealed class LfxSimplePointerTest : LfxPointerTest {

        [Test]
        public static void ToLowerFirstTest() {
            Assert.AreEqual("", "".ToLowerFirst());
            Assert.AreEqual("a", "a".ToLowerFirst());
            Assert.AreEqual("a", "A".ToLowerFirst());
            Assert.AreEqual("ab", "Ab".ToLowerFirst());
            Assert.AreEqual("aB", "AB".ToLowerFirst());
        }

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfxPointer.Parse(pointerText);

            TestSamplePointer(pointer, LfxIdType.Simple);

            Console.Write(pointer);
        }

        [Test]
        public static void LoadTest() {

            using (var contentFile = new TempFile()) {
                File.WriteAllText(contentFile, LfxHashTest.Content, Encoding.UTF8);
                var pointer = LfxPointer.Create(contentFile);
                TestSamplePointer(pointer, LfxIdType.Simple);

                using (var pointerFile = new TempFile()) {
                    File.WriteAllText(pointerFile, pointer.ToString());
                    pointer = LfxPointer.Load(pointerFile);
                    TestSamplePointer(pointer, LfxIdType.Simple);
                }
            }
        }
    }

    [TestFixture]
    public sealed class LfxCurlPointerTest : LfxPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfxPointer.Parse(pointerText);

            var curlPointerParsed = LfxPointer.Parse(CurlPointerText);
            TestSamplePointer(curlPointerParsed, LfxIdType.File);

            var curlPointer = pointer.AddUrl(Url);
            TestSamplePointer(curlPointer, LfxIdType.File);

            Assert.AreEqual(curlPointerParsed, curlPointer);

            Console.Write(curlPointer);
        }
    }

    [TestFixture]
    public sealed class LfxArchivePointerTest : LfxPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SimplePointerText;
            var pointer = LfxPointer.Parse(pointerText);

            var archivePointerParsed = LfxPointer.Parse(ArchivePointerText);
            TestSamplePointer(archivePointerParsed, LfxIdType.Zip);

            var archivePointer = pointer.AddArchive(Url, ArchiveHint);
            TestSamplePointer(archivePointer, LfxIdType.Zip);

            Assert.AreEqual(archivePointerParsed, archivePointer);

            Console.Write(archivePointer);

        }
    }
}
