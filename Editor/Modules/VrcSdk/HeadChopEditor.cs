// LGC_VRCMAFury_中文拓展 v2.3.7 — VRChat SDK VRCHeadChop adapter
// Draw SDK serialized fields using Unity controls; translate only labels, tooltips and enum captions.
using System;
using UnityEditor;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    [CanEditMultipleObjects]
    internal sealed class HeadChopEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!VrcSdkLocalization.HeadChopApplied || !VrcSdkLocalization.IsHeadChop(target))
            {
                DrawDefaultInspector();
                return;
            }
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            if (property.NextVisible(true))
            {
                do
                {
                    using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                        if (DrawProperty(property)) break;
                }
                while (property.NextVisible(false));
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static bool DrawProperty(SerializedProperty property)
        {
            var label = Label(property);
            if (property.propertyType == SerializedPropertyType.Enum &&
                property.name == "applyCondition" && property.propertyPath.StartsWith("targetBones.Array.data[", StringComparison.Ordinal))
            {
                DrawCondition(property, label);
                return false;
            }
            if (property.propertyType == SerializedPropertyType.Generic &&
                (property.propertyPath == "targetBones" ||
                 property.propertyPath.StartsWith("targetBones.Array.data[", StringComparison.Ordinal)))
            {
                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(rect, label, property);
                property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
                EditorGUI.EndProperty();
                if (!property.isExpanded) return false;
                EditorGUI.indentLevel++;
                try
                {
                    var child = property.Copy();
                    var end = property.GetEndProperty();
                    if (child.NextVisible(true))
                    {
                        do
                        {
                            if (SerializedProperty.EqualContents(child, end) || child.depth <= property.depth) break;
                            if (DrawProperty(child)) return true;
                        }
                        while (child.NextVisible(false));
                    }
                }
                finally { EditorGUI.indentLevel--; }
                return false;
            }
            // Range, object references, array size, prefab overrides and Undo use Unity's property controls.
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, label, true);
            var changed = EditorGUI.EndChangeCheck();
            // Resizing an array invalidates child iterators; resume drawing on the next GUI event.
            return changed && property.propertyType == SerializedPropertyType.ArraySize;
        }

        private static GUIContent Label(SerializedProperty property)
        {
            string key = null;
            switch (property.propertyPath)
            {
                case "targetBones": key = "Target Bones"; break;
                case "globalScaleFactor": key = "Global Scale Factor"; break;
            }
            if (key == null && property.propertyPath.StartsWith("targetBones.Array.data[", StringComparison.Ordinal))
            {
                switch (property.name)
                {
                    case "transform": key = "Transform"; break;
                    case "scaleFactor": key = "Scale Factor"; break;
                    case "applyCondition": key = "Apply Condition"; break;
                }
            }
            if (property.propertyPath == "targetBones.Array.size") key = "Size";
            return new GUIContent(TranslationCatalog.Text(key ?? property.displayName),
                TranslationCatalog.Text(property.tooltip));
        }

        private static void DrawCondition(SerializedProperty property, GUIContent label)
        {
            var originalNames = property.enumDisplayNames;
            var names = property.enumNames;
            var captions = new GUIContent[originalNames.Length];
            for (var i = 0; i < captions.Length; i++)
            {
                string key = null;
                if (i < names.Length)
                {
                    switch (names[i])
                    {
                        case "AlwaysApply": key = "Always Apply"; break;
                        case "VrOnly": key = "VR Only"; break;
                        case "NonVrOnly": key = "Non VR Only"; break;
                    }
                }
                captions[i] = new GUIContent(TranslationCatalog.Text(key ?? originalNames[i]));
            }
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, label, property);
            var previousMixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var selected = EditorGUI.Popup(rect, label, property.enumValueIndex, captions);
                if (EditorGUI.EndChangeCheck() && selected >= 0) property.enumValueIndex = selected;
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
                EditorGUI.EndProperty();
            }
        }
    }
}
