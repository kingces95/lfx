using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Git.Lfs.Test {

    [TestFixture]
    public sealed class LfsHashTest {
        public static readonly string Content = "Hello World!";
        public static readonly string HashValue =
            "3b0be410c0102ad989fe4f64610c2228f2f42f06c08139517037900d2906eb9d";

        public static readonly string OtherHashValue = 
            "0347f5219235367aa21d98488ec7883301465a18ed691a862768a3dabfb18ed8";

        public static readonly string UpperHashValue = HashValue.ToUpper();

        [Test]
        public static void HashTest() {
            var hash = LfsHash.Compute(Content, Encoding.UTF8);
            var sameHash = LfsHash.Compute(Content, Encoding.UTF8);
            Assert.AreEqual(hash.ToString(), sameHash.ToString());
            Assert.AreEqual(HashValue, hash.ToString());
        }

        [Test]
        public static void ParseTest() {
            var hash = LfsHash.Parse(UpperHashValue);
            Assert.AreEqual(HashValue, hash.ToString());
            Assert.AreEqual(HashValue, (string)hash);
            Assert.AreEqual(HashValue, hash.ToString());
            Assert.IsTrue(LfsHash.Parse(UpperHashValue).Equals((object)hash));

            var otherHash = LfsHash.Parse(OtherHashValue);
            Assert.IsFalse(HashValue.Equals((object)otherHash));
            Assert.AreNotEqual(hash, otherHash);
            Assert.AreNotEqual(hash.GetHashCode(), otherHash.GetHashCode());
        }

        [Test]
        public static void LoadTest() {
            using (var tempFile = new TempFile()) {
                File.WriteAllText(tempFile, Content, Encoding.UTF8);
                var hash = LfsHash.Compute(tempFile);
                var sampleHash = LfsHash.Compute(Content, Encoding.UTF8);
                Assert.AreEqual(sampleHash, hash);
                Assert.IsTrue(sampleHash.Value.SequenceEqual(hash.Value));
            }
        }
    }
}
