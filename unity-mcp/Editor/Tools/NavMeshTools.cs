using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("NavMesh")]
    public static class NavMeshTools
    {
        [McpTool("navmesh_add_agent", "Add a NavMeshAgent component to a GameObject",
            Group = "navmesh")]
        public static ToolResult AddAgent(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Agent speed")] float speed = 3.5f,
            [Desc("Angular speed")] float angularSpeed = 120f,
            [Desc("Stopping distance")] float stoppingDistance = 0f,
            [Desc("Agent radius")] float radius = 0.5f,
            [Desc("Agent height")] float height = 2f,
            [Desc("Auto braking")] bool autoBraking = true)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            if (go.GetComponent<NavMeshAgent>() != null)
                return ToolResult.Error($"NavMeshAgent already exists on '{target}'");

            var agent = Undo.AddComponent<NavMeshAgent>(go);
            agent.speed = speed;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = radius;
            agent.height = height;
            agent.autoBraking = autoBraking;

            return ToolResult.Json(new
            {
                gameObject = go.name,
                speed,
                radius,
                height,
                message = $"Added NavMeshAgent to '{go.name}'"
            });
        }

        [McpTool("navmesh_add_obstacle", "Add a NavMeshObstacle component to a GameObject",
            Group = "navmesh")]
        public static ToolResult AddObstacle(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Shape: Capsule, Box")] string shape = "Box",
            [Desc("Carve the navmesh")] bool carve = true,
            [Desc("Size (for Box shape)")] Vector3? size = null,
            [Desc("Radius (for Capsule shape)")] float? radius = null,
            [Desc("Height (for Capsule shape)")] float? height = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var obstacle = Undo.AddComponent<NavMeshObstacle>(go);
            obstacle.carving = carve;

            if (shape?.ToLower() == "capsule")
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                if (radius.HasValue) obstacle.radius = radius.Value;
                if (height.HasValue) obstacle.height = height.Value;
            }
            else
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                if (size.HasValue) obstacle.size = size.Value;
            }

            return ToolResult.Text($"Added NavMeshObstacle ({shape}) to '{go.name}'");
        }

        [McpTool("navmesh_add_surface", "Add a NavMeshSurface component for baking (requires AI Navigation package)",
            Group = "navmesh")]
        public static ToolResult AddSurface(
            [Desc("Name or path of the target GameObject (or empty to create new)")] string target = null)
        {
            // NavMeshSurface is in the AI Navigation package (Unity.AI.Navigation)
            var surfaceType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
                return ToolResult.Error("NavMeshSurface not available. Install the 'AI Navigation' package via Package Manager.");

            GameObject go;
            if (!string.IsNullOrEmpty(target))
            {
                go = GameObjectTools.FindGameObject(target, null);
                if (go == null)
                    return ToolResult.Error($"GameObject not found: {target}");
            }
            else
            {
                go = new GameObject("NavMesh Surface");
                UndoHelper.RegisterCreatedObject(go, "Create NavMesh Surface");
            }

            if (go.GetComponent(surfaceType) != null)
                return ToolResult.Error($"NavMeshSurface already exists on '{go.name}'");

            Undo.AddComponent(go, surfaceType);
            return ToolResult.Text($"Added NavMeshSurface to '{go.name}'. Use navmesh_bake to bake.");
        }

        [McpTool("navmesh_bake", "Bake NavMesh using legacy baking system",
            Group = "navmesh")]
        public static ToolResult Bake()
        {
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
            return ToolResult.Text("NavMesh baked successfully");
        }

        [McpTool("navmesh_clear", "Clear the baked NavMesh data",
            Group = "navmesh")]
        public static ToolResult Clear()
        {
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
#pragma warning restore CS0618
            return ToolResult.Text("NavMesh data cleared");
        }

        [McpTool("navmesh_get_info", "Get NavMeshAgent info on a GameObject",
            Group = "navmesh", ReadOnly = true)]
        public static ToolResult GetAgentInfo(
            [Desc("Name or path of the target GameObject")] string target)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
                return ToolResult.Error($"No NavMeshAgent on '{target}'");

            return ToolResult.Json(new
            {
                gameObject = go.name,
                speed = agent.speed,
                angularSpeed = agent.angularSpeed,
                acceleration = agent.acceleration,
                stoppingDistance = agent.stoppingDistance,
                radius = agent.radius,
                height = agent.height,
                autoBraking = agent.autoBraking,
                autoRepath = agent.autoRepath,
                areaMask = agent.areaMask,
                agentTypeID = agent.agentTypeID,
                isOnNavMesh = agent.isOnNavMesh,
            });
        }

        [McpTool("navmesh_modify_agent", "Modify NavMeshAgent properties",
            Group = "navmesh")]
        public static ToolResult ModifyAgent(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Speed")] float? speed = null,
            [Desc("Angular speed")] float? angularSpeed = null,
            [Desc("Acceleration")] float? acceleration = null,
            [Desc("Stopping distance")] float? stoppingDistance = null,
            [Desc("Radius")] float? radius = null,
            [Desc("Height")] float? height = null,
            [Desc("Auto braking")] bool? autoBraking = null,
            [Desc("Auto repath")] bool? autoRepath = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
                return ToolResult.Error($"No NavMeshAgent on '{target}'");

            Undo.RecordObject(agent, "Modify NavMeshAgent");
            var changes = new List<string>();

            if (speed.HasValue) { agent.speed = speed.Value; changes.Add($"speed={speed.Value}"); }
            if (angularSpeed.HasValue) { agent.angularSpeed = angularSpeed.Value; changes.Add($"angularSpeed={angularSpeed.Value}"); }
            if (acceleration.HasValue) { agent.acceleration = acceleration.Value; changes.Add($"acceleration={acceleration.Value}"); }
            if (stoppingDistance.HasValue) { agent.stoppingDistance = stoppingDistance.Value; changes.Add($"stoppingDistance={stoppingDistance.Value}"); }
            if (radius.HasValue) { agent.radius = radius.Value; changes.Add($"radius={radius.Value}"); }
            if (height.HasValue) { agent.height = height.Value; changes.Add($"height={height.Value}"); }
            if (autoBraking.HasValue) { agent.autoBraking = autoBraking.Value; changes.Add($"autoBraking={autoBraking.Value}"); }
            if (autoRepath.HasValue) { agent.autoRepath = autoRepath.Value; changes.Add($"autoRepath={autoRepath.Value}"); }

            EditorUtility.SetDirty(agent);
            if (changes.Count == 0) return ToolResult.Text($"No properties changed on NavMeshAgent '{target}'");
            return ToolResult.Text($"NavMeshAgent '{target}' updated: {string.Join(", ", changes)}");
        }
    }
}
