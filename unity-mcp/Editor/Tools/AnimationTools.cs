using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Animation")]
    public static class AnimationTools
    {
        [McpTool("animation_create_clip", "Create an AnimationClip asset",
            Group = "animation")]
        public static ToolResult CreateClip(
            [Desc("Save path (e.g. Assets/Animations/Idle.anim)")] string path,
            [Desc("Whether the clip should loop")] bool loop = false,
            [Desc("Clip length in seconds")] float length = 1f)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var clip = new AnimationClip();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Set a default keyframe so clip has proper length
            clip.SetCurve("", typeof(Transform), "localPosition.x",
                AnimationCurve.Constant(0, length, 0));

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                path,
                loop,
                length,
                message = $"Created AnimationClip: {path}"
            });
        }

        [McpTool("animation_manage_controller", "Create or modify an AnimatorController",
            Group = "animation")]
        public static ToolResult ManageController(
            [Desc("Controller asset path (e.g. Assets/Animations/AC_Player.controller)")] string path,
            [Desc("Action: 'create', 'add_state', 'add_parameter', 'info'")] string action,
            [Desc("State name (for add_state)")] string stateName = null,
            [Desc("Animation clip path to assign to state (for add_state)")] string clipPath = null,
            [Desc("Parameter name (for add_parameter)")] string paramName = null,
            [Desc("Parameter type: 'float', 'int', 'bool', 'trigger' (for add_parameter)")] string paramType = "float",
            [Desc("Layer index (default: 0)")] int layer = 0)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            switch (action.ToLower())
            {
                case "create":
                    return CreateController(path);
                case "add_state":
                    return AddState(path, stateName, clipPath, layer);
                case "add_parameter":
                    return AddParameter(path, paramName, paramType);
                case "info":
                    return GetInfo(path);
                default:
                    return ToolResult.Error($"Unknown action: {action}. Use: create, add_state, add_parameter, info");
            }
        }

        private static ToolResult CreateController(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            return ToolResult.Json(new
            {
                success = true,
                path,
                message = $"Created AnimatorController: {path}"
            });
        }

        private static ToolResult AddState(string path, string stateName, string clipPath, int layer)
        {
            if (string.IsNullOrEmpty(stateName))
                return ToolResult.Error("stateName is required for add_state");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            if (layer >= controller.layers.Length)
                return ToolResult.Error($"Layer {layer} does not exist (controller has {controller.layers.Length} layers)");

            var stateMachine = controller.layers[layer].stateMachine;
            var state = stateMachine.AddState(stateName);

            if (!string.IsNullOrEmpty(clipPath))
            {
                var pvClip = PathValidator.QuickValidate(clipPath);
                if (!pvClip.IsValid) return ToolResult.Error(pvClip.Error);

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip != null)
                    state.motion = clip;
                else
                    return ToolResult.Error($"AnimationClip not found: {clipPath}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                stateName,
                layer,
                clipAssigned = !string.IsNullOrEmpty(clipPath),
                message = $"Added state '{stateName}' to layer {layer}"
            });
        }

        private static ToolResult AddParameter(string path, string paramName, string paramType)
        {
            if (string.IsNullOrEmpty(paramName))
                return ToolResult.Error("paramName is required for add_parameter");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            var type = paramType.ToLower() switch
            {
                "float" => AnimatorControllerParameterType.Float,
                "int" => AnimatorControllerParameterType.Int,
                "bool" => AnimatorControllerParameterType.Bool,
                "trigger" => AnimatorControllerParameterType.Trigger,
                _ => AnimatorControllerParameterType.Float
            };

            controller.AddParameter(paramName, type);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                paramName,
                paramType = type.ToString(),
                message = $"Added parameter '{paramName}' ({type})"
            });
        }

        private static ToolResult GetInfo(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            var layers = controller.layers.Select((l, i) => new
            {
                index = i,
                name = l.name,
                stateCount = l.stateMachine.states.Length,
                states = l.stateMachine.states.Select(s => new
                {
                    name = s.state.name,
                    hasMotion = s.state.motion != null,
                    motionName = s.state.motion?.name,
                    speed = s.state.speed,
                    transitions = s.state.transitions.Select(t => new
                    {
                        destinationState = t.destinationState?.name,
                        hasExitTime = t.hasExitTime,
                        exitTime = t.exitTime,
                        duration = t.duration,
                        conditionCount = t.conditions.Length,
                    })
                })
            });

            var parameters = controller.parameters.Select(p => new
            {
                name = p.name,
                type = p.type.ToString(),
                defaultFloat = p.defaultFloat,
                defaultInt = p.defaultInt,
                defaultBool = p.defaultBool
            });

            return ToolResult.Json(new
            {
                path,
                layerCount = controller.layers.Length,
                parameterCount = controller.parameters.Length,
                layers,
                parameters
            });
        }

        [McpTool("animation_add_transition", "Add a transition between two states in an AnimatorController",
            Group = "animation")]
        public static ToolResult AddTransition(
            [Desc("Controller asset path")] string path,
            [Desc("Source state name (or 'AnyState' / 'Entry')")] string from,
            [Desc("Destination state name (or 'Exit')")] string to,
            [Desc("Has exit time")] bool hasExitTime = true,
            [Desc("Exit time (0-1)")] float exitTime = 0.9f,
            [Desc("Transition duration in seconds")] float duration = 0.25f,
            [Desc("Layer index")] int layer = 0,
            [Desc("Conditions as array of {parameter, mode, threshold}. Mode: If, IfNot, Greater, Less, Equals, NotEqual")] Newtonsoft.Json.Linq.JArray conditions = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            if (layer >= controller.layers.Length)
                return ToolResult.Error($"Layer {layer} does not exist");

            var sm = controller.layers[layer].stateMachine;

            // Find destination state
            AnimatorState destState = null;
            bool toExit = to?.ToLower() == "exit";
            if (!toExit)
            {
                destState = sm.states.FirstOrDefault(s => s.state.name == to).state;
                if (destState == null)
                    return ToolResult.Error($"Destination state '{to}' not found in layer {layer}");
            }

            AnimatorStateTransition transition;
            string fromLower = from?.ToLower();

            if (fromLower == "anystate")
            {
                transition = toExit ? null : sm.AddAnyStateTransition(destState);
                if (transition == null)
                    return ToolResult.Error("Cannot add AnyState → Exit transition");
            }
            else if (fromLower == "entry")
            {
                if (destState == null)
                    return ToolResult.Error("Entry → Exit is not valid");
                sm.defaultState = destState;
                return ToolResult.Text($"Set default state to '{to}' in layer {layer}");
            }
            else
            {
                var srcState = sm.states.FirstOrDefault(s => s.state.name == from).state;
                if (srcState == null)
                    return ToolResult.Error($"Source state '{from}' not found in layer {layer}");

                transition = toExit
                    ? srcState.AddExitTransition()
                    : srcState.AddTransition(destState);
            }

            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.hasFixedDuration = true;

            // Add conditions
            if (conditions != null)
            {
                foreach (var c in conditions)
                {
                    string paramName = c["parameter"]?.ToString();
                    string modeStr = c["mode"]?.ToString() ?? "If";
                    float threshold = c["threshold"]?.ToObject<float>() ?? 0f;

                    if (string.IsNullOrEmpty(paramName)) continue;

                    if (System.Enum.TryParse<AnimatorConditionMode>(modeStr, true, out var mode))
                        transition.AddCondition(mode, threshold, paramName);
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                from,
                to,
                hasExitTime,
                duration,
                conditionCount = transition.conditions.Length,
                message = $"Added transition: {from} → {to}"
            });
        }

        [McpTool("animation_add_layer", "Add a new layer to an AnimatorController",
            Group = "animation")]
        public static ToolResult AddLayer(
            [Desc("Controller asset path")] string path,
            [Desc("Layer name")] string layerName,
            [Desc("Layer weight (0-1)")] float weight = 1f,
            [Desc("Blending mode: Override, Additive")] string blendingMode = "Override")
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            var newLayer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = weight,
                stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy },
            };

            if (System.Enum.TryParse<AnimatorLayerBlendingMode>(blendingMode, true, out var bm))
                newLayer.blendingMode = bm;

            // State machine must be added as sub-asset
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, path);
            controller.AddLayer(newLayer);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                layerName,
                layerIndex = controller.layers.Length - 1,
                weight,
                message = $"Added layer '{layerName}' at index {controller.layers.Length - 1}"
            });
        }

        [McpTool("animation_create_blend_tree", "Create a BlendTree in an AnimatorController state",
            Group = "animation")]
        public static ToolResult CreateBlendTree(
            [Desc("Controller asset path")] string path,
            [Desc("State name to attach the blend tree to (will be created if it doesn't exist)")] string stateName,
            [Desc("Blend parameter name")] string parameter,
            [Desc("Blend type: Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D")] string blendType = "Simple1D",
            [Desc("Second parameter name (for 2D blend trees)")] string parameter2 = null,
            [Desc("Motions as array of {clipPath, threshold} (1D) or {clipPath, positionX, positionY} (2D)")] Newtonsoft.Json.Linq.JArray motions = null,
            [Desc("Layer index")] int layer = 0)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return ToolResult.Error($"AnimatorController not found: {path}");

            if (layer >= controller.layers.Length)
                return ToolResult.Error($"Layer {layer} does not exist");

            // Ensure parameter exists
            if (!controller.parameters.Any(p => p.name == parameter))
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            if (!string.IsNullOrEmpty(parameter2) && !controller.parameters.Any(p => p.name == parameter2))
                controller.AddParameter(parameter2, AnimatorControllerParameterType.Float);

            var sm = controller.layers[layer].stateMachine;

            BlendTree blendTree;
            var existingState = sm.states.FirstOrDefault(s => s.state.name == stateName).state;

            if (existingState != null)
            {
                blendTree = new BlendTree { name = stateName };
                existingState.motion = blendTree;
                AssetDatabase.AddObjectToAsset(blendTree, path);
            }
            else
            {
                existingState = controller.CreateBlendTreeInController(stateName, out blendTree, layer);
            }

            blendTree.blendParameter = parameter;
            if (!string.IsNullOrEmpty(parameter2))
                blendTree.blendParameterY = parameter2;

            if (System.Enum.TryParse<BlendTreeType>(blendType, true, out var bt))
                blendTree.blendType = bt;

            // Add motions
            int added = 0;
            if (motions != null)
            {
                foreach (var m in motions)
                {
                    string clipPath = m["clipPath"]?.ToString();
                    if (string.IsNullOrEmpty(clipPath)) continue;

                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null) continue;

                    float threshold = m["threshold"]?.ToObject<float>() ?? added;
                    float posX = m["positionX"]?.ToObject<float>() ?? 0;
                    float posY = m["positionY"]?.ToObject<float>() ?? 0;

                    if (bt == BlendTreeType.Simple1D)
                        blendTree.AddChild(clip, threshold);
                    else
                        blendTree.AddChild(clip, new Vector2(posX, posY));

                    added++;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                stateName,
                parameter,
                blendType,
                motionCount = added,
                message = $"Created BlendTree '{stateName}' with {added} motions"
            });
        }

        [McpTool("animation_set_clip_curve", "Add or modify animation curves on an AnimationClip",
            Group = "animation")]
        public static ToolResult SetClipCurve(
            [Desc("AnimationClip asset path")] string path,
            [Desc("Relative path of the target object (empty for root)")] string relativePath,
            [Desc("Component type (e.g. Transform, SpriteRenderer)")] string componentType,
            [Desc("Property name (e.g. localPosition.x, m_Color.r)")] string propertyName,
            [Desc("Keyframes as [{time, value}] or [{time, value, inTangent, outTangent}]")] Newtonsoft.Json.Linq.JArray keyframes)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                return ToolResult.Error($"AnimationClip not found: {path}");

            if (keyframes == null || keyframes.Count == 0)
                return ToolResult.Error("At least one keyframe is required");

            var type = ComponentTools.ResolveComponentType(componentType);
            if (type == null && componentType != "Transform")
                return ToolResult.Error($"Component type not found: {componentType}");
            if (componentType == "Transform") type = typeof(Transform);

            var keys = keyframes.Select(k => new Keyframe(
                k["time"]?.ToObject<float>() ?? 0f,
                k["value"]?.ToObject<float>() ?? 0f,
                k["inTangent"]?.ToObject<float>() ?? 0f,
                k["outTangent"]?.ToObject<float>() ?? 0f
            )).ToArray();

            var curve = new AnimationCurve(keys);
            clip.SetCurve(relativePath ?? "", type, propertyName, curve);

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                path,
                relativePath,
                componentType,
                propertyName,
                keyframeCount = keys.Length,
                message = $"Set {keys.Length} keyframes on '{propertyName}'"
            });
        }
    }
}
