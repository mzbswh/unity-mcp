using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Interfaces;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Adapter that wraps the static MainThreadDispatcher as an IMainThreadDispatcher instance.
    /// </summary>
    public class MainThreadDispatcherAdapter : IMainThreadDispatcher
    {
        public static readonly MainThreadDispatcherAdapter Instance = new();

        public bool IsMainThread => MainThreadDispatcher.IsMainThread;

        public Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000)
            => MainThreadDispatcher.RunAsync(func, timeoutMs);

        public Task RunAsync(Action action, int timeoutMs = 30000)
            => MainThreadDispatcher.RunAsync(action, timeoutMs);
    }

    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> s_queue = new();
        private static int s_mainThreadId;
        private static volatile int s_pending;
        private static Timer s_wakeTimer;

        static MainThreadDispatcher()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueue;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ =>
            {
                s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
                ProcessQueue();
            };

            // Background timer: when pending work exists, nudge the main thread
            // via delayCall every 50ms. This wakes up an idle editor that would
            // otherwise only run EditorApplication.update at ~1 fps.
            s_wakeTimer = new Timer(_ =>
            {
                if (s_pending > 0)
                    EditorApplication.delayCall += ProcessQueue;
            }, null, 50, 50);
        }

        public static bool IsMainThread =>
            Thread.CurrentThread.ManagedThreadId == s_mainThreadId;

        public static Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000)
        {
            if (IsMainThread)
            {
                try { return Task.FromResult(func()); }
                catch (Exception ex) { return Task.FromException<T>(ex); }
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => tcs.TrySetException(
                new TimeoutException(
                    $"Main thread execution timed out after {timeoutMs}ms.")),
                useSynchronizationContext: false);

            s_queue.Enqueue(() =>
            {
                if (cts.IsCancellationRequested) return;
                try { tcs.TrySetResult(func()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
                finally { cts.Dispose(); }
            });

            Interlocked.Increment(ref s_pending);

            return tcs.Task;
        }

        public static Task RunAsync(Action action, int timeoutMs = 30000)
        {
            return RunAsync<object>(() => { action(); return null; }, timeoutMs);
        }

        private static void ProcessQueue()
        {
            int budget = 50;
            while (budget-- > 0 && s_queue.TryDequeue(out var action))
            {
                Interlocked.Decrement(ref s_pending);
                try { action(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
    }
}
