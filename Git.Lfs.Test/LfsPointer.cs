using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Lfs.Test
{
    [TestFixture]
    public static class LfsPointerTest
    {
        [Test]
        public static void ValidPointer() {
            var pointerText = string.Join("\n", new object[] {
                "version https://git-lfs.github.com/spec/v1",
                "oid sha256:f347f5219235367aa21d98488ec7883301465a18ed691a862768a3dabfb18ed8",
                "size 151555",
                string.Empty
            });

            var pointer = LfsPointer.Parse(pointerText);

            Assert.AreEqual(
                pointerText,
                pointer.ToString()
            );

            Console.Write(pointerText);
        }

        [Test]
        public static void AugmentPointer() {
            var file = @"http://file.server.com/foo/bar.file".ToUrl();

            var pointerText = string.Join("\n", new object[] {
                "version https://git-lfs.github.com/spec/v1",
                "oid sha256:f347f5219235367aa21d98488ec7883301465a18ed691a862768a3dabfb18ed8",
                "size 151555",
                string.Empty
            });

            var pointer = LfsPointer.Parse(pointerText);
            pointer = pointer.AddUrl(file);

            Assert.AreEqual(
                pointer.ToString(),
                LfsPointer.Parse(pointer.ToString()).ToString()
            );

            Assert.AreEqual(pointer.Url, file);
            Assert.IsFalse(pointer.ToString().EndsWith("\n\n"));

            Console.Write(pointer);
        }
    }
}
