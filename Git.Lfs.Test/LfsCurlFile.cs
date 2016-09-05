using Git.Lfs;
using NUnit.Framework;
using System;

namespace Git.Lfs.Test {

    //[TestFixture]
    public static class LfsCurlFileTest
    {
        //[Test]
        public static void NugetTest() {
            var path = @"F:\git\lfs-sandbox\packages\NUnit.2.6.4\lib\nunit.framework.dll";
            var lfsCurlFile = LfsFile.Create(path);
            Console.WriteLine($"{lfsCurlFile.Pointer}");
        }
    }
}
