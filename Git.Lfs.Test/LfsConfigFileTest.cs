using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfs.Test {

    [TestFixture]
    public class LfsArchiveConfigTest : LfsTest {

        public static readonly string Url = "http://nuget.org/api/v2/package/${id}/${ver}";
        public static readonly string Regex = @"^((?<id>.*?)[.])(?=\\d)(?<ver>[^/]*)/(?<path>.*)$";
        public static readonly string Hint = "${path}";
        public static readonly string ConfigFileContent =
            $"[lfsEx]{Nl}" +
            $"{Tab}type = archive{Nl}" +
            $"{Tab}url = {Url}{Nl}" +
            $"{Tab}hint = {Hint}{Nl}" +
            $"{Tab}regex = {Regex}{Nl}" +
            $"{Nl}";

        [Test]
        public static void ArchiveParseTest() {
            using (var tempDir = new TempDir()) {

                // write config file
                var configFilePath = tempDir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, ConfigFileContent);

                // load config file
                var loader = LfsLoader.Create();
                var configFile = loader.GetConfigFile(configFilePath);

                // print keys
                foreach (var config in configFile)
                    Console.WriteLine($"{config.Key}: {config.Value}");

                Assert.AreEqual(configFilePath.ToString(), configFile.Path);
                Assert.AreEqual(
                    Path.GetDirectoryName(configFilePath) + Path.DirectorySeparatorChar, 
                    configFile.Directory
                );
                Assert.AreEqual(LfsPointerType.Archive, configFile.Type);
                Assert.AreEqual(Url, configFile.Url);
                Assert.AreEqual(Hint, configFile.Hint);
            }
        }
    }

    [TestFixture]
    public class LfsCurlConfigTest : LfsTest {

        public static readonly string Url = "https://dist.nuget.org/win-x86-commandline/v3.4.4/${file}";
        public static readonly string Regex = @"^(.*/)?(?<file>.*)$";
        public static readonly string ConfigFileContent =
            $"[lfsEx]{Nl}" +
            $"{Tab}type = curl{Nl}" +
            $"{Tab}url = {Url}{Nl}" +
            $"{Tab}regex = {Regex}{Nl}" +
            $"{Nl}";

        [Test]
        public static void CurlParseTest() {
            using (var tempDir = new TempDir()) {
                var configFilePath = tempDir + LfsConfigFile.FileName;
                File.WriteAllText(configFilePath, ConfigFileContent);

                var loader = LfsLoader.Create();
                var configFile = loader.GetConfigFile(configFilePath);

                foreach (var config in configFile)
                    Console.WriteLine($"{config.Key}: {config.Value}");

                Assert.AreEqual(configFilePath.ToString(), configFile.Path);
                Assert.AreEqual(
                    Path.GetDirectoryName(configFilePath) + Path.DirectorySeparatorChar,
                    configFile.Directory
                );
                Assert.AreEqual(LfsPointerType.Curl, configFile.Type);
                Assert.AreEqual(Url, configFile.Url);
            }
        }
    }
}
