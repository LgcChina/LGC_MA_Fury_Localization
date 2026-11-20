// LGC_VRCMAFury_中文拓展 v2.3.6 — SDK inspector adapter
// Translate GUI calls in actual SDK editor methods, including inherited generic editors and list callbacks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using ILGenerator = System.Reflection.Emit.ILGenerator;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class ComponentPatches
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.components";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        internal static bool IsApplied { get; private set; }
        internal static int InspectorCount { get; private set; }
        internal static int MethodCount { get; private set; }
        private static readonly HashSet<MethodBase> activeMethods = new HashSet<MethodBase>();
        private static readonly string[] roots = {
            "VRC.SDK3.Dynamics.PhysBone.VRCPhysBoneEditor", "VRC.SDK3.Dynamics.PhysBone.VRCPhysBoneColliderEditor",
            "VRC.SDK3.Dynamics.PhysBone.VRCPhysBoneEditorSharedUtilities", "VRC.SDK3.Dynamics.Contact.VRCContactSenderEditor",
            "VRC.SDK3.Dynamics.Contact.VRCContactReceiverEditor", "VRC.SDK3.Dynamics.Constraint.VRCAimConstraintEditor",
            "VRC.SDK3.Dynamics.Constraint.VRCLookAtConstraintEditor", "VRC.SDK3.Dynamics.Constraint.VRCParentConstraintEditor",
            "VRC.SDK3.Dynamics.Constraint.VRCPositionConstraintEditor", "VRC.SDK3.Dynamics.Constraint.VRCRotationConstraintEditor",
            "VRC.SDK3.Dynamics.Constraint.VRCScaleConstraintEditor", "VRC.SDK3.Dynamics.Constraint.VRCConstraintSourcePropertyDrawer",
            "VRC.SDK3.Dynamics.Constraint.ConstraintFreeEditSlider", "AvatarDescriptorEditor3",
            "VRC.SDKBase.Editor.VRCPreviewOverrideEditor", "VRCPipelineManagerEditor",
            "AnimatorFaceTrackingBlendshapesSetEditor", "VRC.SDK3A.Editor.FaceTrackingDrawerUtility",
            "VRC.SDK3A.Editor.FaceTrackingParameterBehaviorDrawer", "VRC.SDK3A.Editor.FaceTrackingBlendshapesSetDrawer",
            "VRC.SDK3A.Editor.FaceTrackingPickers"
        };
        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = assembly.GetType(name, false); if (t != null) return t; }
                catch { }
            }
            return null;
        }
        internal static bool Apply()
        {
            SetOff();
            var types = new HashSet<Type>();
            foreach (var name in roots)
            {
                var root = FindType(name);
                if (root == null) continue;
                if (typeof(Editor).IsAssignableFrom(root)) InspectorCount++;
                for (var t = root; t != null && t != typeof(Editor) && t != typeof(PropertyDrawer) && t != typeof(object); t = t.BaseType)
                    CollectTypes(t, types);
            }
            foreach (var type in types)
            {
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
                catch { continue; }
                foreach (var method in methods)
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || activeMethods.Contains(method)) continue;
                    try
                    {
                        if (method.GetMethodBody() == null || !DisplayCalls.HasDisplayCall(method)) continue;
                        Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ComponentPatches), nameof(DisplayTranspiler)));
                        activeMethods.Add(method);
                        MethodCount++;
                    }
                    catch (Exception e)
                    { VrcSdkLocalization.Note("SDK 检查器显示入口已跳过：" + type.Name + "." + method.Name + "（" + (e.InnerException ?? e).GetType().Name + "）"); }
                }
            }
            IsApplied = MethodCount > 0;
            var begin = AccessTools.Method(typeof(EditorGUI), "BeginPropertyInternal", new[] { typeof(Rect), typeof(GUIContent), typeof(SerializedProperty) });
            if (begin != null) TryPatch(begin, nameof(PropertyPrefix), null);
            var cache = typeof(EditorGUI).GetNestedType("EnumNamesCache", BindingFlags.NonPublic | BindingFlags.Public)
                ?? typeof(Editor).Assembly.GetType("UnityEditor.EnumNamesCache", false);
            if (cache != null)
                foreach (var m in cache.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    if (m.ReturnType == typeof(GUIContent[]) && (m.Name == "GetEnumLocalizedGUIContents" || m.Name == "GetEnumTypeLocalizedGUIContents"))
                    {
                        var args = m.GetParameters();
                        if (args.Length == 1 && args[0].ParameterType == typeof(SerializedProperty))
                            TryPatch(m, null, nameof(PropertyEnumPostfix));
                        else if (args.Length == 2 && args[0].ParameterType == typeof(Type))
                            TryPatch(m, null, nameof(TypeEnumPostfix));
                    }
            var title = AccessTools.Method(typeof(ObjectNames), "GetInspectorTitle", new[] { typeof(UnityEngine.Object), typeof(bool) });
            if (title != null) TryPatch(title, null, nameof(TitlePostfix));
            ComponentMenus.Apply();
            if (!IsApplied) VrcSdkLocalization.Note("未能接入 SDK 字段绘制入口；组件标题和菜单不代表字段已启用。");
            else VrcSdkLocalization.Note("SDK 原检查器显示入口已接入（" + MethodCount + " 个绘制方法）。");
            return IsApplied;
        }
        private static void CollectTypes(Type type, HashSet<Type> types)
        {
            if (type.ContainsGenericParameters || !types.Add(type)) return;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                var resolved = nested;
                if (nested.ContainsGenericParameters && type.IsConstructedGenericType)
                {
                    try { resolved = nested.MakeGenericType(type.GetGenericArguments()); }
                    catch { continue; }
                }
                CollectTypes(resolved, types);
            }
        }
        private static IEnumerable<CodeInstruction> DisplayTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => DisplayCalls.Rewrite(instructions, generator, false);
        private static void TryPatch(MethodBase method, string prefix, string postfix)
        {
            try { Harmony.Patch(method, prefix: prefix == null ? null : new HarmonyMethod(typeof(ComponentPatches), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(ComponentPatches), postfix)); }
            catch (Exception e) { VrcSdkLocalization.Note("SDK 可选显示入口不兼容：" + method.DeclaringType?.Name + "." + method.Name + "（" + e.GetBaseException().Message + "）"); }
        }
        internal static bool IsSdkTarget(SerializedProperty property)
        {
            if (!VrcSdkLocalization.IsApplied || property == null) return false;
            var target = property.serializedObject.targetObject;
            if (target == null) return false;
            var name = target.GetType().FullName;
            return name != null && (name.StartsWith("VRC.SDK3.Dynamics.", StringComparison.Ordinal) ||
                name == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor" ||
                name == "VRC.SDKBase.VRCPreviewOverride" || name == "VRC.Core.PipelineManager");
        }
        private static void PropertyPrefix(ref GUIContent __1, SerializedProperty __2)
        {
            try
            {
                if (!IsSdkTarget(__2) || __2.propertyPath == "m_Script") return;
                __1 = DisplayCalls.ComponentContent(__1 ?? new GUIContent(__2.displayName, __2.tooltip));
            }
            catch { /* A disposed optional property keeps its original SDK behavior. */ }
        }
        // SDK-bundled Harmony does not support __args. Bind known arguments by index instead.
        private static void PropertyEnumPostfix(SerializedProperty __0, ref GUIContent[] __result)
            => TranslateEnum(__0, null, ref __result);
        private static void TypeEnumPostfix(Type __0, ref GUIContent[] __result)
            => TranslateEnum(null, __0, ref __result);
        private static void TranslateEnum(SerializedProperty property, Type type, ref GUIContent[] __result)
        {
            try
            {
                if (!VrcSdkLocalization.IsApplied || __result == null) return;
                var name = type?.FullName;
                if (!(property != null && IsSdkTarget(property)) &&
                    !(name != null && (name.StartsWith("VRC.Dynamics.", StringComparison.Ordinal) || name.StartsWith("VRC.SDK3.", StringComparison.Ordinal)))) return;
                __result = __result.Select(c => c == null ? null : new GUIContent(c) {
                    text = TranslationCatalog.ComponentEnum(c.text), tooltip = TranslationCatalog.ComponentText(c.tooltip) }).ToArray();
            }
            catch { /* Keep unknown enum interfaces and their cache unchanged. */ }
        }
        private static void TitlePostfix(UnityEngine.Object __0, ref string __result)
        {
            if (!VrcSdkLocalization.IsApplied || __0 == null) return;
            var title = TranslationCatalog.ComponentTitle(__0.GetType().FullName);
            if (title == null) return;
            __result = __result != null && __result.EndsWith(" (Script)", StringComparison.Ordinal) ? title + " (Script)" : title;
        }
        internal static void SetOff()
        {
            IsApplied = false;
            InspectorCount = MethodCount = 0;
            activeMethods.Clear();
            ComponentMenus.SetOff();
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
        }
    }
}
