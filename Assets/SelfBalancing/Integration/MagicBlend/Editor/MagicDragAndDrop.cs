// Designed by KINEMATION, 2025.

using KINEMATION.MagicBlend.Runtime;
using KINEMATION.Shared.KAnimationCore.Editor.Widgets;
using UnityEditor;
using UnityEngine;

namespace KINEMATION.MagicBlend.Editor
{
    public class MagicDragAndDrop : AssetDragAndDrop<MagicBlending, MagicBlendAsset>
    {
        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
#if UNITY_6000_3_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnInspectorDrop);
            DragAndDrop.AddDropHandlerV2(OnHierarchyDrop);
#else
            DragAndDrop.AddDropHandler(OnInspectorDrop);
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
#endif
        }
    }
}