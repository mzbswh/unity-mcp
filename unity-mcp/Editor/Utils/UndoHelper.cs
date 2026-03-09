using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor.Utils
{
    /// <summary>
    /// Wraps Unity's Undo system for MCP tool operations.
    /// All destructive editor operations should go through this.
    /// </summary>
    public static class UndoHelper
    {
        public static int BeginGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }

        public static void EndGroup() { }

        public static void RegisterCreatedObject(Object obj, string name)
        {
            Undo.RegisterCreatedObjectUndo(obj, name);
        }

        public static void RecordObject(Object obj, string name)
        {
            Undo.RecordObject(obj, name);
        }

        public static void DestroyObject(Object obj)
        {
            Undo.DestroyObjectImmediate(obj);
        }

        public static void SetTransformParent(Transform child, Transform parent, bool worldPositionStays, string name)
        {
            Undo.SetTransformParent(child, parent, worldPositionStays, name);
        }
    }
}
