using Git.Lfx;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace Git.Lfx.Test {

    [TestFixture]
    public class LfxBlobStoreTest : LfxTest {

        [Test]
        public static void AddTest() {
            using (var storeDir = new TempDir()) {

                // create store at storeDir
                var store = new LfxBlobStore(storeDir);
                Assert.AreEqual(storeDir.ToString(), store.Directory);
                Assert.AreEqual(0, store.Count);

                using (var file = new TempFile()) {

                    // create file
                    File.WriteAllText(file, LfxHashTest.Content);

                    // add file to store
                    var blob = store.Add(file);
                    var hash = blob.Hash;
                    Assert.IsTrue(store.Contains(hash));
                    Assert.AreEqual(1, store.Count);
                    Assert.AreEqual(blob, store.Single());

                    // get file from store
                    LfxBlob rtBlob;
                    Assert.IsTrue(store.TryGet(hash, out rtBlob));
                    Assert.AreEqual(blob, rtBlob);

                    using (var altDir = new TempDir()) {
                        // create alternate store, add file blob
                        var altStore = new LfxBlobStore(altDir);
                        var altBlob = altStore.Add(blob);

                        Assert.AreNotEqual(altBlob, blob);
                        Assert.AreEqual(blob.Hash, altBlob.Hash);
                    }
                }
            }
        }
    }
}
