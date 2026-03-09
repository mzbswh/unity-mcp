#if UNITY_MCP_RUNTIME
using System;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Screenshot")]
    public static class RuntimeScreenshotTools
    {
        [McpTool("screenshot_game", "Capture current game view as Base64 PNG",
            ReadOnly = true, Group = "runtime")]
        public static ToolResult CaptureGameView(
            [Desc("Width in pixels (default: Screen.width)")] int width = 0,
            [Desc("Height in pixels (default: Screen.height)")] int height = 0)
        {
            width = width > 0 ? width : Screen.width;
            height = height > 0 ? height : Screen.height;

            var camera = Camera.main;
            if (camera == null)
                return ToolResult.Error("No main camera found");

            return CaptureFromCamera(camera, width, height, "MainCamera");
        }

        [McpTool("screenshot_camera", "Capture specific camera view as Base64 PNG",
            ReadOnly = true, Group = "runtime")]
        public static ToolResult CaptureCamera(
            [Desc("Camera name (default: Main Camera)")] string cameraName = "Main Camera",
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080)
        {
            var camera = GameObject.Find(cameraName)?.GetComponent<Camera>();
            if (camera == null)
                return ToolResult.Error($"Camera '{cameraName}' not found");

            return CaptureFromCamera(camera, width, height, cameraName);
        }

        private static ToolResult CaptureFromCamera(Camera camera, int width, int height, string sourceName)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prevRT = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = prevRT;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            return ToolResult.Json(new
            {
                format = "png",
                source = sourceName,
                width,
                height,
                base64 = Convert.ToBase64String(bytes),
                sizeBytes = bytes.Length
            });
        }
    }
}
#endif
