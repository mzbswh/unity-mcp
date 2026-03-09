#if UNITY_MCP_RUNTIME
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Runtime.Core
{
    public class McpRuntimeBehaviour : MonoBehaviour
    {
        private static McpRuntimeBehaviour _instance;

        [Header("Runtime MCP Settings")]
        [SerializeField] private int _port = 0;
        [SerializeField] private bool _autoStart = true;

        private RuntimeTcpTransport _transport;
        private RuntimeToolRegistry _registry;
        private RuntimeRequestHandler _requestHandler;
        private RuntimeMainThreadDispatcher _dispatcher;

        public static McpRuntimeBehaviour Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null) return;

            var go = new GameObject("[MCP Runtime]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<McpRuntimeBehaviour>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _dispatcher = new RuntimeMainThreadDispatcher();
            _dispatcher.CaptureMainThreadId();
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_autoStart) StartMcp();
        }

        public void StartMcp()
        {
            int port = _port > 0 ? _port : PortResolver.GetPort(Application.dataPath) + 1;

            _registry = new RuntimeToolRegistry();
            _registry.ScanAll();

            _requestHandler = new RuntimeRequestHandler(_registry, this);

            _transport = new RuntimeTcpTransport(port, _requestHandler);
            _transport.Start();

            Debug.Log($"[MCP Runtime] Listening on port {port} " +
                      $"({_registry.ToolCount} tools, {_registry.ResourceCount} resources)");
        }

        private void Update()
        {
            _dispatcher.ProcessQueue(10);
            Tools.RuntimeStatsTools.UpdateStats();
        }

        private void OnDestroy()
        {
            _transport?.Stop();
            if (_instance == this) _instance = null;
        }

        public Task<T> RunOnMainThread<T>(Func<T> action, CancellationToken ct = default)
        {
            return _dispatcher.RunOnMainThread(action, ct);
        }

        public Task RunOnMainThread(Action action, CancellationToken ct = default)
        {
            return _dispatcher.RunOnMainThread(action, ct);
        }
    }
}
#endif
