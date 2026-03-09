using System;
using System.Threading.Tasks;

namespace UnityMcp.Shared.Interfaces
{
    public interface IMainThreadDispatcher
    {
        bool IsMainThread { get; }
        Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000);
        Task RunAsync(Action action, int timeoutMs = 30000);
    }
}
