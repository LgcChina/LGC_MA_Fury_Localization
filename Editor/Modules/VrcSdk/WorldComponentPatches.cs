// LGC_VRCMAFury_中文拓展 v2.3.7 — World SDK UI Toolkit inspectors
// Translate SDK-owned captions after binding; preserve property paths, input values and object names.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class WorldComponentPatches
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.world-components";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal)
        {
            "VRCSceneDescriptor", "VRCAvatarPedestal", "VRCBillboard", "VRCMirrorReflection",
            "VRCObjectPool", "VRCObjectSync", "VRCPickup", "VRCPlacementZone", "VRCPortalMarker",
            "VRCSpatialAudioSource", "VRCStation", "VRCVelocityTracker", "VRCCameraDollyAnimation",
            "VRCCameraDollyPath", "VRCCameraDollyPathPoint", "VRCMidiPlayer", "VRCMidiListener"
        };
        private static readonly List<Root> roots = new List<Root>();
        private static readonly List<Edit> edits = new List<Edit>();
        private static ConditionalWeakTable<VisualElement, Dictionary<string, Edit>> byElement =
            new ConditionalWeakTable<VisualElement, Dictionary<string, Edit>>();
        private static double nextUpdate;
        internal static bool IsWorldSdk { get; private set; }
        private static bool applied;

        internal static bool IsWorldTarget(UnityEngine.Object target)
        {
            if (target == null) return false;
            var type = target.GetType();
            return type.Namespace == "VRC.SDK3.Components" && targets.Contains(type.Name);
        }

        internal static GUIContent PropertyContent(SerializedProperty property, GUIContent original)
        {
            var source = original ?? new GUIContent(property.displayName, property.tooltip);
            var text = source.text;
            if (property.propertyType == SerializedPropertyType.ArraySize)
                return new GUIContent(source) { text = TranslationCatalog.ComponentText(property.displayName) };
            // A call-site wrapper may have already applied the shared component vocabulary.
            if (text == TranslationCatalog.ComponentText(property.displayName)) text = property.displayName;
            return new GUIContent(source) { text = TranslationCatalog.WorldComponentText(text),
                tooltip = TranslationCatalog.WorldComponentText(source.tooltip) };
        }

        internal static bool Apply()
        {
            SetOff();
            Type inspectorBase = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    IsWorldSdk |= assembly.GetType("VRC.SDK3.Components.VRCSceneDescriptor", false) != null;
                    if (inspectorBase == null) inspectorBase = assembly.GetType("VRC.SDK3.Editor.VRCInspectorBase", false);
                }
                catch { /* Missing optional package or unreadable metadata. */ }
            }
            if (!IsWorldSdk) return false;
            if (inspectorBase == null)
            {
                VrcSdkLocalization.Note("世界 SDK 未提供 UI Toolkit 检查器基类，保留已有 IMGUI 字段适配。");
                return false;
            }
            var method = AccessTools.DeclaredMethod(inspectorBase, "CreateInspectorGUI", Type.EmptyTypes);
            if (method == null || !typeof(VisualElement).IsAssignableFrom(method.ReturnType)) return false;
            try
            {
                Harmony.Patch(method, postfix: new HarmonyMethod(typeof(WorldComponentPatches), nameof(InspectorPostfix)));
                applied = true;
                EditorApplication.update += Update;
                VrcSdkLocalization.Note("世界 SDK 检查器已接入：绑定后的字段标题、说明和提示；重新选中组件即可查看。");
                return true;
            }
            catch (Exception e)
            {
                VrcSdkLocalization.Note("世界 SDK 可选检查器入口已跳过：" + e.GetBaseException().Message);
                return false;
            }
        }

        private static void InspectorPostfix(Editor __instance, VisualElement __result)
        {
            if (!applied || !VrcSdkLocalization.IsApplied || __result == null || __instance == null || !IsWorldTarget(__instance.target)) return;
            roots.Add(new Root { Editor = new WeakReference<Editor>(__instance), Element = new WeakReference<VisualElement>(__result) });
            Walk(__result);
        }

        private static void Update()
        {
            if (!applied || !VrcSdkLocalization.IsApplied || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.4;
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                var root = roots[i];
                if (!root.Editor.TryGetTarget(out var editor) || editor == null || !root.Element.TryGetTarget(out var element))
                { roots.RemoveAt(i); continue; }
                if (element.panel != null) Walk(element);
            }
            edits.RemoveAll(edit => !edit.Element.TryGetTarget(out _));
        }

        private static void Walk(VisualElement element)
        {
            try
            {
                var tip = TranslationCatalog.WorldComponentText(element.tooltip);
                if (tip != element.tooltip) Set(element, true, element.tooltip, tip);
                // Read-only metadata labels only: never descend into editable values or object selectors.
                if (!(element is Foldout) && IsField(element.GetType()))
                {
                    var label = AccessTools.Property(element.GetType(), "labelElement")?.GetValue(element) as Label;
                    if (label != null) Caption(label);
                    if (element is Toggle)
                    {
                        var toggleText = element.Q<Label>(className: "unity-toggle__text");
                        if (toggleText != null) Caption(toggleText);
                    }
                    // EnumField uses a display-only TextElement. Its enum value and menu IDs are untouched.
                    if (element is EnumField)
                    {
                        var valueLabel = element.Q<TextElement>(className: "unity-enum-field__text");
                        if (valueLabel != null) Caption(valueLabel, true);
                    }
                    return;
                }
                if (element is TextElement text) Caption(text);
                for (var i = 0; i < element.hierarchy.childCount; i++) Walk(element.hierarchy[i]);
            }
            catch { /* A disposed optional visual keeps the SDK's original behavior. */ }
        }

        private static bool IsField(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BaseField<>)) return true;
            return false;
        }

        private static void Caption(TextElement element, bool isEnum = false)
        {
            var original = element.text;
            var translated = isEnum ? TranslationCatalog.ComponentEnum(original) : original == "Size" ? TranslationCatalog.ComponentText(original) : TranslationCatalog.WorldComponentText(original);
            if (translated != original) Set(element, false, original, translated);
        }

        private static void Set(VisualElement element, bool tooltip, string original, string translated)
        {
            var key = tooltip ? "tooltip" : "text";
            var records = byElement.GetOrCreateValue(element);
            if (!records.TryGetValue(key, out var edit))
            {
                edit = new Edit { Element = new WeakReference<VisualElement>(element), Tooltip = tooltip };
                records[key] = edit;
                edits.Add(edit);
            }
            edit.Original = original;
            edit.Translated = translated;
            if (tooltip) element.tooltip = translated;
            else ((TextElement)element).text = translated;
        }

        internal static void SetOff()
        {
            applied = false;
            IsWorldSdk = false;
            EditorApplication.update -= Update;
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            for (var i = edits.Count - 1; i >= 0; i--)
            {
                var edit = edits[i];
                try
                {
                    if (!edit.Element.TryGetTarget(out var element)) continue;
                    if (edit.Tooltip) { if (element.tooltip == edit.Translated) element.tooltip = edit.Original; }
                    else if (element is TextElement text && text.text == edit.Translated) text.text = edit.Original;
                }
                catch { }
            }
            edits.Clear(); roots.Clear();
            byElement = new ConditionalWeakTable<VisualElement, Dictionary<string, Edit>>();
            nextUpdate = 0;
        }
        private sealed class Root
        {
            internal WeakReference<Editor> Editor;
            internal WeakReference<VisualElement> Element;
        }
        private sealed class Edit
        {
            internal WeakReference<VisualElement> Element;
            internal bool Tooltip;
            internal string Original, Translated;
        }
    }
}
