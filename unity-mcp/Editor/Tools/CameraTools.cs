using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Camera")]
    public static class CameraTools
    {
        [McpTool("camera_create", "Create a new camera with specified settings",
            Group = "camera")]
        public static ToolResult Create(
            [Desc("Name for the camera GameObject")] string name = "New Camera",
            [Desc("Projection type: Perspective or Orthographic")] string projection = "Perspective",
            [Desc("Field of view (perspective only)")] float fov = 60f,
            [Desc("Position [x, y, z]")] float[] position = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create Camera '{name}'");
            var cam = go.AddComponent<Camera>();

            cam.orthographic = projection?.ToLower() == "orthographic";
            if (!cam.orthographic)
                cam.fieldOfView = Mathf.Clamp(fov, 1f, 179f);

            if (position != null && position.Length >= 3)
                go.transform.position = new Vector3(position[0], position[1], position[2]);

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                projection = cam.orthographic ? "Orthographic" : "Perspective",
                fov = cam.fieldOfView,
            });
        }

        [McpTool("camera_configure", "Configure camera settings (FOV, clip planes, clear flags, etc.)",
            Group = "camera")]
        public static ToolResult Configure(
            [Desc("Camera name, path, or instanceId")] string target,
            [Desc("Field of view")] float? fov = null,
            [Desc("Near clip plane")] float? nearClip = null,
            [Desc("Far clip plane")] float? farClip = null,
            [Desc("Clear flags: Skybox, SolidColor, Depth, Nothing")] string clearFlags = null,
            [Desc("Background color hex (e.g. #000000)")] string backgroundColor = null,
            [Desc("Orthographic size")] float? orthoSize = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null) return ToolResult.Error($"Camera not found: {target}");
            var cam = go.GetComponent<Camera>();
            if (cam == null) return ToolResult.Error($"'{target}' has no Camera component");

            Undo.RecordObject(cam, "Configure Camera");

            if (fov.HasValue) cam.fieldOfView = Mathf.Clamp(fov.Value, 1f, 179f);
            if (nearClip.HasValue) cam.nearClipPlane = nearClip.Value;
            if (farClip.HasValue) cam.farClipPlane = farClip.Value;
            if (orthoSize.HasValue) cam.orthographicSize = orthoSize.Value;

            if (!string.IsNullOrEmpty(clearFlags))
            {
                cam.clearFlags = clearFlags.ToLower() switch
                {
                    "skybox" => CameraClearFlags.Skybox,
                    "solidcolor" => CameraClearFlags.SolidColor,
                    "depth" => CameraClearFlags.Depth,
                    "nothing" => CameraClearFlags.Nothing,
                    _ => cam.clearFlags,
                };
            }

            if (!string.IsNullOrEmpty(backgroundColor) &&
                ColorUtility.TryParseHtmlString(backgroundColor, out var color))
                cam.backgroundColor = color;

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                fov = cam.fieldOfView,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                clearFlags = cam.clearFlags.ToString(),
                orthographic = cam.orthographic,
            });
        }

        [McpTool("camera_get_info", "Get detailed camera parameters",
            Group = "camera", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Camera name, path, or instanceId (empty = Main Camera)")] string target = null)
        {
            Camera cam;
            if (string.IsNullOrEmpty(target))
            {
                cam = Camera.main;
                if (cam == null)
                    return ToolResult.Error("No Main Camera found in scene");
            }
            else
            {
                var go = GameObjectTools.FindGameObject(target, null);
                if (go == null) return ToolResult.Error($"Camera not found: {target}");
                cam = go.GetComponent<Camera>();
                if (cam == null) return ToolResult.Error($"'{target}' has no Camera component");
            }

            return ToolResult.Json(new
            {
                instanceId = cam.gameObject.GetInstanceID(),
                name = cam.gameObject.name,
                isMainCamera = cam.CompareTag("MainCamera"),
                projection = cam.orthographic ? "Orthographic" : "Perspective",
                fov = cam.fieldOfView,
                orthographicSize = cam.orthographicSize,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                clearFlags = cam.clearFlags.ToString(),
                backgroundColor = $"#{ColorUtility.ToHtmlStringRGBA(cam.backgroundColor)}",
                cullingMask = cam.cullingMask,
                depth = cam.depth,
                rect = new { cam.rect.x, cam.rect.y, cam.rect.width, cam.rect.height },
                position = new { x = cam.transform.position.x, y = cam.transform.position.y, z = cam.transform.position.z },
                rotation = new { x = cam.transform.eulerAngles.x, y = cam.transform.eulerAngles.y, z = cam.transform.eulerAngles.z },
            });
        }

        [McpTool("camera_look_at", "Point a camera at a target position or GameObject",
            Group = "camera")]
        public static ToolResult LookAt(
            [Desc("Camera name, path, or instanceId (empty = Main Camera)")] string camera = null,
            [Desc("Target position [x, y, z]")] float[] position = null,
            [Desc("Target GameObject name/path")] string targetObject = null)
        {
            Camera cam;
            if (string.IsNullOrEmpty(camera))
            {
                cam = Camera.main;
                if (cam == null) return ToolResult.Error("No Main Camera found");
            }
            else
            {
                var go = GameObjectTools.FindGameObject(camera, null);
                if (go == null) return ToolResult.Error($"Camera not found: {camera}");
                cam = go.GetComponent<Camera>();
                if (cam == null) return ToolResult.Error($"'{camera}' has no Camera component");
            }

            Vector3 targetPos;
            if (!string.IsNullOrEmpty(targetObject))
            {
                var targetGo = GameObjectTools.FindGameObject(targetObject, null);
                if (targetGo == null) return ToolResult.Error($"Target not found: {targetObject}");
                targetPos = targetGo.transform.position;
            }
            else if (position != null && position.Length >= 3)
            {
                targetPos = new Vector3(position[0], position[1], position[2]);
            }
            else
            {
                return ToolResult.Error("Provide either 'position' or 'targetObject'");
            }

            Undo.RecordObject(cam.transform, "Camera Look At");
            cam.transform.LookAt(targetPos);

            return ToolResult.Json(new
            {
                camera = cam.gameObject.name,
                lookingAt = new { x = targetPos.x, y = targetPos.y, z = targetPos.z },
                rotation = new { x = cam.transform.eulerAngles.x, y = cam.transform.eulerAngles.y, z = cam.transform.eulerAngles.z },
            });
        }
    }
}
