using System.Collections.Generic;
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

        [McpTool("vfx_modify_particle", "Modify Particle System module parameters. Covers main, emission, shape, textureSheetAnimation, colorOverLifetime, sizeOverLifetime, velocityOverLifetime, rotationOverLifetime, and noise modules. For advanced/uncommon properties, use component_modify with nested fields instead.",
            Group = "vfx")]
        public static ToolResult ModifyParticle(
            [Desc("Name or path of the Particle System GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            // --- Main module ---
            [Desc("Start lifetime (main)")] float? startLifetime = null,
            [Desc("Start speed (main)")] float? startSpeed = null,
            [Desc("Start size (main)")] float? startSize = null,
            [Desc("Start color as {r,g,b,a} (main)")] Color? startColor = null,
            [Desc("Start rotation in degrees (main)")] float? startRotation = null,
            [Desc("Max particles (main)")] int? maxParticles = null,
            [Desc("Gravity modifier (main)")] float? gravityModifier = null,
            [Desc("Simulation space: 'local' or 'world' (main)")] string simulationSpace = null,
            [Desc("Looping (main)")] bool? looping = null,
            [Desc("Play on awake (main)")] bool? playOnAwake = null,
            [Desc("Duration in seconds (main)")] float? duration = null,
            // --- Emission module ---
            [Desc("Emission rate over time")] float? emissionRate = null,
            [Desc("Enable/disable emission module")] bool? emissionEnabled = null,
            // --- Shape module ---
            [Desc("Shape type: 'sphere', 'cone', 'box', 'circle', 'hemisphere', 'donut', 'edge'")] string shapeType = null,
            [Desc("Shape radius")] float? shapeRadius = null,
            [Desc("Shape angle (cone)")] float? shapeAngle = null,
            [Desc("Shape arc (0-360)")] float? shapeArc = null,
            // --- TextureSheetAnimation module ---
            [Desc("Enable texture sheet animation")] bool? texSheetEnabled = null,
            [Desc("Texture tiles X")] int? texSheetTilesX = null,
            [Desc("Texture tiles Y")] int? texSheetTilesY = null,
            [Desc("Animation type: 'wholeSheet' or 'singleRow'")] string texSheetAnimationType = null,
            [Desc("Cycle count")] int? texSheetCycleCount = null,
            [Desc("Single row index (when animationType='singleRow')")] int? texSheetRowIndex = null,
            // --- ColorOverLifetime module ---
            [Desc("Enable color over lifetime")] bool? colorOverLifetimeEnabled = null,
            [Desc("Color gradient start {r,g,b,a}")] Color? colorOverLifetimeStart = null,
            [Desc("Color gradient end {r,g,b,a}")] Color? colorOverLifetimeEnd = null,
            // --- SizeOverLifetime module ---
            [Desc("Enable size over lifetime")] bool? sizeOverLifetimeEnabled = null,
            [Desc("Size multiplier at start (0-1 curve start)")] float? sizeOverLifetimeStart = null,
            [Desc("Size multiplier at end (0-1 curve end)")] float? sizeOverLifetimeEnd = null,
            // --- VelocityOverLifetime module ---
            [Desc("Enable velocity over lifetime")] bool? velocityEnabled = null,
            [Desc("Velocity X constant")] float? velocityX = null,
            [Desc("Velocity Y constant")] float? velocityY = null,
            [Desc("Velocity Z constant")] float? velocityZ = null,
            [Desc("Velocity space: 'local' or 'world'")] string velocitySpace = null,
            // --- RotationOverLifetime module ---
            [Desc("Enable rotation over lifetime")] bool? rotationOverLifetimeEnabled = null,
            [Desc("Angular velocity in degrees/sec (Z axis)")] float? angularVelocity = null,
            // --- Noise module ---
            [Desc("Enable noise")] bool? noiseEnabled = null,
            [Desc("Noise strength")] float? noiseStrength = null,
            [Desc("Noise frequency")] float? noiseFrequency = null,
            [Desc("Noise scroll speed")] float? noiseScrollSpeed = null,
            [Desc("Noise octave count (1-4)")] int? noiseOctaves = null,
            [Desc("Noise damping")] bool? noiseDamping = null)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                return ToolResult.Error($"No ParticleSystem on '{go.name}'");

            Undo.RecordObject(ps, "Modify ParticleSystem");
            var changes = new List<string>();

            // --- Main module ---
            var main = ps.main;
            if (startLifetime.HasValue) { main.startLifetime = startLifetime.Value; changes.Add("startLifetime"); }
            if (startSpeed.HasValue) { main.startSpeed = startSpeed.Value; changes.Add("startSpeed"); }
            if (startSize.HasValue) { main.startSize = startSize.Value; changes.Add("startSize"); }
            if (startColor.HasValue) { main.startColor = startColor.Value; changes.Add("startColor"); }
            if (startRotation.HasValue) { main.startRotation = startRotation.Value * Mathf.Deg2Rad; changes.Add("startRotation"); }
            if (maxParticles.HasValue) { main.maxParticles = maxParticles.Value; changes.Add("maxParticles"); }
            if (gravityModifier.HasValue) { main.gravityModifier = gravityModifier.Value; changes.Add("gravityModifier"); }
            if (looping.HasValue) { main.loop = looping.Value; changes.Add("looping"); }
            if (playOnAwake.HasValue) { main.playOnAwake = playOnAwake.Value; changes.Add("playOnAwake"); }
            if (duration.HasValue) { main.duration = duration.Value; changes.Add("duration"); }
            if (!string.IsNullOrEmpty(simulationSpace))
            {
                main.simulationSpace = simulationSpace.ToLower() == "world"
                    ? ParticleSystemSimulationSpace.World
                    : ParticleSystemSimulationSpace.Local;
                changes.Add("simulationSpace");
            }

            // --- Emission module ---
            if (emissionEnabled.HasValue || emissionRate.HasValue)
            {
                var emission = ps.emission;
                if (emissionEnabled.HasValue) { emission.enabled = emissionEnabled.Value; changes.Add("emission.enabled"); }
                if (emissionRate.HasValue) { emission.rateOverTime = emissionRate.Value; changes.Add("emission.rateOverTime"); }
            }

            // --- Shape module ---
            if (!string.IsNullOrEmpty(shapeType) || shapeRadius.HasValue || shapeAngle.HasValue || shapeArc.HasValue)
            {
                var shape = ps.shape;
                if (!string.IsNullOrEmpty(shapeType))
                {
                    shape.shapeType = shapeType.ToLower() switch
                    {
                        "sphere" => ParticleSystemShapeType.Sphere,
                        "hemisphere" => ParticleSystemShapeType.Hemisphere,
                        "cone" => ParticleSystemShapeType.Cone,
                        "box" => ParticleSystemShapeType.Box,
                        "circle" => ParticleSystemShapeType.Circle,
                        "donut" => ParticleSystemShapeType.Donut,
                        "edge" => ParticleSystemShapeType.SingleSidedEdge,
                        _ => shape.shapeType
                    };
                    changes.Add("shape.type");
                }
                if (shapeRadius.HasValue) { shape.radius = shapeRadius.Value; changes.Add("shape.radius"); }
                if (shapeAngle.HasValue) { shape.angle = shapeAngle.Value; changes.Add("shape.angle"); }
                if (shapeArc.HasValue) { shape.arc = shapeArc.Value; changes.Add("shape.arc"); }
            }

            // --- TextureSheetAnimation module ---
            if (texSheetEnabled.HasValue || texSheetTilesX.HasValue || texSheetTilesY.HasValue ||
                texSheetAnimationType != null || texSheetCycleCount.HasValue || texSheetRowIndex.HasValue)
            {
                var tex = ps.textureSheetAnimation;
                if (texSheetEnabled.HasValue) { tex.enabled = texSheetEnabled.Value; changes.Add("textureSheet.enabled"); }
                if (texSheetTilesX.HasValue) { tex.numTilesX = texSheetTilesX.Value; changes.Add("textureSheet.tilesX"); }
                if (texSheetTilesY.HasValue) { tex.numTilesY = texSheetTilesY.Value; changes.Add("textureSheet.tilesY"); }
                if (!string.IsNullOrEmpty(texSheetAnimationType))
                {
                    tex.animation = texSheetAnimationType.ToLower() == "singlerow"
                        ? ParticleSystemAnimationType.SingleRow
                        : ParticleSystemAnimationType.WholeSheet;
                    changes.Add("textureSheet.animationType");
                }
                if (texSheetCycleCount.HasValue) { tex.cycleCount = texSheetCycleCount.Value; changes.Add("textureSheet.cycleCount"); }
                if (texSheetRowIndex.HasValue) { tex.rowIndex = texSheetRowIndex.Value; changes.Add("textureSheet.rowIndex"); }
            }

            // --- ColorOverLifetime module ---
            if (colorOverLifetimeEnabled.HasValue || colorOverLifetimeStart.HasValue || colorOverLifetimeEnd.HasValue)
            {
                var col = ps.colorOverLifetime;
                if (colorOverLifetimeEnabled.HasValue) { col.enabled = colorOverLifetimeEnabled.Value; changes.Add("colorOverLifetime.enabled"); }
                if (colorOverLifetimeStart.HasValue || colorOverLifetimeEnd.HasValue)
                {
                    var startCol = colorOverLifetimeStart ?? Color.white;
                    var endCol = colorOverLifetimeEnd ?? new Color(startCol.r, startCol.g, startCol.b, 0f);
                    var gradient = new Gradient();
                    gradient.SetKeys(
                        new[] { new GradientColorKey(startCol, 0f), new GradientColorKey(endCol, 1f) },
                        new[] { new GradientAlphaKey(startCol.a, 0f), new GradientAlphaKey(endCol.a, 1f) });
                    col.color = new ParticleSystem.MinMaxGradient(gradient);
                    changes.Add("colorOverLifetime.gradient");
                }
            }

            // --- SizeOverLifetime module ---
            if (sizeOverLifetimeEnabled.HasValue || sizeOverLifetimeStart.HasValue || sizeOverLifetimeEnd.HasValue)
            {
                var size = ps.sizeOverLifetime;
                if (sizeOverLifetimeEnabled.HasValue) { size.enabled = sizeOverLifetimeEnabled.Value; changes.Add("sizeOverLifetime.enabled"); }
                if (sizeOverLifetimeStart.HasValue || sizeOverLifetimeEnd.HasValue)
                {
                    float s = sizeOverLifetimeStart ?? 1f;
                    float e = sizeOverLifetimeEnd ?? 0f;
                    var curve = new AnimationCurve(new Keyframe(0f, s), new Keyframe(1f, e));
                    size.size = new ParticleSystem.MinMaxCurve(1f, curve);
                    changes.Add("sizeOverLifetime.curve");
                }
            }

            // --- VelocityOverLifetime module ---
            if (velocityEnabled.HasValue || velocityX.HasValue || velocityY.HasValue || velocityZ.HasValue || velocitySpace != null)
            {
                var vel = ps.velocityOverLifetime;
                if (velocityEnabled.HasValue) { vel.enabled = velocityEnabled.Value; changes.Add("velocity.enabled"); }
                if (velocityX.HasValue) { vel.x = velocityX.Value; changes.Add("velocity.x"); }
                if (velocityY.HasValue) { vel.y = velocityY.Value; changes.Add("velocity.y"); }
                if (velocityZ.HasValue) { vel.z = velocityZ.Value; changes.Add("velocity.z"); }
                if (!string.IsNullOrEmpty(velocitySpace))
                {
                    vel.space = velocitySpace.ToLower() == "world"
                        ? ParticleSystemSimulationSpace.World
                        : ParticleSystemSimulationSpace.Local;
                    changes.Add("velocity.space");
                }
            }

            // --- RotationOverLifetime module ---
            if (rotationOverLifetimeEnabled.HasValue || angularVelocity.HasValue)
            {
                var rot = ps.rotationOverLifetime;
                if (rotationOverLifetimeEnabled.HasValue) { rot.enabled = rotationOverLifetimeEnabled.Value; changes.Add("rotation.enabled"); }
                if (angularVelocity.HasValue) { rot.z = angularVelocity.Value * Mathf.Deg2Rad; changes.Add("rotation.z"); }
            }

            // --- Noise module ---
            if (noiseEnabled.HasValue || noiseStrength.HasValue || noiseFrequency.HasValue ||
                noiseScrollSpeed.HasValue || noiseOctaves.HasValue || noiseDamping.HasValue)
            {
                var noise = ps.noise;
                if (noiseEnabled.HasValue) { noise.enabled = noiseEnabled.Value; changes.Add("noise.enabled"); }
                if (noiseStrength.HasValue) { noise.strength = noiseStrength.Value; changes.Add("noise.strength"); }
                if (noiseFrequency.HasValue) { noise.frequency = noiseFrequency.Value; changes.Add("noise.frequency"); }
                if (noiseScrollSpeed.HasValue) { noise.scrollSpeed = noiseScrollSpeed.Value; changes.Add("noise.scrollSpeed"); }
                if (noiseOctaves.HasValue) { noise.octaveCount = noiseOctaves.Value; changes.Add("noise.octaveCount"); }
                if (noiseDamping.HasValue) { noise.damping = noiseDamping.Value; changes.Add("noise.damping"); }
            }

            if (changes.Count == 0)
                return ToolResult.Text($"No properties changed on ParticleSystem '{go.name}'");

            return ToolResult.Json(new
            {
                name = go.name,
                modified = changes,
                message = $"Modified ParticleSystem '{go.name}': {string.Join(", ", changes)}"
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

        [McpTool("vfx_get_info", "Get detailed info about a Particle System including all module states",
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
            var tex = ps.textureSheetAnimation;
            var col = ps.colorOverLifetime;
            var size = ps.sizeOverLifetime;
            var vel = ps.velocityOverLifetime;
            var rot = ps.rotationOverLifetime;
            var noise = ps.noise;
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
                    playOnAwake = main.playOnAwake,
                    startLifetime = FormatMinMaxCurve(main.startLifetime),
                    startSpeed = FormatMinMaxCurve(main.startSpeed),
                    startSize = FormatMinMaxCurve(main.startSize),
                    startRotation = main.startRotation.constant * Mathf.Rad2Deg,
                    startColor = FormatColor(main.startColor.color),
                    maxParticles = main.maxParticles,
                    simulationSpace = main.simulationSpace.ToString(),
                    gravityModifier = main.gravityModifier.constant
                },
                emission = new
                {
                    enabled = emission.enabled,
                    rateOverTime = FormatMinMaxCurve(emission.rateOverTime),
                    burstCount = emission.burstCount
                },
                shape = new
                {
                    enabled = shape.enabled,
                    shapeType = shape.shapeType.ToString(),
                    radius = shape.radius,
                    angle = shape.angle,
                    arc = shape.arc
                },
                textureSheetAnimation = new
                {
                    enabled = tex.enabled,
                    tilesX = tex.numTilesX,
                    tilesY = tex.numTilesY,
                    animationType = tex.animation.ToString(),
                    cycleCount = tex.cycleCount,
                    rowIndex = tex.rowIndex
                },
                colorOverLifetime = new
                {
                    enabled = col.enabled
                },
                sizeOverLifetime = new
                {
                    enabled = size.enabled
                },
                velocityOverLifetime = new
                {
                    enabled = vel.enabled,
                    x = FormatMinMaxCurve(vel.x),
                    y = FormatMinMaxCurve(vel.y),
                    z = FormatMinMaxCurve(vel.z),
                    space = vel.space.ToString()
                },
                rotationOverLifetime = new
                {
                    enabled = rot.enabled,
                    z = rot.enabled ? rot.z.constant * Mathf.Rad2Deg : 0f
                },
                noise = new
                {
                    enabled = noise.enabled,
                    strength = noise.enabled ? noise.strength.constant : 0f,
                    frequency = noise.enabled ? noise.frequency : 0f,
                    scrollSpeed = noise.enabled ? noise.scrollSpeed.constant : 0f,
                    octaveCount = noise.enabled ? noise.octaveCount : 0,
                    damping = noise.enabled && noise.damping
                },
                renderer = renderer != null ? new
                {
                    renderMode = renderer.renderMode.ToString(),
                    material = renderer.sharedMaterial?.name
                } : null
            });
        }

        private static object FormatMinMaxCurve(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return new { min = curve.constantMin, max = curve.constantMax };
                default:
                    return new { mode = curve.mode.ToString(), multiplier = curve.curveMultiplier };
            }
        }

        private static object FormatColor(Color c)
        {
            return new { r = c.r, g = c.g, b = c.b, a = c.a };
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
            var changes = new List<string>();

            if (startWidth.HasValue) { lr.startWidth = startWidth.Value; changes.Add($"startWidth={startWidth.Value}"); }
            if (endWidth.HasValue) { lr.endWidth = endWidth.Value; changes.Add($"endWidth={endWidth.Value}"); }
            if (useWorldSpace.HasValue) { lr.useWorldSpace = useWorldSpace.Value; changes.Add($"useWorldSpace={useWorldSpace.Value}"); }
            if (loop.HasValue) { lr.loop = loop.Value; changes.Add($"loop={loop.Value}"); }
            if (cornerVertices.HasValue) { lr.numCornerVertices = cornerVertices.Value; changes.Add($"cornerVertices={cornerVertices.Value}"); }
            if (endCapVertices.HasValue) { lr.numCapVertices = endCapVertices.Value; changes.Add($"endCapVertices={endCapVertices.Value}"); }

            if (startColor.HasValue || endColor.HasValue)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(startColor ?? Color.white, 0), new GradientColorKey(endColor ?? Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                lr.colorGradient = gradient;
                changes.Add("colorGradient=updated");
            }

            if (!string.IsNullOrEmpty(material))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(material);
                if (mat != null) { lr.sharedMaterial = mat; changes.Add($"material={material}"); }
            }

            if (points != null && points.Length > 0)
            {
                lr.positionCount = points.Length;
                lr.SetPositions(points);
                changes.Add($"points={points.Length}");
            }

            EditorUtility.SetDirty(lr);
            if (changes.Count == 0) return ToolResult.Text($"No properties changed on LineRenderer '{target}'");
            return ToolResult.Text($"LineRenderer '{target}' updated: {string.Join(", ", changes)}");
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
