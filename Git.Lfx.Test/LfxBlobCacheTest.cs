using Git.Lfx.Test;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace Git.Lfx.Live.Test {

    [TestFixture]
    public class LfxCurlBlobCacheTest : LfxBlobCacheTest {

        public const string Hash = "c12d583dd1b5447ac905a334262e02718f641fca3877d0b6117fe44674072a27";
        public const int Size = 3957976;
        public static readonly Uri Url = new Uri("https://dist.nuget.org/win-x86-commandline/v3.4.4/NuGet.exe");
        public static readonly LfxPointer Pointer =
            LfxPointerTest.CreatePointer(Hash, Size, LfxIdType.File, Url);

        public const string AltHash = "af8ee5c2299a7d71f4bfefe046701af551c348b8c9f6c10302598262f16d42aa";
        public const int AltSize = 3787952;
        public static readonly Uri AltUrl = new Uri("https://dist.nuget.org/win-x86-commandline/v3.3.0/nuget.exe");
        public static readonly LfxPointer AltPointer =
            LfxPointerTest.CreatePointer(AltHash, AltSize, LfxIdType.File, AltUrl);

        [Test]
        public static void DownloadTest() {
            DownloadTest(Pointer, AltPointer);
        }
    }

    [TestFixture]
    public class LfxArchiveBlobCacheTest : LfxBlobCacheTest {

        public const string Hash = "39a9d50b6fecd1dec6f12dbf26023a6544e0ba018cfdc01b5625db6ae1f2f1f9";
        public const string Hint = "lib/nunit.framework.dll";
        public const int Size = 151552;
        public static readonly Uri Url = new Uri("http://nuget.org/api/v2/package/NUnit/2.6.4");
        public static readonly LfxPointer Pointer =
            LfxPointerTest.CreatePointer(Hash, Size, LfxIdType.Zip, Url, Hint);

        public const string AltHash = "8b2bc1c3a689c5b5426bdb86ee1a3f63c904987763c266684e440ded74278f87";
        public const string AltHint = "tools/lib/nunit.core.dll";
        public const int AltSize = 155648;
        public static readonly Uri AltUrl = new Uri("http://nuget.org/api/v2/package/NUnit.Runners/2.6.4");
        public static readonly LfxPointer AltPointer =
            LfxPointerTest.CreatePointer(AltHash, AltSize, LfxIdType.Zip, AltUrl, AltHint);

        [Test]
        public static void DownloadTest() {
            DownloadTest(Pointer, AltPointer);
        }
    }

    public class LfxBlobCacheTest : LfxTest {

        public static void DownloadTest(LfxPointer pointer, LfxPointer altPointer) {
            using (var storeDir = new TempDir()) {

                // create cache with storeDir
                var cache = new LfxCache(storeDir);

                using (var file = new TempFile()) {

                    // create file
                    File.WriteAllText(file, LfxHashTest.Content);

                    // add file to cache
                    var blob = cache.Load(pointer);
                    var count = cache.Store.Count;
                    Assert.IsTrue(count > 0);
                    var hash = blob.Hash;
                    Assert.IsTrue(cache.Contains(blob));

                    // get file from cache
                    LfxTarget rtBlob;
                    Assert.IsTrue(cache.TryGet(hash, out rtBlob));
                    Assert.AreEqual(blob, rtBlob);

                    using (var altDir = new TempDir()) {
                        // create alternate cache, promote file
                        var altCache = new LfxCache(altDir, cache);
                        Assert.AreEqual(cache, altCache.Parent);

                        // promote
                        LfxTarget altBlob;
                        Assert.IsTrue(altCache.TryGet(hash, out altBlob));

                        Assert.AreNotEqual(altBlob, blob);
                        Assert.AreEqual(blob.Hash, altBlob.Hash);
                    }

                    using (var altDir = new TempDir()) {
                        // create alternate cache, promote file
                        var altCache = new LfxCache(altDir, cache);

                        // promote
                        Assert.IsTrue(altCache.Promote(hash));
                    }

                    using (var altDir = new TempDir()) {
                        // create alternate cache, promote file
                        var altCache = new LfxCache(altDir, cache);

                        // promote all files;
                        altCache.ToArray();

                        // promote
                        LfxTarget altBlob;
                        Assert.IsTrue(altCache.TryGet(hash, out altBlob));
                        Assert.IsTrue(altCache.Contains(altBlob));
                        Assert.IsFalse(altCache.Contains(blob));
                    }

                    using (var altDir = new TempDir()) {
                        // create alternate cache, promote file
                        var altCache = new LfxCache(altDir, cache);

                        // ask child cache to download same file as parent
                        var altBlob = altCache.Load(pointer);

                        // child should contain one blob promoted from parent
                        Assert.AreNotEqual(blob, altBlob);
                        Assert.AreEqual(blob.Hash, altBlob.Hash);
                        Assert.AreEqual(count, cache.Store.Count);
                        Assert.AreEqual(1, altCache.Store.Count);

                        // load altPointer; child cache is subset of parent
                        altBlob = altCache.Load(altPointer);
                        Assert.IsTrue(altCache.Store.Count > 1);

                        var altHashes = altCache.Store.Select(o => o.Hash);
                        var parentHashes = cache.Store.Select(o => o.Hash);
                        Assert.IsTrue(!altHashes.Except(parentHashes).Any());
                    }
                }
            }
        }
    }
}
