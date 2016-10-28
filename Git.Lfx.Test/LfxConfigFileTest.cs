using Git.Lfx;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfx.Test {

    [TestFixture]
    public class LfxArchiveConfigTest : LfxTest {

        public static readonly string Url = "http://nuget.org/api/v2/package/${id}/${ver}";
        public static readonly string Regex = @"^((?<id>.*?)[.])(?=\\d)(?<ver>[^/]*)/(?<path>.*)$";
        public static readonly string Hint = "${path}";
        public static readonly string ConfigFileContent =
            $"[lfx]{Nl}" +
            $"{Tab}type = archive{Nl}" +
            $"{Tab}url = {Url}{Nl}" +
            $"{Tab}hint = {Hint}{Nl}" +
            $"{Tab}pattern = {Regex}{Nl}" +
            $"{Nl}";

        [Test]
        public static void ArchiveParseTest() {
            using (var tempDir = new TempDir()) {

                // write config file
                var configFilePath = tempDir + LfxConfigFile.FileName;
                File.WriteAllText(configFilePath, ConfigFileContent);

                // load config file
                var configFile = LfxConfigFile.Load(configFilePath);

                // print keys
                foreach (var configValue in configFile)
                    Console.WriteLine($"{configValue.Key}: {configValue.Value}");

                Assert.AreEqual(configFilePath.ToString(), configFile.Path);
                Assert.AreEqual(LfxIdType.Zip, configFile.Type);
                Assert.AreEqual(Url, configFile.Url);
                Assert.AreEqual(Hint, configFile.Hint);
            }
        }
    }

    [TestFixture]
    public class LfxCurlConfigTest : LfxTest {

        public static readonly string Url = "https://dist.nuget.org/win-x86-commandline/v3.4.4/${file}";
        public static readonly string Regex = @"^(.*/)?(?<file>.*)$";
        public static readonly string ConfigFileContent =
            $"[lfx]{Nl}" +
            $"{Tab}type = curl{Nl}" +
            $"{Tab}url = {Url}{Nl}" +
            $"{Tab}pattern = {Regex}{Nl}" +
            $"{Nl}";

        [Test]
        public static void CurlParseTest() {
            using (var tempDir = new TempDir()) {
                var configFilePath = tempDir + LfxConfigFile.FileName;
                File.WriteAllText(configFilePath, ConfigFileContent);

                var configFile = LfxConfigFile.Load(configFilePath);

                foreach (var configValue in configFile)
                    Console.WriteLine($"{configValue.Key}: {configValue.Value}");

                Assert.AreEqual(configFilePath.ToString(), configFile.Path);
                Assert.AreEqual(LfxIdType.File, configFile.Type);
                Assert.AreEqual(Url, configFile.Url);
            }
        }
    }
}
