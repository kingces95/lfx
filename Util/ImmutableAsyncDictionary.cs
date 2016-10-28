using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Util {

    public abstract class ImmutableAsyncDictionary<TKey, TPointer, T> {

        private readonly ConcurrentDictionary<TKey, Task<T>> m_values;

        public ImmutableAsyncDictionary() {
            m_values = new ConcurrentDictionary<TKey, Task<T>>();
        }

        protected abstract TKey GetKey(TPointer pointer);
        protected abstract bool TryLoadValue(TKey key, out T value);
        protected abstract Task<T> LoadValueAsync(TPointer pointer);

        public event Action<TPointer> OnAsyncLoad;
        public event Action<TKey> OnKeyMissing;

        public bool ContainsKey(TKey key) {
            T value;
            return TryGetValue(key, out value);
        }
        public bool TryGetValue(TKey key, out T value) {
            value = default(T);

            Task<T> taskValue;
            if (!m_values.TryGetValue(key, out taskValue)) {
                OnKeyMissing?.Invoke(key);
                return false;
            }

            value = taskValue.Result;
            return true;
        }
        public async Task<T> GetOrLoadValueAsync(TPointer pointer) {

            TKey key = GetKey(pointer);
            ManualResetEvent mre = null;
            Task<T> freshTask = null;
            Exception ex = null;

            var cachedTask = m_values.GetOrAdd(key, delegate {

                // try synchronous load
                T value;
                if (TryLoadValue(key, out value))
                    return Task.FromResult(value);

                // threads not driving async load block on WaitHandle
                mre = new ManualResetEvent(false);

                // when WaitHandle is singled, result is available in m_values
                return freshTask = mre.WaitOneAsync(() => {
                    if (ex != null)
                        throw ex;

                    return m_values[key].Result;
                });
            });

            // synchronous load or lost race to perform load
            if (freshTask != cachedTask)
                return await cachedTask;

            try {
                // async load!
                OnAsyncLoad?.Invoke(pointer);
                var loadResult = await LoadValueAsync(pointer);

                // cache result, free WaitHandle
                m_values[key] = Task.FromResult(loadResult);

                // finished
                return loadResult;

            // capture error so it can be rethrown by waiting threads
            } catch (Exception e) {
                ex = e;
                throw e;

            // notify threads of success or failure
            } finally {
                mre.Set();
            }
        }
    }
}