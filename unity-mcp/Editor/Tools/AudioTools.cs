using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Audio")]
    public static class AudioTools
    {
        [McpTool("audio_create_source", "Create a GameObject with an AudioSource component",
            Group = "audio")]
        public static ToolResult CreateSource(
            [Desc("Name of the GameObject")] string name = "AudioSource",
            [Desc("Audio clip asset path (e.g. Assets/Audio/bgm.wav)")] string clipPath = null,
            [Desc("World position")] Vector3? position = null,
            [Desc("Play on awake")] bool playOnAwake = false,
            [Desc("Loop the clip")] bool loop = false,
            [Desc("Volume (0-1)")] float volume = 1f,
            [Desc("Spatial blend (0=2D, 1=3D)")] float spatialBlend = 0f,
            [Desc("Parent GameObject name")] string parent = null)
        {
            var go = new GameObject(name);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = playOnAwake;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = spatialBlend;

            if (!string.IsNullOrEmpty(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                    source.clip = clip;
                else
                    return ToolResult.Error($"AudioClip not found: {clipPath}");
            }

            if (position.HasValue) go.transform.position = position.Value;

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectTools.FindGameObject(parent, null);
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, true);
            }

            UndoHelper.RegisterCreatedObject(go, "Create AudioSource");

            return ToolResult.Json(new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                hasClip = source.clip != null,
                message = $"Created AudioSource: {go.name}"
            });
        }

        [McpTool("audio_modify_source", "Modify AudioSource properties on a GameObject",
            Group = "audio")]
        public static ToolResult ModifySource(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Audio clip asset path")] string clipPath = null,
            [Desc("Volume (0-1)")] float? volume = null,
            [Desc("Pitch")] float? pitch = null,
            [Desc("Loop")] bool? loop = null,
            [Desc("Play on awake")] bool? playOnAwake = null,
            [Desc("Spatial blend (0=2D, 1=3D)")] float? spatialBlend = null,
            [Desc("Min distance (3D)")] float? minDistance = null,
            [Desc("Max distance (3D)")] float? maxDistance = null,
            [Desc("Mute")] bool? mute = null,
            [Desc("Priority (0=highest, 256=lowest)")] int? priority = null,
            [Desc("Doppler level")] float? dopplerLevel = null,
            [Desc("Rolloff mode: Logarithmic, Linear, Custom")] string rolloffMode = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var source = go.GetComponent<AudioSource>();
            if (source == null)
                return ToolResult.Error($"No AudioSource on '{target}'");

            Undo.RecordObject(source, "Modify AudioSource");
            int modified = 0;

            if (!string.IsNullOrEmpty(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) { source.clip = clip; modified++; }
            }
            if (volume.HasValue) { source.volume = volume.Value; modified++; }
            if (pitch.HasValue) { source.pitch = pitch.Value; modified++; }
            if (loop.HasValue) { source.loop = loop.Value; modified++; }
            if (playOnAwake.HasValue) { source.playOnAwake = playOnAwake.Value; modified++; }
            if (spatialBlend.HasValue) { source.spatialBlend = spatialBlend.Value; modified++; }
            if (minDistance.HasValue) { source.minDistance = minDistance.Value; modified++; }
            if (maxDistance.HasValue) { source.maxDistance = maxDistance.Value; modified++; }
            if (mute.HasValue) { source.mute = mute.Value; modified++; }
            if (priority.HasValue) { source.priority = priority.Value; modified++; }
            if (dopplerLevel.HasValue) { source.dopplerLevel = dopplerLevel.Value; modified++; }
            if (!string.IsNullOrEmpty(rolloffMode))
            {
                if (System.Enum.TryParse<AudioRolloffMode>(rolloffMode, true, out var rm))
                { source.rolloffMode = rm; modified++; }
            }

            EditorUtility.SetDirty(source);
            return ToolResult.Text($"Modified {modified} properties on AudioSource '{target}'");
        }

        [McpTool("audio_get_source_info", "Get AudioSource component properties",
            Group = "audio", ReadOnly = true)]
        public static ToolResult GetSourceInfo(
            [Desc("Name or path of the target GameObject")] string target)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var source = go.GetComponent<AudioSource>();
            if (source == null)
                return ToolResult.Error($"No AudioSource on '{target}'");

            return ToolResult.Json(new
            {
                gameObject = go.name,
                clip = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : null,
                clipName = source.clip != null ? source.clip.name : null,
                volume = source.volume,
                pitch = source.pitch,
                loop = source.loop,
                playOnAwake = source.playOnAwake,
                mute = source.mute,
                spatialBlend = source.spatialBlend,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                priority = source.priority,
                dopplerLevel = source.dopplerLevel,
                rolloffMode = source.rolloffMode.ToString(),
            });
        }

        [McpTool("audio_set_clip_import", "Set AudioClip import settings",
            Group = "audio")]
        public static ToolResult SetClipImport(
            [Desc("Audio clip asset path")] string path,
            [Desc("Force to mono")] bool? forceToMono = null,
            [Desc("Load in background")] bool? loadInBackground = null,
            [Desc("Preload audio data")] bool? preloadAudioData = null,
            [Desc("Load type: DecompressOnLoad, CompressedInMemory, Streaming")] string loadType = null,
            [Desc("Compression format: PCM, Vorbis, ADPCM")] string compressionFormat = null,
            [Desc("Quality (0-1, for Vorbis)")] float? quality = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                return ToolResult.Error($"No AudioImporter for: {path}");

            int applied = 0;
            if (forceToMono.HasValue) { importer.forceToMono = forceToMono.Value; applied++; }
            if (loadInBackground.HasValue) { importer.loadInBackground = loadInBackground.Value; applied++; }
            if (preloadAudioData.HasValue) { importer.preloadAudioData = preloadAudioData.Value; applied++; }

            var settings = importer.defaultSampleSettings;
            if (!string.IsNullOrEmpty(loadType))
            {
                if (System.Enum.TryParse<AudioClipLoadType>(loadType, true, out var lt))
                { settings.loadType = lt; applied++; }
            }
            if (!string.IsNullOrEmpty(compressionFormat))
            {
                if (System.Enum.TryParse<AudioCompressionFormat>(compressionFormat, true, out var cf))
                { settings.compressionFormat = cf; applied++; }
            }
            if (quality.HasValue) { settings.quality = quality.Value; applied++; }

            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            return ToolResult.Text($"Applied {applied} import settings to '{path}'");
        }

        [McpTool("audio_create_listener", "Add an AudioListener to a GameObject (removes any existing one in scene first)",
            Group = "audio")]
        public static ToolResult CreateListener(
            [Desc("Target GameObject name (defaults to Main Camera)")] string target = null)
        {
            var go = !string.IsNullOrEmpty(target)
                ? GameObjectTools.FindGameObject(target, null)
                : Camera.main?.gameObject;

            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? "Main Camera"}");

            if (go.GetComponent<AudioListener>() != null)
                return ToolResult.Text($"AudioListener already exists on '{go.name}'");

            Undo.AddComponent<AudioListener>(go);
            return ToolResult.Text($"Added AudioListener to '{go.name}'");
        }
    }
}
