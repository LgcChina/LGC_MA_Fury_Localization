// LGC_MA_Fury_Localization v2.0.0 — VRCFury adapter
// Translate menu captions only. Component paths, callbacks, selection data and menu priorities stay original.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.Vrcfury
{
    internal static class MenuPatches
    {
        private const string HarmonyId = VrcfuryLocalization.PackageName + ".vrcfury.menus";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly HashSet<MethodBase> methods = new HashSet<MethodBase>();
        private static Type componentItem;
        private static PropertyInfo menuPath, content, scriptingPath;
        private static FieldInfo localizedName, searchableName;

        internal static void SetApplied(bool enabled)
        {
            if (!enabled)
            {
                try { Harmony.UnpatchAll(HarmonyId); } catch { }
                methods.Clear();
                return;
            }
            var assembly = typeof(Editor).Assembly;
            componentItem = assembly.GetType("UnityEditor.AddComponent.ComponentDropdownItem", false);
            var source = assembly.GetType("UnityEditor.AddComponent.AddComponentDataSource", false);
            if (componentItem != null && source != null)
            {
                menuPath = AccessTools.Property(componentItem, "menuPath");
                content = AccessTools.Property(typeof(AdvancedDropdownItem), "content");
                localizedName = AccessTools.Field(componentItem, "m_LocalizedName");
                searchableName = AccessTools.Field(componentItem, "m_SearchableNameLocalized");
                if (menuPath != null && localizedName != null)
                {
                    Patch(AccessTools.Method(source, "RebuildTree"), nameof(ComponentTreePostfix), false);
                    Patch(AccessTools.Method(source, "Search"), nameof(ComponentTreePostfix), false);
                }
            }
            var item = assembly.GetType("UnityEditor.ScriptingMenuItem", false);
            scriptingPath = item == null ? null : AccessTools.Property(item, "path");
            var utility = assembly.GetType("UnityEditor.MenuUtils", false);
            var extract = utility == null ? null : AccessTools.Method(utility, "ExtractOnlyEnabledMenuItem");
            var parameters = extract?.GetParameters();
            if (scriptingPath != null && parameters != null && parameters.Length >= 3 &&
                parameters[0].ParameterType == item && parameters[2].ParameterType == typeof(string))
                Patch(extract, nameof(ContextPrefix), true);
            Patch(AccessTools.Method(typeof(ObjectNames), "GetInspectorTitle", new[] { typeof(UnityEngine.Object), typeof(bool) }),
                nameof(InspectorTitlePostfix), false);
            foreach (var method in typeof(GenericMenu).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                var args = method.GetParameters();
                if ((method.Name == "AddItem" || method.Name == "AddDisabledItem") && args.Length > 0 &&
                    args[0].ParameterType == typeof(GUIContent)) Patch(method, nameof(GenericMenuPrefix), true);
            }
        }
        private static void Patch(MethodBase method, string callback, bool prefix)
        {
            if (method == null || methods.Contains(method)) return;
            try
            {
                var hook = new HarmonyMethod(typeof(MenuPatches), callback);
                Harmony.Patch(method, prefix: prefix ? hook : null, postfix: prefix ? null : hook);
                methods.Add(method);
            }
            catch { VrcfuryLocalization.Note("部分菜单显示接口不兼容，已保留该部分原版菜单。"); }
        }
        private static string ComponentCaption(string path)
        {
            if (path == null) return null;
            if (path.StartsWith("Component/", StringComparison.Ordinal)) path = path.Substring(10);
            if (!path.StartsWith("VRCFury/", StringComparison.Ordinal)) return null;
            var leaf = path.Substring(path.LastIndexOf('/') + 1);
            const string suffix = " (VRCFury)";
            var title = leaf.EndsWith(suffix, StringComparison.Ordinal) ? leaf.Substring(0, leaf.Length - suffix.Length) : leaf;
            var translated = TranslationCatalog.Title(title);
            return translated == title ? null : translated + (leaf.EndsWith(suffix, StringComparison.Ordinal) ? suffix : "");
        }
        private static void ComponentTreePostfix(AdvancedDropdownItem __result)
        {
            if (!VrcfuryLocalization.IsApplied || __result == null) return;
            try { TranslateTree(__result); } catch { }
        }
        private static void TranslateTree(AdvancedDropdownItem item)
        {
            if (componentItem.IsInstanceOfType(item))
            {
                var translated = ComponentCaption(menuPath.GetValue(item) as string);
                if (translated != null)
                {
                    localizedName.SetValue(item, translated);
                    searchableName?.SetValue(item, translated.ToLowerInvariant());
                    if (content?.GetValue(item) is GUIContent label) label.text = translated;
                }
            }
            foreach (var child in item.children) TranslateTree(child);
        }
        private static void ContextPrefix(object __0, ref string __2)
        {
            if (!VrcfuryLocalization.IsApplied || __2 == null) return;
            try
            {
                var path = scriptingPath.GetValue(__0) as string;
                if (path == null || path.IndexOf("/VRCFury/", StringComparison.Ordinal) < 0) return;
                var leaf = path.Substring(path.LastIndexOf('/') + 1);
                var translated = ComponentCaption(path) ?? TranslationCatalog.Title(leaf);
                if (leaf == translated) return;
                if (__2 == leaf || __2.EndsWith("/" + leaf, StringComparison.Ordinal))
                    __2 = __2.Substring(0, __2.Length - leaf.Length) + translated;
            }
            catch { }
        }
        private static void InspectorTitlePostfix(UnityEngine.Object __0, ref string __result)
        {
            if (__0 == null || !VrcfuryLocalization.IsApplied) return;
            try
            {
                var translated = TranslationCatalog.ComponentTitle(__0.GetType().FullName);
                if (translated != null) __result = translated;
            }
            catch { }
        }
        private static void GenericMenuPrefix(ref GUIContent __0)
        {
            if (!VrcfuryLocalization.IsApplied || __0 == null || string.IsNullOrEmpty(__0.text)) return;
            try
            {
                var translated = TranslationCatalog.Menu(__0.text);
                if (translated == __0.text || !HasVrcfuryCaller()) return;
                var replacement = new GUIContent(__0) { text = translated };
                replacement.tooltip = TranslationCatalog.Text(replacement.tooltip);
                __0 = replacement;
            }
            catch { }
        }
        private static bool HasVrcfuryCaller()
        {
            // GenericMenu is shared by every editor. Only calls originating in VRCFury may be translated.
            var stack = new System.Diagnostics.StackTrace(1, false);
            for (var i = 0; i < Math.Min(stack.FrameCount, 18); i++)
            {
                var assembly = stack.GetFrame(i)?.GetMethod()?.DeclaringType?.Assembly.GetName().Name;
                if (assembly != null && assembly.StartsWith("VRCFury-Editor", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
