using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfs.Test {

    [TestFixture]
    public static class LfsConfigTest
    {
        [Test]
        public static void ArchiveLoadTest() {
            using (var dir = new TempDir()) {

                // write archive config file
                var configFilePath = dir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, LfsArchiveConfigTest.ConfigFileContent);

                // crate /NUnit.2.6.4
                var nunitDir = Path.Combine(dir, "NUnit.2.6.4");
                Directory.CreateDirectory(nunitDir);

                // create /NUnit.2.6.4/lib/
                var libDirName = @"lib";
                var libDir = Path.Combine(nunitDir, libDirName);
                Directory.CreateDirectory(libDir);

                // write dummy content to /NUnit.2.6.4/lib/NUnit.dll
                var dllName = "NUnit.dll";
                var subPath = Path.Combine(libDirName, dllName);
                var nunitDllPath = Path.Combine(nunitDir, subPath);
                File.WriteAllText(nunitDllPath, LfsPointerTest.Content);

                // load file
                var pointer = LfsPointer.Create(nunitDllPath);

                // check regex expansions
                Assert.AreEqual(LfsPointerType.Archive, pointer.Type);
                Assert.AreEqual(
                    "http://nuget.org/api/v2/package/NUnit/2.6.4", 
                    pointer.Url.ToString()
                );
                Assert.AreEqual(subPath.Replace(@"\","/"), pointer.ArchiveHint);
            }
        }

        [Test]
        public static void CurlLoadTest() {
            using (var dir = new TempDir()) {

                // write curl config file
                var configFilePath = dir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, LfsCurlConfigTest.ConfigFileContent);

                // create /tools/nuget/
                var nugetDir = Path.Combine(dir, "tools", "nuget");
                Directory.CreateDirectory(nugetDir);

                // write dummy content to /tools/nuget/nuget.exe
                var lfsFilePath = Path.Combine(nugetDir, "nuget.exe");
                File.WriteAllText(lfsFilePath, LfsPointerTest.Content);

                // load file
                var pointer = LfsPointer.Create(lfsFilePath);

                // check regex expansions
                Assert.AreEqual(LfsPointerType.Curl, pointer.Type);
                Assert.AreEqual(
                    "https://dist.nuget.org/win-x86-commandline/v3.4.4/nuget.exe",
                    pointer.Url.ToString()
                );
            }
        }
    }
}
