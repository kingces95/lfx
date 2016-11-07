using NUnit.Framework;
using System.Linq;
using System;
using System.IO;

namespace Git.Test {

    [TestFixture]
    public class GitArgsTest
    {
        public enum KnownSwitches {
            MyGitSwitch, M
        }

        private const string Exe = "git-lfx.exe";
        private const string Name = "myGitCommand";
        private static readonly string ExeName = $"{Exe} {Name}";
        private const string LongSwitch = nameof(KnownSwitches.MyGitSwitch);
        private const string OtherLongSwitch = "myOtherGitSwitch";
        private const string ShortSwitch = nameof(KnownSwitches.M);
        private const string OtherShortSwitch = "o";
        private const string FileNameWithSpaces = @"c:\foo\b a r.txt";
        private static readonly GitCmdSwitchInfo[] SwitchInfo = new[] {
            new GitCmdSwitchInfo(LongSwitch),
            new GitCmdSwitchInfo(ShortSwitch),
        };

        private static void Throws(TestDelegate action) {
            Assert.Throws<GitCmdException>(action);
        }

        [Test]
        public static void SimpleTest() {
            var args = GitCmdArgs.Parse(Exe);
            Assert.AreEqual(Exe, args.Exe);
            Assert.AreEqual(null, args.Name);
            Assert.AreEqual(0, args.Length);
            Assert.AreEqual(0, args.Switches().Count());
        }

        [Test]
        public static void SimpleNameTest() {
            var args = GitCmdArgs.Parse(ExeName);
            Assert.AreEqual(Exe, args.Exe);
            Assert.AreEqual(Name, args.Name);
            Assert.AreEqual(0, args.Length);
            Assert.AreEqual(0, args.Switches().Count());
        }

        [Test]
        public static void SimpleSwitch() {
            var longArgs = GitCmdArgs.Parse($"{ExeName} --{LongSwitch}");
            Assert.IsTrue(longArgs.IsSet(LongSwitch.ToUpper()));
            Assert.IsTrue(longArgs.IsSet(LongSwitch.ToLower()));

            var shortArgs = GitCmdArgs.Parse($"{ExeName} -{ShortSwitch}");
            Assert.IsTrue(shortArgs.IsSet(ShortSwitch.ToUpper()));
            Assert.IsTrue(shortArgs.IsSet(ShortSwitch.ToLower()));
        }

        [Test]
        public static void KnownSwitchName() {
            var args = GitCmdArgs.Parse(
                $"{ExeName} -{ShortSwitch} --{LongSwitch}",
                switchInfo: GitCmdSwitchInfo.Create(
                    ShortSwitch, 
                    LongSwitch
                )
            );
            Assert.IsTrue(args.IsSet(ShortSwitch));
            Assert.IsTrue(args.IsSet(LongSwitch));
        }

        [Test]
        public static void KnownSwitchEnum() {
            var args = GitCmdArgs.Parse(
                $"{ExeName} -{ShortSwitch} --{LongSwitch}",
                switchInfo: GitCmdSwitchInfo.Create(
                    KnownSwitches.MyGitSwitch,
                    KnownSwitches.M
                )
            );
            Assert.IsTrue(args.IsSet(KnownSwitches.MyGitSwitch));
            Assert.IsTrue(args.IsSet(KnownSwitches.M));
        }

        [Test]
        public static void ArgBoundsName() {
            GitCmdArgs.Parse($"{ExeName} a", minArgs: 1, maxArgs: 1);
            Throws(() => GitCmdArgs.Parse($"{ExeName} a b", minArgs: 1, maxArgs: 1));
            Throws(() => GitCmdArgs.Parse($"{ExeName}", minArgs: 1, maxArgs: 1));
        }

        [Test]
        public static void UnknownSwitchName() {
            var switches = GitCmdSwitchInfo.Create(
                ShortSwitch,
                LongSwitch
            );

            var longSwitchCmd = $"{ExeName} --{OtherLongSwitch}";
            GitCmdArgs.Parse(longSwitchCmd);
            Throws(() => GitCmdArgs.Parse(longSwitchCmd, switchInfo: switches));

            var shortSwitchCmd = $"{ExeName} -{OtherShortSwitch}";
            GitCmdArgs.Parse(shortSwitchCmd);
            Throws(() => GitCmdArgs.Parse(shortSwitchCmd, switchInfo: switches));
        }

        [Test]
        public static void QuotedSwitchName() {
            var args = GitCmdArgs.Parse($"{ExeName} --swi\"tch\"");
            Assert.AreEqual(Exe, args.Exe);
            Assert.AreEqual(Name, args.Name);
            Assert.AreEqual(0, args.Length);
            Assert.IsTrue(args.IsSet("switch"));
        }

        [Test]
        public static void AllTypesTest() {

            var args = GitCmdArgs.Parse($"{ExeName} -{ShortSwitch} --{LongSwitch} \"{FileNameWithSpaces}\"");
            Assert.AreEqual(Exe, args.Exe);
            Assert.AreEqual(Name, args.Name);
            Assert.IsTrue(args.IsSet(LongSwitch));
            Assert.IsTrue(args.IsSet(ShortSwitch));
            Assert.IsTrue(args.IsSet(ShortSwitch, OtherShortSwitch));
            Assert.IsFalse(args.IsSet(OtherShortSwitch));
            Assert.AreEqual(FileNameWithSpaces, args.Single());
            Assert.AreEqual(FileNameWithSpaces, args[0]);
            Assert.AreEqual(1, args.Length);

            var toString = args.ToString();
            Assert.IsTrue(toString.Contains($"{Exe}"));
            Assert.IsTrue(toString.Contains($"{Name}"));
            Assert.IsTrue(toString.Contains($"{ShortSwitch.ToLower()}"));
            Assert.IsTrue(toString.Contains($"{LongSwitch.ToLower()}"));
            Assert.IsTrue(toString.Contains($"{FileNameWithSpaces}"));

        }

        [Test]
        public static void ThrowsNoName() {
            Throws(() => GitCmdArgs.Parse($"--a"));
        }

        [Test]
        public static void ThrowsDasheCount() {
            Throws(() => GitCmdArgs.Parse($"{ExeName} ---a"));
        }

        [Test]
        public static void ThrowsNoSwitchName() {
            Throws(() => GitCmdArgs.Parse($"{ExeName} -"));
            GitCmdArgs.Parse($"{ExeName} --");
        }

        [Test]
        public static void ThrowsBadSwitchLength() {
            Throws(() => GitCmdArgs.Parse($"{ExeName} --a"));
            Throws(() => GitCmdArgs.Parse($"{ExeName} -aa"));
        }

        [Test]
        public static void ThrowsDuplicateSwitch() {
            Throws(() => GitCmdArgs.Parse($"{ExeName} -a -a"));
            Throws(() => GitCmdArgs.Parse($"{ExeName} --aa --aa"));
        }
    }
}
