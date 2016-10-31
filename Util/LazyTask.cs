using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Util {

    // expanded upon from: https://blogs.msdn.microsoft.com/pfxteam/2011/01/15/asynclazyt/
    public class LazyTask : Lazy<Task> {
        public static LazyTask<T> FromResult<T>(T result) {
            return new LazyTask<T>(() => Task.FromResult(result));
        }

        public LazyTask(Action action) :
            base(() => Task.Factory.StartNew(action)) { }
        public LazyTask(Func<Task> action) :
            base(() => Task.Factory.StartNew(() => action()).Unwrap()) { }
    }
    public class LazyTask<T> : Lazy<Task<T>> {

        public LazyTask(Func<T> valueFactory) :
            base(() => Task.Factory.StartNew(valueFactory)) { }
        public LazyTask(Func<Task<T>> taskFactory) :
            base(() => Task.Factory.StartNew(() => taskFactory()).Unwrap()) { }

        public TaskAwaiter<T> GetAwaiter() { return Value.GetAwaiter(); }
    }
}