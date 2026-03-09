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
                    speed = s.state.speed
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
    }
}
