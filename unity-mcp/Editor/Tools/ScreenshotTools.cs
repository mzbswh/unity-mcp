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
        [McpTool("screenshot_scene", "Capture a screenshot from the Scene View. Returns base64 PNG image for AI vision.",
            Group = "screenshot", ReadOnly = true)]
        public static ToolResult CaptureSceneView(
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080,
            [Desc("Save path (optional, returns base64 if not provided)")] string savePath = null,
            [Desc("Max resolution for the returned image (longest edge). 0 = no downscaling. Recommended: 640-1024 for AI vision.")] int maxResolution = 0)
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

            return CaptureFromCamera(camera, width, height, savePath, "SceneView", maxResolution);
        }

        [McpTool("screenshot_game", "Capture a screenshot from the Game View (main camera). Returns base64 PNG image for AI vision.",
            Group = "screenshot", ReadOnly = true)]
        public static ToolResult CaptureGameView(
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080,
            [Desc("Camera name (default: Main Camera)")] string cameraName = null,
            [Desc("Save path (optional, returns base64 if not provided)")] string savePath = null,
            [Desc("Max resolution for the returned image (longest edge). 0 = no downscaling. Recommended: 640-1024 for AI vision.")] int maxResolution = 0)
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
                var go = GameObjectTools.FindGameObject(cameraName, null);
                if (go == null)
                    return ToolResult.Error($"GameObject '{cameraName}' not found");
                camera = go.GetComponent<Camera>();
                if (camera == null)
                    return ToolResult.Error($"No Camera component on '{cameraName}'");
            }

            return CaptureFromCamera(camera, width, height, savePath, cameraName ?? "MainCamera", maxResolution);
        }

        private static ToolResult CaptureFromCamera(Camera camera, int width, int height, string savePath, string sourceName, int maxResolution = 0)
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

            var fullBytes = tex.EncodeToPNG();

            // Save full-resolution file if requested
            if (!string.IsNullOrEmpty(savePath))
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(savePath, fullBytes);
            }

            // Downscale for AI vision if maxResolution is set
            byte[] returnBytes = fullBytes;
            int returnW = width, returnH = height;
            if (maxResolution > 0 && (width > maxResolution || height > maxResolution))
            {
                float scale = Mathf.Min((float)maxResolution / width, (float)maxResolution / height);
                int dstW = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                int dstH = Mathf.Max(1, Mathf.RoundToInt(height * scale));

                var downRT = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32);
                downRT.filterMode = FilterMode.Bilinear;
                var prevActive = RenderTexture.active;
                Graphics.Blit(tex, downRT);
                RenderTexture.active = downRT;
                var downTex = new Texture2D(dstW, dstH, TextureFormat.RGB24, false);
                downTex.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
                downTex.Apply();
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(downRT);

                returnBytes = downTex.EncodeToPNG();
                returnW = dstW;
                returnH = dstH;
                UnityEngine.Object.DestroyImmediate(downTex);
            }

            UnityEngine.Object.DestroyImmediate(tex);

            var base64 = Convert.ToBase64String(returnBytes);
            string desc = !string.IsNullOrEmpty(savePath)
                ? $"Screenshot from {sourceName} ({returnW}x{returnH}), saved to {savePath}"
                : $"Screenshot from {sourceName} ({returnW}x{returnH})";

            return ToolResult.Image(base64, "image/png", desc);
        }
    }
}
