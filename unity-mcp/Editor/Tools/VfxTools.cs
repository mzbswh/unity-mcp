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

        [McpTool("vfx_create_line", "Create a LineRenderer on a new or existing GameObject",
            Group = "vfx")]
        public static ToolResult CreateLine(
            [Desc("Name of the GameObject")] string name = "Line",
            [Desc("Target existing GameObject (null = create new)")] string target = null,
            [Desc("Points as array of {x,y,z}")] Vector3[] points = null,
            [Desc("Start width")] float startWidth = 0.1f,
            [Desc("End width")] float endWidth = 0.1f,
            [Desc("Start color")] Color? startColor = null,
            [Desc("End color")] Color? endColor = null,
            [Desc("Material asset path")] string material = null,
            [Desc("Use world space")] bool useWorldSpace = true)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(target))
            {
                go = GameObjectTools.FindGameObject(target, null);
                if (go == null)
                    return ToolResult.Error($"GameObject not found: {target}");
            }
            else
            {
                go = new GameObject(name);
                UndoHelper.RegisterCreatedObject(go, "Create LineRenderer");
            }

            var lr = go.GetComponent<LineRenderer>();
            if (lr == null) lr = go.AddComponent<LineRenderer>();

            lr.startWidth = startWidth;
            lr.endWidth = endWidth;
            lr.useWorldSpace = useWorldSpace;

            if (startColor.HasValue || endColor.HasValue)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(startColor ?? Color.white, 0), new GradientColorKey(endColor ?? Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                lr.colorGradient = gradient;
            }

            if (!string.IsNullOrEmpty(material))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(material);
                if (mat != null) lr.sharedMaterial = mat;
            }

            if (points != null && points.Length > 0)
            {
                lr.positionCount = points.Length;
                lr.SetPositions(points);
            }

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                pointCount = lr.positionCount,
                message = $"Created LineRenderer on '{go.name}'"
            });
        }

        [McpTool("vfx_modify_line", "Modify a LineRenderer's properties",
            Group = "vfx")]
        public static ToolResult ModifyLine(
            [Desc("Name or path of the GameObject")] string target,
            [Desc("Points as array of {x,y,z}")] Vector3[] points = null,
            [Desc("Start width")] float? startWidth = null,
            [Desc("End width")] float? endWidth = null,
            [Desc("Start color")] Color? startColor = null,
            [Desc("End color")] Color? endColor = null,
            [Desc("Material asset path")] string material = null,
            [Desc("Use world space")] bool? useWorldSpace = null,
            [Desc("Loop the line")] bool? loop = null,
            [Desc("Corner vertices")] int? cornerVertices = null,
            [Desc("End cap vertices")] int? endCapVertices = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var lr = go.GetComponent<LineRenderer>();
            if (lr == null)
                return ToolResult.Error($"No LineRenderer on '{target}'");

            Undo.RecordObject(lr, "Modify LineRenderer");
            int modified = 0;

            if (startWidth.HasValue) { lr.startWidth = startWidth.Value; modified++; }
            if (endWidth.HasValue) { lr.endWidth = endWidth.Value; modified++; }
            if (useWorldSpace.HasValue) { lr.useWorldSpace = useWorldSpace.Value; modified++; }
            if (loop.HasValue) { lr.loop = loop.Value; modified++; }
            if (cornerVertices.HasValue) { lr.numCornerVertices = cornerVertices.Value; modified++; }
            if (endCapVertices.HasValue) { lr.numCapVertices = endCapVertices.Value; modified++; }

            if (startColor.HasValue || endColor.HasValue)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(startColor ?? Color.white, 0), new GradientColorKey(endColor ?? Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                lr.colorGradient = gradient;
                modified++;
            }

            if (!string.IsNullOrEmpty(material))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(material);
                if (mat != null) { lr.sharedMaterial = mat; modified++; }
            }

            if (points != null && points.Length > 0)
            {
                lr.positionCount = points.Length;
                lr.SetPositions(points);
                modified++;
            }

            EditorUtility.SetDirty(lr);
            return ToolResult.Text($"Modified {modified} properties on LineRenderer '{target}'");
        }

        [McpTool("vfx_create_trail", "Create a TrailRenderer on a new or existing GameObject",
            Group = "vfx")]
        public static ToolResult CreateTrail(
            [Desc("Name of the GameObject")] string name = "Trail",
            [Desc("Target existing GameObject (null = create new)")] string target = null,
            [Desc("Trail time (seconds)")] float time = 1f,
            [Desc("Start width")] float startWidth = 0.5f,
            [Desc("End width")] float endWidth = 0f,
            [Desc("Start color")] Color? startColor = null,
            [Desc("End color")] Color? endColor = null,
            [Desc("Material asset path")] string material = null,
            [Desc("Min vertex distance")] float minVertexDistance = 0.1f)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(target))
            {
                go = GameObjectTools.FindGameObject(target, null);
                if (go == null)
                    return ToolResult.Error($"GameObject not found: {target}");
            }
            else
            {
                go = new GameObject(name);
                UndoHelper.RegisterCreatedObject(go, "Create TrailRenderer");
            }

            var tr = go.GetComponent<TrailRenderer>();
            if (tr == null) tr = go.AddComponent<TrailRenderer>();

            tr.time = time;
            tr.startWidth = startWidth;
            tr.endWidth = endWidth;
            tr.minVertexDistance = minVertexDistance;

            if (startColor.HasValue || endColor.HasValue)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(startColor ?? Color.white, 0), new GradientColorKey(endColor ?? Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                tr.colorGradient = gradient;
            }

            if (!string.IsNullOrEmpty(material))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(material);
                if (mat != null) tr.sharedMaterial = mat;
            }

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                message = $"Created TrailRenderer on '{go.name}'"
            });
        }
    }
}
