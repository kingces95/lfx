using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace Git.Lfs.Test {

    [TestFixture]
    public class LfsBlobStoreTest : LfsTest {

        [Test]
        public static void CurlParseTest() {
            using (var storeDir = new TempDir()) {
                var store = new LfsBlobStore(storeDir);
                Assert.AreEqual(storeDir.ToString(), store.Directory);
                Assert.AreEqual(0, store.Count);

                using (var file = new TempFile()) {
                    File.WriteAllText(file, LfsHashTest.Content);

                    var blob = store.Add(file);
                    var hash = blob.Hash;
                    Assert.IsTrue(store.Contains(hash));
                    Assert.AreEqual(1, store.Count);
                    Assert.AreEqual(blob, store.Files().Single());

                    LfsBlob rtBlob;
                    Assert.IsTrue(store.TryGet(hash, out rtBlob));
                    Assert.AreEqual(blob, rtBlob);

                    using (var altDir = new TempDir()) {
                        var altStore = new LfsBlobStore(altDir);
                        var altBlob = altStore.Add(blob);

                        Assert.AreNotEqual(altBlob, blob);
                        Assert.AreEqual(blob.Hash, altBlob.Hash);
                    }
                }
            }
        }
    }
}
