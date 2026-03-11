using System;
using System.Collections.Generic;

namespace UnityMcp.Editor.Core
{
    public static class ToolCallLogger
    {
        public struct CallRecord
        {
            public string ToolName;
            public long DurationMs;
            public bool Success;
            public DateTime Timestamp;
        }

        private const int MaxRecords = 20;
        private static readonly CallRecord[] _buffer = new CallRecord[MaxRecords];
        private static int _head;
        private static int _count;

        public static void Log(string tool, long durationMs, bool success)
        {
            _buffer[_head] = new CallRecord
            {
                ToolName = tool,
                DurationMs = durationMs,
                Success = success,
                Timestamp = DateTime.Now,
            };
            _head = (_head + 1) % MaxRecords;
            if (_count < MaxRecords) _count++;
        }

        public static List<CallRecord> GetHistory()
        {
            var list = new List<CallRecord>(_count);
            int start = _count < MaxRecords ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % MaxRecords;
                list.Add(_buffer[idx]);
            }
            return list;
        }

        public static void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
