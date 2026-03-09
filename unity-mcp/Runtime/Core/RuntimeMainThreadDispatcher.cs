#if UNITY_MCP_RUNTIME
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityMcp.Shared.Interfaces;

namespace UnityMcp.Runtime.Core
{
    /// <summary>
    /// Thread-safe main thread dispatcher for Runtime.
    /// Queues work items from background threads and executes them on MonoBehaviour.Update().
    /// </summary>
    public class RuntimeMainThreadDispatcher : IMainThreadDispatcher
    {
        private readonly ConcurrentQueue<WorkItem> _workQueue = new();
        private int _mainThreadId = -1;

        /// <summary>Call from the main thread (e.g. Awake) to capture the thread ID.</summary>
        public void CaptureMainThreadId()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool IsMainThread =>
            _mainThreadId >= 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>Process up to maxItems work items. Call from Update().</summary>
        public int ProcessQueue(int maxItems = 10)
        {
            int processed = 0;
            while (processed < maxItems && _workQueue.TryDequeue(out var item))
            {
                try
                {
                    item.Execute();
                }
                catch (Exception ex)
                {
                    item.SetException(ex);
                }
                processed++;
            }
            return processed;
        }

        /// <summary>Schedule a function to run on the main thread and return its result.</summary>
        public Task<T> RunOnMainThread<T>(Func<T> action, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => tcs.TrySetCanceled(), false);

            _workQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }
                    tcs.TrySetResult(action());
                }
                finally
                {
                    cts.Dispose();
                }
            }, ex =>
            {
                tcs.TrySetException(ex);
                cts.Dispose();
            }));

            return tcs.Task;
        }

        /// <summary>Schedule an action to run on the main thread.</summary>
        public Task RunOnMainThread(Action action, CancellationToken ct = default)
        {
            return RunOnMainThread<object>(() => { action(); return null; }, ct);
        }

        // IMainThreadDispatcher implementation
        public Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000)
        {
            var cts = new CancellationTokenSource(timeoutMs);
            var task = RunOnMainThread(func, cts.Token);
            task.ContinueWith(_ => cts.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        public Task RunAsync(Action action, int timeoutMs = 30000)
        {
            return RunAsync<object>(() => { action(); return null; }, timeoutMs);
        }

        private readonly struct WorkItem
        {
            private readonly Action _action;
            private readonly Action<Exception> _onError;

            public WorkItem(Action action, Action<Exception> onError)
            {
                _action = action;
                _onError = onError;
            }

            public void Execute() => _action();
            public void SetException(Exception ex) => _onError(ex);
        }
    }
}
#endif
