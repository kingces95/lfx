using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Lfs.Test {

    public abstract class LfsPointerTest {
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

        public static readonly Uri SampleVersion = LfsPointer.Version1Uri;
        public static readonly LfsHashMethod SampleMethod = LfsHashMethod.Sha256;
        public static readonly string SampleContent = LfsHashTest.SampleContent;
        public static readonly string SampleHashValue = LfsHashTest.SampleHashValue;
        public static readonly int ByteOrderMarkLength = 3;
        public static readonly int SampleSize = LfsHashTest.SampleContent.Length + ByteOrderMarkLength;
        public static readonly int OtherSize = 123120;
        public static readonly Uri SampleUrl = @"http://file.server.com/foo/bar.file".ToUrl();
        public static readonly string SampleHint = "/foo/bar";

        public static readonly string OptherPointerText =
            CreatePointerText(SampleVersion, SampleMethod, SampleHashValue, OtherSize);

        public static readonly string SampleSimplePointerText =
            CreatePointerText(SampleVersion, SampleMethod, SampleHashValue, SampleSize);

        public static readonly string SampleCurlPointerText =
            CreatePointerText(SampleVersion, SampleMethod, SampleHashValue, SampleSize, 
                LfsPointerType.Curl, SampleUrl);

        public static readonly string SampleArchivePointerText =
            CreatePointerText(SampleVersion, SampleMethod, SampleHashValue, SampleSize,
                LfsPointerType.Archive, SampleUrl, SampleHint);

        public static void TestSamplePointer(LfsPointer pointer, LfsPointerType type) {

            var pointerText = type == LfsPointerType.Simple ? SampleSimplePointerText :
                type == LfsPointerType.Curl ? SampleCurlPointerText :
                SampleArchivePointerText;

            Assert.AreEqual(type, pointer.Type);
            Assert.AreEqual(SampleVersion, pointer.Version);
            Assert.AreEqual(SampleMethod, pointer.HashMethod);
            Assert.AreEqual(SampleHashValue, pointer.HashValue);
            Assert.AreEqual(LfsHash.Parse(SampleHashValue), pointer.Hash);
            Assert.AreEqual(SampleSize, pointer.Size);

            if (type == LfsPointerType.Simple) {
                Assert.AreEqual(null, pointer.Url);
                Assert.AreEqual(null, pointer.Hint);
            }

            if (type == LfsPointerType.Curl) {
                Assert.AreEqual(SampleUrl, pointer.Url);
                Assert.AreEqual(null, pointer.Hint);
            }

            if (type == LfsPointerType.Archive) {
                Assert.AreEqual(SampleUrl, pointer.Url);
                Assert.AreEqual(SampleHint, pointer.Hint);
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


            var otherPointer = LfsPointer.Parse(OptherPointerText);
            Assert.AreNotEqual(pointer, otherPointer);
            Assert.AreNotEqual(pointer.GetHashCode(), otherPointer.GetHashCode());
        }
    }

    [TestFixture]
    public sealed class LfsSimplePointerTest : LfsPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SampleSimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            TestSamplePointer(pointer, LfsPointerType.Simple);

            Console.Write(pointer);
        }

        [Test]
        public static void LoadTest() {

            using (var contentFile = new TempFile()) {
                File.WriteAllText(contentFile, LfsHashTest.SampleContent, Encoding.UTF8);
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

            var pointerText = SampleSimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            var curlPointerParsed = LfsPointer.Parse(SampleCurlPointerText);
            TestSamplePointer(curlPointerParsed, LfsPointerType.Curl);

            var curlPointer = pointer.AddUrl(SampleUrl);
            TestSamplePointer(curlPointer, LfsPointerType.Curl);

            Assert.AreEqual(curlPointerParsed, curlPointer);

            Console.Write(curlPointer);
        }
    }

    [TestFixture]
    public sealed class LfsArchivePointerTest : LfsPointerTest {

        [Test]
        public static void ParseTest() {

            var pointerText = SampleSimplePointerText;
            var pointer = LfsPointer.Parse(pointerText);

            var archivePointerParsed = LfsPointer.Parse(SampleArchivePointerText);
            TestSamplePointer(archivePointerParsed, LfsPointerType.Archive);

            var archivePointer = pointer.AddArchive(SampleUrl, SampleHint);
            TestSamplePointer(archivePointer, LfsPointerType.Archive);

            Assert.AreEqual(archivePointerParsed, archivePointer);

            Console.Write(archivePointer);

        }
    }
}
