using System.Linq;
using System.Threading;
using Util;
using System.Text;
using System;
using System.Collections.Generic;

namespace Lfx {
    public sealed class LfxProgressTracker {

        private sealed class Tracker {
            private static void BuildProgress(
                StringBuilder sb, 
                long progress, 
                long total, 
                Func<long, string> format = null,
                bool showPercent = false,
                bool showTotal = false) {

                if (format == null)
                    format = o => o.ToString();

                if (total != 0 && showTotal)
                    sb.Append($"{format(total - progress)}|");
                sb.Append($"{format(progress)}");

                if (showPercent && total != 0)
                    sb.Append($" ({progress / (double)total:P})");
            }

            private readonly LfxEnv m_env;
            private readonly LfxLoadAction m_type;
            private HashSet<string> m_targetPaths;
            private bool m_verbose;

            private long m_bytesTotal;
            private long m_bytesProgress;

            private long m_countTotal;
            private long m_countProgress;

            internal Tracker(
                LfxEnv env, 
                LfxLoadAction type, 
                long bytesTotal,
                long countTotal,
                bool verbose) {

                m_env = env;
                m_type = type;
                m_verbose = verbose;
                m_targetPaths = new HashSet<string>();
                m_bytesTotal = bytesTotal;
                m_countTotal = countTotal;
            }

            private void Log(object obj) {
                if (!m_verbose)
                    return;

                m_env.Log(obj);
            }
            private void LogProgress(LfxProgress progress) {

                var sourcePath = progress.SourcePath;
                var targetPath = progress.TargetPath;
                lock (this) {
                    if (!m_targetPaths.Contains(targetPath)) {
                        m_targetPaths.Add(targetPath);
                        Log($"Start {Type}: {sourcePath}");
                    }

                    if (progress.Bytes == null) 
                        Log($"End   {Type}: {sourcePath}");
                }
            }

            internal LfxLoadAction Type => m_type;
            internal void UpdateProgress(LfxProgress progress) {
                LogProgress(progress);

                if (progress.Bytes == null) 
                    Interlocked.Increment(ref m_countProgress);
                else
                    Interlocked.Add(ref m_bytesProgress, (long)progress.Bytes);
            }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append($"{m_type}: ");

                // count
                BuildProgress(sb, m_countProgress, m_countTotal, showTotal: true);
                sb.Append(" ");

                // bytes
                BuildProgress(sb, m_bytesProgress, m_bytesTotal, o => o.ToFileSize(), showPercent: true);
                return sb.ToString();
            }
        }

        private readonly LfxEnv m_env;
        private readonly Dictionary<LfxLoadAction, Tracker> m_trackers;
        private readonly bool m_verbose;

        public LfxProgressTracker(
            LfxEnv env,
            IEnumerable<LfxCount> counts,
            bool verbose) {

            m_env = env;
            m_verbose = verbose;
            m_trackers = (
                from o in counts
                where o.Count > 0
                select new Tracker(
                    env: env,
                    type: o.Action,
                    bytesTotal: o.Bytes,
                    countTotal: o.Count,
                    verbose: verbose
                )
            ).ToDictionary(o => o.Type);
        }

        private LfxLoadAction GetAction(LfxProgress progress) {
            if (progress.Type == LfxProgressType.Download)
                return LfxLoadAction.Wan;

            else if (progress.Type == LfxProgressType.Expand)
                return LfxLoadAction.Expand;

            if (progress.SourcePath.IsUncPath())
                return LfxLoadAction.Lan;

            if (progress.TargetPath.IsSubPathOf(m_env.CacheDir))
                return LfxLoadAction.Copy;

            return LfxLoadAction.Bus;
        }

        public void UpdateProgress(LfxProgress progress) {
            lock (this) {
                var action = GetAction(progress);
                Tracker tracker;
                if (!m_trackers.TryGetValue(action, out tracker))
                    m_trackers[action] = tracker = new Tracker(m_env, action, 0, 0, m_verbose);
                tracker.UpdateProgress(progress);
                m_env.ReLog(ToString());
            }
        }

        public override string ToString() {
            var trackers =
                from tracker in m_trackers.Values
                orderby tracker.Type descending
                select tracker;

            return string.Join(", ", trackers.Select(o => o.ToString()).ToArray());
        }
    }
}