using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Git.Lfx;
using Util;
using System.Text;
using System.Collections.Concurrent;
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
            private readonly LfxProgressType m_type;
            private HashSet<string> m_paths;
            private bool m_verbose;
            private long m_bytesTotal;
            private long m_bytesProgress;
            private long m_countTotal;
            private long m_countProgress;

            internal Tracker(LfxEnv env, LfxProgressType type, bool verbose) {
                m_env = env;
                m_type = type;
                m_verbose = verbose;
                m_paths = new HashSet<string>();
            }

            private void Log(object obj) {
                if (!m_verbose)
                    return;

                m_env.Log(obj);
            }
            private void LogProgress(LfxProgress progress) {

                var path = progress.Path;
                lock (this) {
                    if (!m_paths.Contains(path)) {
                        m_paths.Add(path);
                        Log($"Begin {Type}: {path}");
                    }
                }

                if (progress.Bytes == -1) 
                    Log($"End {Type}: {path}");
            }

            internal LfxProgressType Type => m_type;
            internal void UpdateProgress(LfxProgress progress) {
                LogProgress(progress);

                if (progress.Bytes == -1) 
                    Interlocked.Increment(ref m_countProgress);
                else
                    Interlocked.Add(ref m_bytesProgress, progress.Bytes);
            }
            internal void SetTotal(long bytes, long count) {
                m_bytesTotal = bytes;
                m_countTotal = count;
            }
            internal void Finish() {
                //m_bytesProgress = m_bytesTotal;
                //m_countProgress = m_countTotal;
            }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append($"{m_type}: ");

                // count
                BuildProgress(sb, m_countProgress, m_countTotal, showTotal: true);
                sb.Append(" ");

                // bytes
                BuildProgress(sb, m_bytesProgress, m_bytesTotal, o => o.ToStringMetric("b"), showPercent: true);
                return sb.ToString();
            }
        }

        private readonly LfxEnv m_env;
        private readonly ConcurrentDictionary<LfxProgressType, Tracker> m_trackers;
        private readonly bool m_verbose;

        public LfxProgressTracker(LfxEnv env, bool verbose) {
            m_env = env;
            m_trackers = new ConcurrentDictionary<LfxProgressType, Tracker>();
            m_verbose = verbose;
        }

        private void ReportProgress() {
            m_env.ReLog(ToString());
        }
        private void Log(object obj) => m_env.Log(obj);
        private Tracker GetTracker(LfxProgressType type) {
            return m_trackers.GetOrAdd(type, o => new Tracker(m_env, o, m_verbose));
        }

        public Task ComputeTotalsAsync(LfxEnv env) {
            long totalPaths = 0;
            long totalDownloads = 0;
            long totalDownloadSize = 0;
            long totalExpands = 0;
            long totalExpandedSize = 0;

            return Task.Run(() => {
                foreach (var infoPath in env.InfoDir.GetAllFiles()) {
                    var repoInfo = env.GetRepoInfo(infoPath);

                    totalPaths++;
                    if (m_verbose)
                        Log($"Discovered: {infoPath}");

                    // missing metadata; progress not possible
                    if (!repoInfo.HasMetadata)
                        return;

                    totalDownloads++;
                    if (!repoInfo.IsFile)
                        totalExpands++;

                    totalExpandedSize += repoInfo.ContentSize ?? 0;
                    totalDownloadSize += repoInfo.Size;
                };

                if (m_verbose)
                    Log($"Discovered Total: {totalPaths}");

                lock (this) {
                    GetTracker(LfxProgressType.Download).SetTotal(totalDownloadSize, totalDownloads);
                    GetTracker(LfxProgressType.Expand).SetTotal(totalExpandedSize, totalExpands);
                }
            });
        }
        public void UpdateProgress(LfxProgress progress) {
            var tracker = GetTracker(progress.Type);
            lock (this)
                tracker.UpdateProgress(progress);
            ReportProgress();
        }
        public void Finished() {
            foreach (var tracker in m_trackers.Values)
                tracker.Finish();
            ReportProgress();
        }

        public override string ToString() {

            var trackers =
                from tracker in m_trackers.Values
                orderby tracker.Type
                select tracker;

            lock (this)
                return string.Join(", ", trackers.Select(o => o.ToString()).ToArray());
        }
    }
}