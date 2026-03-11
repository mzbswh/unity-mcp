using System.Collections.Generic;
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
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) { source.clip = clip; changes.Add($"clip={clipPath}"); }
            }
            if (volume.HasValue) { source.volume = volume.Value; changes.Add($"volume={volume.Value}"); }
            if (pitch.HasValue) { source.pitch = pitch.Value; changes.Add($"pitch={pitch.Value}"); }
            if (loop.HasValue) { source.loop = loop.Value; changes.Add($"loop={loop.Value}"); }
            if (playOnAwake.HasValue) { source.playOnAwake = playOnAwake.Value; changes.Add($"playOnAwake={playOnAwake.Value}"); }
            if (spatialBlend.HasValue) { source.spatialBlend = spatialBlend.Value; changes.Add($"spatialBlend={spatialBlend.Value}"); }
            if (minDistance.HasValue) { source.minDistance = minDistance.Value; changes.Add($"minDistance={minDistance.Value}"); }
            if (maxDistance.HasValue) { source.maxDistance = maxDistance.Value; changes.Add($"maxDistance={maxDistance.Value}"); }
            if (mute.HasValue) { source.mute = mute.Value; changes.Add($"mute={mute.Value}"); }
            if (priority.HasValue) { source.priority = priority.Value; changes.Add($"priority={priority.Value}"); }
            if (dopplerLevel.HasValue) { source.dopplerLevel = dopplerLevel.Value; changes.Add($"dopplerLevel={dopplerLevel.Value}"); }
            if (!string.IsNullOrEmpty(rolloffMode))
            {
                if (System.Enum.TryParse<AudioRolloffMode>(rolloffMode, true, out var rm))
                { source.rolloffMode = rm; changes.Add($"rolloffMode={rm}"); }
            }

            EditorUtility.SetDirty(source);
            if (changes.Count == 0) return ToolResult.Text($"No properties changed on AudioSource '{target}'");
            return ToolResult.Text($"AudioSource '{target}' updated: {string.Join(", ", changes)}");
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

            var changes = new List<string>();
            if (forceToMono.HasValue) { importer.forceToMono = forceToMono.Value; changes.Add($"forceToMono={forceToMono.Value}"); }
            if (loadInBackground.HasValue) { importer.loadInBackground = loadInBackground.Value; changes.Add($"loadInBackground={loadInBackground.Value}"); }
            if (preloadAudioData.HasValue) { importer.preloadAudioData = preloadAudioData.Value; changes.Add($"preloadAudioData={preloadAudioData.Value}"); }

            var settings = importer.defaultSampleSettings;
            if (!string.IsNullOrEmpty(loadType))
            {
                if (System.Enum.TryParse<AudioClipLoadType>(loadType, true, out var lt))
                { settings.loadType = lt; changes.Add($"loadType={lt}"); }
            }
            if (!string.IsNullOrEmpty(compressionFormat))
            {
                if (System.Enum.TryParse<AudioCompressionFormat>(compressionFormat, true, out var cf))
                { settings.compressionFormat = cf; changes.Add($"compressionFormat={cf}"); }
            }
            if (quality.HasValue) { settings.quality = quality.Value; changes.Add($"quality={quality.Value}"); }

            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            if (changes.Count == 0) return ToolResult.Text($"No import settings changed for '{path}'");
            return ToolResult.Text($"Audio import '{path}' updated: {string.Join(", ", changes)}");
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
