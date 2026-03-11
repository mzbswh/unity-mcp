using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class ToolRegistryTests
    {
        private ToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ToolRegistry();
        }

        [Test]
        public void ScanAll_FindsTools()
        {
            _registry.ScanAll();
            Assert.That(_registry.ToolCount, Is.GreaterThan(0),
                "Should discover at least one tool");
        }

        [Test]
        public void ScanAll_FindsResources()
        {
            _registry.ScanAll();
            Assert.That(_registry.ResourceCount, Is.GreaterThan(0),
                "Should discover at least one resource");
        }

        [Test]
        public void ScanAll_FindsPrompts()
        {
            _registry.ScanAll();
            Assert.That(_registry.PromptCount, Is.GreaterThan(0),
                "Should discover at least one prompt");
        }

        [Test]
        public void GetToolList_ReturnsValidSchema()
        {
            _registry.ScanAll();
            var tools = _registry.GetToolList();
            Assert.That(tools, Is.Not.Null);
            Assert.That(tools.Count, Is.GreaterThan(0));

            foreach (var tool in tools)
            {
                Assert.That(tool["name"]?.ToString(), Is.Not.Null.And.Not.Empty,
                    "Each tool must have a name");
                Assert.That(tool["inputSchema"], Is.Not.Null,
                    $"Tool '{tool["name"]}' must have an inputSchema");
            }
        }

        [Test]
        public void GetResourceList_ReturnsValidEntries()
        {
            _registry.ScanAll();
            var resources = _registry.GetResourceList();
            Assert.That(resources, Is.Not.Null);
            Assert.That(resources.Count, Is.GreaterThan(0));

            foreach (var resource in resources)
            {
                Assert.That(resource["uri"]?.ToString(), Is.Not.Null.And.Not.Empty,
                    "Each resource must have a URI");
                Assert.That(resource["name"]?.ToString(), Is.Not.Null.And.Not.Empty,
                    "Each resource must have a name");
            }
        }

        [Test]
        public void GetPromptList_ReturnsValidEntries()
        {
            _registry.ScanAll();
            var prompts = _registry.GetPromptList();
            Assert.That(prompts, Is.Not.Null);
            Assert.That(prompts.Count, Is.GreaterThan(0));

            foreach (var prompt in prompts)
            {
                Assert.That(prompt["name"]?.ToString(), Is.Not.Null.And.Not.Empty,
                    "Each prompt must have a name");
            }
        }

        [Test]
        public void SetToolEnabled_DisablesTool()
        {
            _registry.ScanAll();
            var tools = _registry.GetToolList();
            if (tools.Count == 0) Assert.Ignore("No tools registered");
            var firstName = tools[0]["name"].ToString();

            _registry.SetToolEnabled(firstName, false);
            Assert.IsFalse(_registry.IsToolEnabled(firstName));
            Assert.IsNull(_registry.GetTool(firstName), "Disabled tool should not be returned");

            _registry.SetToolEnabled(firstName, true);
            Assert.IsTrue(_registry.IsToolEnabled(firstName));
            Assert.IsNotNull(_registry.GetTool(firstName));
        }

        [Test]
        public void MatchResource_FindsExistingUri()
        {
            _registry.ScanAll();
            var entry = _registry.MatchResource("unity://editor/state");
            Assert.IsNotNull(entry, "Should match unity://editor/state resource");
        }

        [Test]
        public void MatchResource_ReturnsNullForUnknown()
        {
            _registry.ScanAll();
            var entry = _registry.MatchResource("unity://nonexistent/thing");
            Assert.IsNull(entry);
        }

        [Test]
        public void GetAllToolEntries_ReturnsAllRegistered()
        {
            _registry.ScanAll();
            int count = 0;
            foreach (var _ in _registry.GetAllToolEntries()) count++;
            Assert.AreEqual(_registry.ToolCount, count,
                "GetAllToolEntries should return all registered tools");
        }
    }
}
