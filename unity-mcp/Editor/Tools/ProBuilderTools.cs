// ProBuilder tools - only compiled when the ProBuilder package is installed.
// To use, add "com.unity.probuilder" to your manifest.json and add
// PROBUILDER_ENABLED to Scripting Define Symbols in Player Settings.

#if PROBUILDER_ENABLED
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Editor.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("ProBuilder")]
    public static class ProBuilderTools
    {
        [McpTool("probuilder_create_shape", "Create a ProBuilder primitive shape",
            Group = "probuilder")]
        public static ToolResult CreateShape(
            [Desc("Shape type: Cube, Cylinder, Sphere, Plane, Prism, Stair, Arch, Pipe, Cone, Torus, Door")] string shape,
            [Desc("Name for the GameObject")] string name = null,
            [Desc("World position")] Vector3? position = null,
            [Desc("Scale")] Vector3? scale = null)
        {
            if (!Enum.TryParse<ShapeType>(shape, true, out var shapeType))
                return ToolResult.Error($"Unknown shape: {shape}. Available: {string.Join(", ", Enum.GetNames(typeof(ShapeType)))}");

            var mesh = ShapeGenerator.CreateShape(shapeType);
            mesh.gameObject.name = name ?? $"ProBuilder {shape}";

            if (position.HasValue)
                mesh.transform.position = position.Value;
            if (scale.HasValue)
                mesh.transform.localScale = scale.Value;

            mesh.ToMesh();
            mesh.Refresh();

            UndoHelper.RegisterCreatedObject(mesh.gameObject, $"Create ProBuilder {shape}");

            return ToolResult.Json(new
            {
                name = mesh.gameObject.name,
                shape,
                vertexCount = mesh.vertexCount,
                faceCount = mesh.faceCount,
                instanceId = mesh.gameObject.GetInstanceID(),
            });
        }

        [McpTool("probuilder_get_mesh_info", "Get detailed info about a ProBuilder mesh",
            Group = "probuilder", ReadOnly = true)]
        public static ToolResult GetMeshInfo(
            [Desc("GameObject name or path")] string gameObject)
        {
            var go = GameObjectTools.FindGameObject(gameObject, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: '{gameObject}'");

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult.Error($"No ProBuilderMesh on '{gameObject}'");

            return ToolResult.Json(new
            {
                name = go.name,
                vertexCount = pb.vertexCount,
                faceCount = pb.faceCount,
                edgeCount = pb.edgeCount,
                triangleCount = pb.triangleCount,
                meshBounds = new
                {
                    center = pb.mesh.bounds.center.ToString(),
                    size = pb.mesh.bounds.size.ToString(),
                },
            });
        }

        [McpTool("probuilder_extrude_faces", "Extrude selected faces of a ProBuilder mesh",
            Group = "probuilder")]
        public static ToolResult ExtrudeFaces(
            [Desc("GameObject name or path")] string gameObject,
            [Desc("Face indices to extrude (e.g. [0, 1, 2])")] int[] faceIndices,
            [Desc("Extrusion distance")] float distance = 0.5f)
        {
            var go = GameObjectTools.FindGameObject(gameObject, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: '{gameObject}'");

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult.Error($"No ProBuilderMesh on '{gameObject}'");

            if (faceIndices == null || faceIndices.Length == 0)
                return ToolResult.Error("faceIndices is required");

            var faces = pb.faces;
            var selectedFaces = faceIndices
                .Where(i => i >= 0 && i < faces.Count)
                .Select(i => faces[i])
                .ToArray();

            if (selectedFaces.Length == 0)
                return ToolResult.Error("No valid face indices provided");

            Undo.RecordObject(pb, "Extrude Faces");
            pb.Extrude(selectedFaces, ExtrudeMethod.FaceNormal, distance);
            pb.ToMesh();
            pb.Refresh();

            return ToolResult.Json(new
            {
                gameObject = go.name,
                extrudedFaces = selectedFaces.Length,
                distance,
                newVertexCount = pb.vertexCount,
                newFaceCount = pb.faceCount,
            });
        }

        [McpTool("probuilder_set_material", "Set material on specific faces of a ProBuilder mesh",
            Group = "probuilder")]
        public static ToolResult SetFaceMaterial(
            [Desc("GameObject name or path")] string gameObject,
            [Desc("Material asset path")] string materialPath,
            [Desc("Face indices (null = all faces)")] int[] faceIndices = null)
        {
            var go = GameObjectTools.FindGameObject(gameObject, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: '{gameObject}'");

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult.Error($"No ProBuilderMesh on '{gameObject}'");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
                return ToolResult.Error($"Material not found: {materialPath}");

            Undo.RecordObject(pb, "Set ProBuilder Material");

            var faces = pb.faces;
            var targetFaces = faceIndices != null
                ? faceIndices.Where(i => i >= 0 && i < faces.Count).Select(i => faces[i]).ToArray()
                : faces.ToArray();

            foreach (var face in targetFaces)
                face.submeshIndex = pb.GetComponent<Renderer>().sharedMaterials.Length;

            var renderer = pb.GetComponent<Renderer>();
            var mats = renderer.sharedMaterials.ToList();
            mats.Add(mat);
            renderer.sharedMaterials = mats.ToArray();

            pb.ToMesh();
            pb.Refresh();

            return ToolResult.Json(new
            {
                gameObject = go.name,
                material = mat.name,
                affectedFaces = targetFaces.Length,
            });
        }
    }
}
#endif
