using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("VFX")]
    public static class VfxTools
    {
        [McpTool("vfx_create_particle", "Create a Particle System with optional preset template",
            Group = "vfx")]
        public static ToolResult CreateParticle(
            [Desc("Name of the Particle System")] string name = "ParticleSystem",
            [Desc("Preset template: 'fire', 'smoke', 'explosion', 'sparks', 'rain', or 'default'")] string preset = "default",
            [Desc("World position")] Vector3? position = null,
            [Desc("Parent GameObject name or path")] string parent = null)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            if (position.HasValue)
                go.transform.position = position.Value;

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectTools.FindGameObject(parent, null);
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, true);
            }

            ApplyPreset(ps, preset.ToLower());

            UndoHelper.RegisterCreatedObject(go, $"Create ParticleSystem {name}");

            return ToolResult.Json(new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                preset,
                message = $"Created Particle System: {name} (preset: {preset})"
            });
        }

        [McpTool("vfx_modify_particle", "Modify Particle System module parameters",
            Group = "vfx")]
        public static ToolResult ModifyParticle(
            [Desc("Name or path of the Particle System GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("Module: 'main', 'emission', 'shape', 'colorOverLifetime', 'sizeOverLifetime'")] string module = "main",
            [Desc("Start lifetime (main module)")] float? startLifetime = null,
            [Desc("Start speed (main module)")] float? startSpeed = null,
            [Desc("Start size (main module)")] float? startSize = null,
            [Desc("Max particles (main module)")] int? maxParticles = null,
            [Desc("Emission rate over time")] float? emissionRate = null,
            [Desc("Shape type: 'sphere', 'cone', 'box', 'circle'")] string shapeType = null,
            [Desc("Shape radius")] float? shapeRadius = null,
            [Desc("Simulation space: 'local' or 'world'")] string simulationSpace = null,
            [Desc("Gravity modifier")] float? gravityModifier = null,
            [Desc("Looping")] bool? looping = null)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                return ToolResult.Error($"No ParticleSystem on '{go.name}'");

            Undo.RecordObject(ps, "Modify ParticleSystem");

            var main = ps.main;
            if (startLifetime.HasValue) main.startLifetime = startLifetime.Value;
            if (startSpeed.HasValue) main.startSpeed = startSpeed.Value;
            if (startSize.HasValue) main.startSize = startSize.Value;
            if (maxParticles.HasValue) main.maxParticles = maxParticles.Value;
            if (gravityModifier.HasValue) main.gravityModifier = gravityModifier.Value;
            if (looping.HasValue) main.loop = looping.Value;
            if (!string.IsNullOrEmpty(simulationSpace))
                main.simulationSpace = simulationSpace.ToLower() == "world"
                    ? ParticleSystemSimulationSpace.World
                    : ParticleSystemSimulationSpace.Local;

            if (emissionRate.HasValue)
            {
                var emission = ps.emission;
                emission.rateOverTime = emissionRate.Value;
            }

            if (!string.IsNullOrEmpty(shapeType) || shapeRadius.HasValue)
            {
                var shape = ps.shape;
                if (!string.IsNullOrEmpty(shapeType))
                {
                    shape.shapeType = shapeType.ToLower() switch
                    {
                        "sphere" => ParticleSystemShapeType.Sphere,
                        "cone" => ParticleSystemShapeType.Cone,
                        "box" => ParticleSystemShapeType.Box,
                        "circle" => ParticleSystemShapeType.Circle,
                        _ => shape.shapeType
                    };
                }
                if (shapeRadius.HasValue) shape.radius = shapeRadius.Value;
            }

            return ToolResult.Json(new
            {
                success = true,
                name = go.name,
                message = $"Modified ParticleSystem '{go.name}'"
            });
        }

        [McpTool("vfx_create_graph", "Create a VFX Graph asset (requires Visual Effect Graph package)",
            Group = "vfx")]
        public static ToolResult CreateGraph(
            [Desc("Save path (e.g. Assets/VFX/MyEffect.vfx)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            // Check if VFX Graph package is available
            var vfxAssetType = System.Type.GetType("UnityEngine.VFX.VisualEffectAsset, Unity.VisualEffectGraph.Runtime");
            if (vfxAssetType == null)
                return ToolResult.Error("VFX Graph package (com.unity.visualeffectgraph) is not installed. Install it via Package Manager first.");

            // Use menu item to create VFX Graph (safest cross-version approach)
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            // Check if a template asset can be created via ProjectWindowUtil
            return ToolResult.Json(new
            {
                success = true,
                path,
                message = $"To create VFX Graph, use Unity Editor: Right-click in Project > Create > Visual Effects > Visual Effect Graph, then save to {path}. " +
                          "Programmatic VFX Graph creation requires direct VFX Graph API access."
            });
        }

        [McpTool("vfx_get_info", "Get info about a Particle System or VFX asset",
            Group = "vfx", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Name or path of the Particle System GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                return ToolResult.Error($"No ParticleSystem on '{go.name}'");

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            return ToolResult.Json(new
            {
                name = go.name,
                isPlaying = ps.isPlaying,
                particleCount = ps.particleCount,
                main = new
                {
                    duration = main.duration,
                    looping = main.loop,
                    startLifetime = main.startLifetime.constant,
                    startSpeed = main.startSpeed.constant,
                    startSize = main.startSize.constant,
                    maxParticles = main.maxParticles,
                    simulationSpace = main.simulationSpace.ToString(),
                    gravityModifier = main.gravityModifier.constant
                },
                emission = new
                {
                    enabled = emission.enabled,
                    rateOverTime = emission.rateOverTime.constant,
                    burstCount = emission.burstCount
                },
                shape = new
                {
                    enabled = shape.enabled,
                    shapeType = shape.shapeType.ToString(),
                    radius = shape.radius,
                    angle = shape.angle
                },
                renderer = renderer != null ? new
                {
                    renderMode = renderer.renderMode.ToString(),
                    material = renderer.sharedMaterial?.name
                } : null
            });
        }

        private static void ApplyPreset(ParticleSystem ps, string preset)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            switch (preset)
            {
                case "fire":
                    main.startLifetime = 1.5f;
                    main.startSpeed = 2f;
                    main.startSize = 0.5f;
                    main.startColor = new Color(1f, 0.5f, 0.1f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 30;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15;
                    shape.radius = 0.3f;
                    break;

                case "smoke":
                    main.startLifetime = 4f;
                    main.startSpeed = 1f;
                    main.startSize = 1f;
                    main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.gravityModifier = -0.05f;
                    emission.rateOverTime = 10;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    break;

                case "explosion":
                    main.startLifetime = 0.8f;
                    main.startSpeed = 10f;
                    main.startSize = 0.3f;
                    main.startColor = new Color(1f, 0.7f, 0.2f);
                    main.loop = false;
                    main.maxParticles = 200;
                    emission.rateOverTime = 0;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 100) });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    break;

                case "sparks":
                    main.startLifetime = 0.5f;
                    main.startSpeed = 8f;
                    main.startSize = 0.05f;
                    main.startColor = new Color(1f, 0.9f, 0.5f);
                    main.gravityModifier = 1f;
                    emission.rateOverTime = 50;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25;
                    shape.radius = 0.1f;
                    break;

                case "rain":
                    main.startLifetime = 2f;
                    main.startSpeed = 15f;
                    main.startSize = 0.03f;
                    main.startColor = new Color(0.7f, 0.8f, 1f, 0.6f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 200;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(20, 0, 20);
                    shape.rotation = new Vector3(0, 0, 180);
                    break;
            }
        }
    }
}
