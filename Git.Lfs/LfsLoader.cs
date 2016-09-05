using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsLoader {
        public static readonly string DefaultLfsUserDir = 
            Environment.GetEnvironmentVariable("APPDATA") + 
                Path.DirectorySeparatorChar + "lfs";

        private readonly string m_lfsDir;
        private readonly LfsObjectsCache m_cache;
        private readonly Dictionary<LfsHash, LfsObject> m_objects; 

        public LfsLoader(string lfsDir) {
            m_lfsDir = lfsDir;
            Directory.CreateDirectory(m_lfsDir);

            m_cache = new LfsObjectsCache(m_lfsDir + "objects" + Path.DirectorySeparatorChar);
            m_objects = new Dictionary<LfsHash, LfsObject>();
        }

        public LfsObjectsCache RootCache => m_cache;
        public LfsObject GetObject(LfsPointer pointer) {
            m_cache.Load(pointer);

            var hash = pointer.Hash;

            LfsObject result;
            if (!m_objects.TryGetValue(hash, out result))
                m_objects[hash] = new LfsObject(this, hash);
            return result;
        }
    }
}