using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMcp.Shared.Models;

namespace UnityMcp.Tests.Editor
{
    public static class TestUtilities
    {
        private static readonly List<GameObject> _tempObjects = new();

        /// <summary>Create a temporary GameObject that is auto-cleaned after each test.</summary>
        public static GameObject CreateTempGameObject(string name = "TestObject")
        {
            var go = new GameObject(name);
            _tempObjects.Add(go);
            return go;
        }

        /// <summary>Destroy all temp GameObjects. Call from [TearDown].</summary>
        public static void CleanUp()
        {
            foreach (var go in _tempObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _tempObjects.Clear();
        }

        /// <summary>Assert that a ToolResult is successful and return its content.</summary>
        public static JToken AssertSuccessAndParse(ToolResult result)
        {
            Assert.IsTrue(result.IsSuccess, $"Expected success but got error: {result.ErrorMessage}");
            Assert.IsNotNull(result.Content);
            return result.Content;
        }

        /// <summary>Assert that a ToolResult is an error with expected message substring.</summary>
        public static void AssertError(ToolResult result, string expectedSubstring = null)
        {
            Assert.IsFalse(result.IsSuccess, "Expected error but got success");
            if (expectedSubstring != null)
                Assert.That(result.ErrorMessage, Does.Contain(expectedSubstring));
        }
    }
}
