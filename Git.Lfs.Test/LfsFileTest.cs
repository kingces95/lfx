using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfs.Test {

    [TestFixture]
    public static class LfsFileTest
    {
        [Test]
        public static void ArchiveLoadTest() {
            using (var dir = new TempDir()) {
                var configFilePath = dir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, LfsArchiveConfigTest.ConfigFileContent);

                var nugetDir = Path.Combine(dir, "NUnit.2.6.4");
                Directory.CreateDirectory(nugetDir);

                var lfsFilePath = Path.Combine(nugetDir, "Foo.txt");
                File.WriteAllText(lfsFilePath, LfsPointerTest.SampleContent);

                var subDir = Path.Combine(nugetDir, "subdir");
                Directory.CreateDirectory(subDir);

                var lfsSubFilePath = Path.Combine(subDir, "Foo.txt");
                File.WriteAllText(lfsSubFilePath, LfsPointerTest.SampleContent);

                var loader = LfsLoader.Create();
                var lfsConfigFile = loader.GetConfigFile(configFilePath);
                var lfsFile = loader.GetFile(lfsFilePath);
                var lfsSubFile = loader.GetFile(lfsSubFilePath);

                Assert.AreEqual(lfsConfigFile, lfsFile.ConfigFile);
                Assert.AreEqual(lfsConfigFile, lfsSubFile.ConfigFile);
            }
        }

        [Test]
        public static void CurlLoadTest() {
            using (var dir = new TempDir()) {
                var configFilePath = dir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, LfsCurlConfigTest.ConfigFileContent);

                var nugetDir = Path.Combine(dir, "tools", "nuget");
                Directory.CreateDirectory(nugetDir);

                var lfsFilePath = Path.Combine(nugetDir, "nuget.exe");
                File.WriteAllText(lfsFilePath, LfsPointerTest.SampleContent);

                var subDir = Path.Combine(nugetDir, "subdir");
                Directory.CreateDirectory(subDir);

                var loader = LfsLoader.Create();
                var lfsConfigFile = loader.GetConfigFile(configFilePath);
                var lfsFile = loader.GetFile(lfsFilePath);

                Assert.AreEqual(lfsConfigFile, lfsFile.ConfigFile);
            }
        }
    }
}
