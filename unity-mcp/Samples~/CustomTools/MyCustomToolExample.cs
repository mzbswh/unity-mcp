using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Samples
{
    /// <summary>
    /// Example: How to create custom MCP tools for your project.
    ///
    /// Steps:
    /// 1. Create a static class with [McpToolGroup] attribute
    /// 2. Add static methods with [McpTool] attribute
    /// 3. Use [Desc] on parameters for documentation
    /// 4. Return ToolResult from each method
    ///
    /// Your tools will be automatically discovered and registered by the MCP server.
    /// </summary>
    [McpToolGroup("MyProject.CustomTools")]
    public static class MyCustomToolExample
    {
        [McpTool("my_hello_world", "A simple hello world tool for testing",
            Group = "custom", ReadOnly = true)]
        public static ToolResult HelloWorld(
            [Desc("Your name")] string name = "World")
        {
            return ToolResult.Json(new
            {
                message = $"Hello, {name}! This is a custom MCP tool.",
                timestamp = System.DateTime.Now.ToString("O")
            });
        }

        [McpTool("my_spawn_objects", "Spawn multiple objects in a grid pattern",
            Group = "custom")]
        public static ToolResult SpawnGrid(
            [Desc("Prefab name or primitive type (Cube, Sphere, etc.)")] string objectType = "Cube",
            [Desc("Number of columns")] int columns = 3,
            [Desc("Number of rows")] int rows = 3,
            [Desc("Spacing between objects")] float spacing = 2f)
        {
            int count = 0;
            var parent = new GameObject($"Grid_{objectType}_{columns}x{rows}");

            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    GameObject obj;
                    if (System.Enum.TryParse<PrimitiveType>(objectType, true, out var primitive))
                        obj = GameObject.CreatePrimitive(primitive);
                    else
                        obj = new GameObject(objectType);

                    obj.name = $"{objectType}_{x}_{z}";
                    obj.transform.position = new Vector3(x * spacing, 0, z * spacing);
                    obj.transform.SetParent(parent.transform);
                    count++;
                }
            }

            return ToolResult.Json(new
            {
                success = true,
                objectsCreated = count,
                parentObject = parent.name
            });
        }

        /// <summary>
        /// Example of a custom MCP resource.
        /// Resources are read-only data endpoints that MCP clients can query.
        /// </summary>
        [McpResource("unity://custom/status", "Custom Status",
            "Example custom resource showing project-specific status")]
        public static ToolResult GetCustomStatus()
        {
            return ToolResult.Json(new
            {
                projectName = Application.productName,
                customMessage = "This is a custom resource example",
                objectCount = Object.FindObjectsByType<GameObject>(
                    FindObjectsSortMode.None).Length
            });
        }
    }
}
