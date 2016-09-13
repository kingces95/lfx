using System.Linq;
using NUnit.Framework;
using System;
using Git.Lfs;

namespace Git.Test {

    [TestFixture]
    public class GitConfigTest {
        private static void Git(string arguments) => GitCmd.Execute(arguments);

        [Test]
        public static void Test() {
            Console.WriteLine($"currentDir: {Environment.CurrentDirectory}");

            using (var tempDir = new TempDir()) {
                var tempDirString = (string)tempDir;
                Console.WriteLine($"tempDir: {tempDir}");
                Git($"init");

                var local = GitConfig.Load();
                var global = local.Parent;
                var system = global.Parent;
                Assert.IsNull(system.Parent);
                Assert.IsTrue(!system.Except(local).Any());
                Assert.IsTrue(!system.Except(global).Any());
                Assert.IsTrue(!global.Except(local).Any());
                Assert.AreEqual(local.Count, local.Count());
                Assert.IsTrue(system.Keys().All(o => local.Contains(o)));
                Assert.IsTrue(system.Keys().All(o => global.Contains(o)));
                Assert.IsTrue(global.Keys().All(o => local.Contains(o)));
                Assert.IsTrue(!system.Keys().Except(local.Keys()).Any());
                Assert.IsTrue(!global.Keys().Except(local.Keys()).Any());

                Assert.AreEqual((string)tempDir, local.EnlistmentDirectory);
                Assert.AreEqual(null, global.EnlistmentDirectory);
                Assert.AreEqual(null, system.EnlistmentDirectory);

                var localFile = local.ConfigFile;
                Assert.AreEqual((string)tempDir, localFile.WorkingDir);
                Assert.AreEqual(tempDir + @".git\config", localFile.Path);

                var globalFile = global.ConfigFile;
                var systemFile = system.ConfigFile;
                var count = localFile.Count + globalFile.Count + systemFile.Count;
                Assert.AreEqual(
                    local.Count, 
                    localFile.Keys().Union(
                        globalFile.Keys().Union(
                            systemFile.Keys())).Count()
                 );

                var myKey = "myKey.mySubKey";
                var myValue = "myValue";
                Git($"config --add {myKey} {myValue}");
                var localFile0 = localFile.Reload();
                var local0 = local.Reload();
                Assert.AreEqual(localFile.Count + 1, local0.ConfigFile.Count);

                string result;
                Assert.IsFalse(local.TryGetValue(myKey, out result));
                Assert.IsFalse(localFile.TryGetValue(myKey, out result));

                Assert.IsTrue(local0.TryGetValue(myKey, out result));
                Assert.AreEqual(myValue, result);

                Assert.IsTrue(local0.ConfigFile.TryGetValue(myKey, out result));
                Assert.AreEqual(myValue, result);

                Assert.AreEqual(myValue, (string)local0[myKey]);

                Console.WriteLine("systemFile:");
                foreach (var keyValue in systemFile)
                    Console.WriteLine("  " + keyValue.ToString());

                Console.WriteLine("globalFile:");
                foreach (var keyValue in globalFile)
                    Console.WriteLine("  " + keyValue.ToString());

                Console.WriteLine("localFile:");
                foreach (var keyValue in localFile)
                    Console.WriteLine("  " + keyValue.ToString());

                Console.WriteLine("config:");
                foreach (var keyValue in local)
                    Console.WriteLine("  " + keyValue.ToString());
            }
        }
    }
}
