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

                var nunitDir = Path.Combine(dir, "NUnit.2.6.4");
                Directory.CreateDirectory(nunitDir);

                var licenseName = "License.txt";
                var lfsFilePath = Path.Combine(nunitDir, licenseName);
                File.WriteAllText(lfsFilePath, LfsPointerTest.Content);

                var libDirName = @"lib";
                var libDir = Path.Combine(nunitDir, libDirName);
                Directory.CreateDirectory(libDir);

                var dllName = "NUnit.dll";
                var subPath = Path.Combine(libDirName, dllName);
                var lfsSubFilePath = Path.Combine(nunitDir, subPath);
                File.WriteAllText(lfsSubFilePath, LfsPointerTest.Content);

                var loader = LfsLoader.Create();
                var lfsConfigFile = loader.GetConfigFile(configFilePath);
                var lfsFile = loader.GetFile(lfsFilePath);
                var lfsSubFile = loader.GetFile(lfsSubFilePath);

                Assert.AreEqual(lfsConfigFile, lfsFile.ConfigFile);
                Assert.AreEqual(licenseName, lfsFile.Hint);

                Assert.AreEqual(lfsConfigFile, lfsSubFile.ConfigFile);
                Assert.AreEqual(subPath.Replace(@"\","/"), lfsSubFile.Hint);
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
                File.WriteAllText(lfsFilePath, LfsPointerTest.Content);

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
