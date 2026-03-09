using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Screenshot")]
    public static class ScreenshotTools
    {
        [McpTool("screenshot_scene", "Capture a screenshot from the Scene View",
            Group = "screenshot", ReadOnly = true)]
        public static ToolResult CaptureSceneView(
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080,
            [Desc("Save path (optional, returns base64 if not provided)")] string savePath = null)
        {
            if (!string.IsNullOrEmpty(savePath))
            {
                var pv = PathValidator.QuickValidate(savePath);
                if (!pv.IsValid) return ToolResult.Error(pv.Error);
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View found");

            var camera = sceneView.camera;
            if (camera == null)
                return ToolResult.Error("Scene View camera not available");

            return CaptureFromCamera(camera, width, height, savePath, "SceneView");
        }

        [McpTool("screenshot_game", "Capture a screenshot from the Game View (main camera)",
            Group = "screenshot", ReadOnly = true)]
        public static ToolResult CaptureGameView(
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080,
            [Desc("Camera name (default: Main Camera)")] string cameraName = null,
            [Desc("Save path (optional, returns base64 if not provided)")] string savePath = null)
        {
            if (!string.IsNullOrEmpty(savePath))
            {
                var pv = PathValidator.QuickValidate(savePath);
                if (!pv.IsValid) return ToolResult.Error(pv.Error);
            }

            Camera camera;
            if (string.IsNullOrEmpty(cameraName))
            {
                camera = Camera.main;
                if (camera == null)
                    return ToolResult.Error("No Main Camera found in scene");
            }
            else
            {
                var go = GameObject.Find(cameraName);
                if (go == null)
                    return ToolResult.Error($"GameObject '{cameraName}' not found");
                camera = go.GetComponent<Camera>();
                if (camera == null)
                    return ToolResult.Error($"No Camera component on '{cameraName}'");
            }

            return CaptureFromCamera(camera, width, height, savePath, cameraName ?? "MainCamera");
        }

        private static ToolResult CaptureFromCamera(Camera camera, int width, int height, string savePath, string sourceName)
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
            UnityEngine.Object.DestroyImmediate(tex);

            if (!string.IsNullOrEmpty(savePath))
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(savePath, bytes);

                return ToolResult.Json(new
                {
                    success = true,
                    source = sourceName,
                    width,
                    height,
                    savedTo = savePath,
                    sizeBytes = bytes.Length
                });
            }

            return ToolResult.Json(new
            {
                success = true,
                source = sourceName,
                format = "png",
                width,
                height,
                base64 = Convert.ToBase64String(bytes),
                sizeBytes = bytes.Length
            });
        }
    }
}
