using Git.Lfx.Test;
using Lfx;
using NUnit.Framework;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Git.Lfx.Test.Extensions;

namespace Git.Lfx.Live.Test {

    [TestFixture]
    public class EndToEndExeTest : LfxTest {
        public const string PortableGit = "https://github.com/git-for-windows/git/releases/download/v2.10.0.windows.1/PortableGit-2.10.0-32-bit.7z.exe";
        public const string Args = "-y -gm2";

        [Test]
        public static void DownloadAndSelfExtractTest() {
            var url = new Uri(PortableGit);
            using (var dir = url.DownloadAndSelfExtract("-y -gm2"))
                Assert.IsTrue(Directory.EnumerateFiles(dir).Any());
        }

        [Test]
        public static void ManualTest() {
            Console.WriteLine($"currentDir: {Environment.CurrentDirectory}");

            var url = new Uri(PortableGit);
            using (var dir = url.DownloadAndSelfExtract("-y -gm2")) {
            //using (var dir = new TempCurDir(@"C:\Users\kingc\Downloads\PortableGit")) {

                using (var tempDir = new TempDir()) {
                    var tempDirString = (string)tempDir;
                    Console.WriteLine($"tempDir: {tempDir}");

                    using (var env = new TempCurDir("tools")) {

                        // copy git-lfx.exe and libs to tools dir
                        typeof(ImmutableDictionary).Assembly.Location.CopyToDir();
                        typeof(GitCmd).Assembly.Location.CopyToDir();
                        typeof(LfxCmd).Assembly.Location.CopyToDir();
                        typeof(Program).Assembly.Location.CopyToDir();

                        // add tools dir to path
                        Environment.SetEnvironmentVariable("PATH",
                            Environment.GetEnvironmentVariable("PATH") + ";" + env
                        );
                    }

                    using (var repoDir = new TempDir("repo")) {
                        Git($"lfx init");

                        using (var gitDir = new TempDir("git")) {
                            dir.Path.CopyDir(".");

                            File.WriteAllLines(".gitattributes", new[] {
                                $"* filter=lfx diff=lfx merge=lfx -text",
                                $".gitattributes filter= diff= merge= text=auto",
                                $".gitignore filter= diff= merge= text=auto",
                                $"packages.config filter= diff= merge= text=auto",
                                $"*.lfxconfig filter= diff= merge= text eol=lf"
                            });

                            Git($"config -f .lfxconfig --add {LfxConfigFile.TypeId} {LfxPointerType.Exe}");
                            Git($"config -f .lfxconfig --add {LfxConfigFile.UrlId} {PortableGit}");
                            Git($"config -f .lfxconfig --add {LfxConfigFile.ArgsId} \"{Args}\"");

                            //Git($"lfx reset");
                            //Git($"lfx checkout");
                        }
                    }
                }
            }
        }
    }
}
