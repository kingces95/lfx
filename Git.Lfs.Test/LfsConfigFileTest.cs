using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfs.Test {

    [TestFixture]
    public static class LfsConfigTest
    {
        private static string Nl = "\n";
        private static string Tab = "\t";

        private static string SampleUrl = "http://nuget.org/api/v2/package/${id}/${ver}";
        private static string SampleRegex = @"^((?<id>.*?)[.])(?=\\d)(?<ver>[^/]*)/(?<path>.*)$";
        private static string SampleHint = "{path}";
        private static string LfsConfigFile =
            $"[lfsEx]{Nl}" +
            $"{Tab}type = archive{Nl}" +
            $"{Tab}url = {SampleUrl}{Nl}" +
            $"{Tab}hint = {SampleHint}{Nl}" +
            $"{Tab}regex = {SampleRegex}{Nl}" +
            $"{Nl}";

        [Test]
        public static void ParseTest() {
            using (var configFilePath = TempFile.Create(LfsConfigFile)) {
                var loader = new LfsLoader();
                var configFile = loader.GetConfigFile(configFilePath);

                foreach (var config in configFile)
                    Console.WriteLine($"{config.Key}: {config.Value}");

                Assert.AreEqual(configFilePath.ToString(), configFile.Path);
                Assert.AreEqual(
                    Path.GetDirectoryName(configFilePath) + Path.DirectorySeparatorChar, 
                    configFile.Directory
                );
                Assert.AreEqual(LfsPointerType.Archive, configFile.Type);
                Assert.AreEqual(SampleUrl, configFile.Url);
                Assert.AreEqual(SampleHint, configFile.Hint);
            }
        }
    }
}
