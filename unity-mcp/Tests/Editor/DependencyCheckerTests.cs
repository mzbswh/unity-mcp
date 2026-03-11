using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class DependencyCheckerTests
    {
        [Test]
        public void ParseVersion_PythonFormat()
        {
            var version = DependencyChecker.ParseVersion("Python 3.12.1");
            Assert.AreEqual("3.12.1", version);
        }

        [Test]
        public void ParseVersion_UvFormat()
        {
            var version = DependencyChecker.ParseVersion("uv 0.5.1 (abcdef 2025-01-01)");
            Assert.AreEqual("0.5.1", version);
        }

        [Test]
        public void ParseVersion_NullInput()
        {
            Assert.IsNull(DependencyChecker.ParseVersion(null));
        }

        [Test]
        public void ParseVersion_EmptyInput()
        {
            Assert.IsNull(DependencyChecker.ParseVersion(""));
        }

        [Test]
        public void ParseVersion_NoVersionInString()
        {
            Assert.IsNull(DependencyChecker.ParseVersion("no version here"));
        }

        [Test]
        public void Check_ReturnsStatus()
        {
            var status = DependencyChecker.Check();
            Assert.That(status.AllSatisfied, Is.TypeOf<bool>());
        }

        [Test]
        public void RunCommand_InvalidCommand_ReturnsFalse()
        {
            var (success, _) = DependencyChecker.RunCommand("nonexistent_command_xyz", "--version");
            Assert.IsFalse(success);
        }
    }
}
