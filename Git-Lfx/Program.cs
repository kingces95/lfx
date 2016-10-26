using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Lfx {

    public class Program {
        public static void Main() {
            LfxCmd.Execute(Environment.CommandLine);
        }
    }
}