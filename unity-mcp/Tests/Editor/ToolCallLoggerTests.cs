using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class ToolCallLoggerTests
    {
        [SetUp]
        public void SetUp()
        {
            ToolCallLogger.Clear();
        }

        [Test]
        public void Log_SingleCall_InHistory()
        {
            ToolCallLogger.Log("test_tool", 42, true);
            var history = ToolCallLogger.GetHistory();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("test_tool", history[0].ToolName);
            Assert.AreEqual(42, history[0].DurationMs);
            Assert.IsTrue(history[0].Success);
        }

        [Test]
        public void Log_ExceedsBuffer_OldestDropped()
        {
            for (int i = 0; i < 25; i++)
                ToolCallLogger.Log($"tool_{i}", i, true);

            var history = ToolCallLogger.GetHistory();
            Assert.AreEqual(20, history.Count);
            // Oldest should be tool_5 (first 5 dropped)
            Assert.AreEqual("tool_5", history[0].ToolName);
            Assert.AreEqual("tool_24", history[19].ToolName);
        }

        [Test]
        public void Clear_EmptiesHistory()
        {
            ToolCallLogger.Log("test", 1, true);
            ToolCallLogger.Clear();
            Assert.AreEqual(0, ToolCallLogger.GetHistory().Count);
        }

        [Test]
        public void GetHistory_EmptyByDefault()
        {
            Assert.AreEqual(0, ToolCallLogger.GetHistory().Count);
        }
    }
}
