using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Git.Lfx {

    [Flags]
    public enum LfxFileFlags {
        Tracked = 1 << 0,
        Untracked = 1 << 1,
        Pointer = 1 << 2,
        Content = 1 << 3,
        Default = Tracked | Content
    }

    public static class LfxFile {
        public static IEnumerable<GitFile> Load(
            string filter = null,
            string dir = null,
            LfxFileFlags flags = LfxFileFlags.Default) {

            GitFileFlags gitFlags = default(GitFileFlags);
            if ((flags & LfxFileFlags.Tracked) != 0)
                gitFlags |= GitFileFlags.Tracked;

            if ((flags & LfxFileFlags.Untracked) != 0)
                gitFlags |= GitFileFlags.Untracked;

            var lfxFiles = GitFile.Load(filter, dir, gitFlags)
                .Where(o => o.IsDefined("filter", "lfx"));

            if ((flags & LfxFileFlags.Content) != 0)
                lfxFiles = lfxFiles.Where(o => !LfxPointer.CanLoad(o.Path));

            if ((flags & LfxFileFlags.Pointer) != 0)
                lfxFiles = lfxFiles.Where(o => LfxPointer.CanLoad(o.Path));

            return lfxFiles;
        }
    }
}