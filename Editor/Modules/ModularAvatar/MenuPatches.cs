// LGC_VRCMAFury_中文拓展 v2.3.7 — Modular Avatar adapter
// Optional menu and inspector-title display patches. Preserve all original commands and component identities.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.ModularAvatar
{
    internal static class MenuPatches
    {
        private const string HarmonyId = MaLocalization.PackageName + ".ma.menus";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly HashSet<MethodBase> activeMethods = new HashSet<MethodBase>();
        private static Type itemType, scriptingItemType;
        private static PropertyInfo menuPathProperty, contentProperty, scriptingPathProperty;
        private static FieldInfo localizedNameField, searchableNameField;
        internal static bool IsApplied => activeMethods.Count > 0;

        internal static void SetApplied(bool enabled)
        {
            if (!enabled)
            {
                try { Harmony.UnpatchAll(HarmonyId); } catch { }
                activeMethods.Clear();
                return;
            }
            var assembly = typeof(Editor).Assembly;
            itemType = assembly.GetType("UnityEditor.AddComponent.ComponentDropdownItem", false);
            var source = assembly.GetType("UnityEditor.AddComponent.AddComponentDataSource", false);
            if (itemType != null && source != null)
            {
                menuPathProperty = AccessTools.Property(itemType, "menuPath");
                contentProperty = AccessTools.Property(typeof(AdvancedDropdownItem), "content");
                localizedNameField = AccessTools.Field(itemType, "m_LocalizedName");
                searchableNameField = AccessTools.Field(itemType, "m_SearchableNameLocalized");
                if (menuPathProperty != null && localizedNameField != null)
                {
                    TryPatch(AccessTools.Method(source, "RebuildTree"), nameof(ComponentTreePostfix), false, "添加组件列表");
                    TryPatch(AccessTools.Method(source, "Search"), nameof(ComponentTreePostfix), false, "添加组件搜索");
                }
                else MaLocalization.Note("添加组件显示接口不同，已跳过该部分。");
            }
            else MaLocalization.Note("未发现对应添加组件窗口，已跳过该部分。");

            scriptingItemType = assembly.GetType("UnityEditor.ScriptingMenuItem", false);
            scriptingPathProperty = scriptingItemType == null ? null : AccessTools.Property(scriptingItemType, "path");
            var utilities = assembly.GetType("UnityEditor.MenuUtils", false);
            var extract = utilities == null ? null : AccessTools.Method(utilities, "ExtractOnlyEnabledMenuItem");
            var parameters = extract?.GetParameters();
            if (parameters != null && parameters.Length >= 3 && parameters[2].ParameterType == typeof(string) &&
                parameters[0].ParameterType == scriptingItemType && scriptingPathProperty != null)
                TryPatch(extract, nameof(ContextPrefix), true, "上下文菜单");
            else MaLocalization.Note("上下文菜单接口不同，已跳过该部分。");

            TryPatch(AccessTools.Method(typeof(ObjectNames), "GetInspectorTitle", new[] { typeof(UnityEngine.Object), typeof(bool) }),
                nameof(InspectorTitlePostfix), false, "组件标题");
        }

        private static void TryPatch(MethodBase method, string patch, bool prefix, string feature)
        {
            if (method == null) { MaLocalization.Note(feature + "接口不存在，已跳过。"); return; }
            if (activeMethods.Contains(method)) return;
            try
            {
                var hook = new HarmonyMethod(typeof(MenuPatches), patch);
                Harmony.Patch(method, prefix: prefix ? hook : null, postfix: prefix ? null : hook);
                activeMethods.Add(method);
            }
            catch { MaLocalization.Note(feature + "接口不兼容，已跳过。"); }
        }

        private static void ComponentTreePostfix(AdvancedDropdownItem __result)
        {
            try { TranslateComponentTree(__result, false); }
            catch { MaLocalization.Note("部分添加组件条目未能翻译，保留原版显示。"); }
        }

        private static void TranslateComponentTree(AdvancedDropdownItem item, bool withinMa)
        {
            if (item == null) return;
            withinMa |= item.name == "Modular Avatar";
            if (itemType.IsInstanceOfType(item))
            {
                var path = TranslationCatalog.NormalizeComponentPath(menuPathProperty.GetValue(item) as string);
                withinMa |= path.StartsWith("Modular Avatar/", StringComparison.Ordinal);
                string translated = null;
                if (!TranslationCatalog.Components.TryGetValue(path, out translated) && withinMa && item.name == "Vertex Filters")
                    translated = "顶点过滤器 (Vertex Filters)";
                if (translated != null)
                {
                    localizedNameField.SetValue(item, translated);
                    searchableNameField?.SetValue(item, translated.ToLowerInvariant());
                    var content = contentProperty?.GetValue(item) as GUIContent;
                    if (content != null) content.text = translated;
                }
            }
            foreach (var child in item.children) TranslateComponentTree(child, withinMa);
        }

        private static void ContextPrefix(object __0, ref string __2)
        {
            try
            {
                var path = scriptingPathProperty?.GetValue(__0) as string;
                if (path == null || __2 == null || !TranslationCatalog.Menus.TryGetValue(path, out var translated)) return;
                // Only the label changes; the ScriptingMenuItem and Unity callback retain the original real path.
                var leaf = path.Substring(path.LastIndexOf('/') + 1);
                if (__2 != leaf && !__2.EndsWith("/" + leaf, StringComparison.Ordinal)) return;
                var next = __2.Substring(0, __2.Length - leaf.Length) + translated;
                __2 = next.Replace("/Debug/", "/调试/");
            }
            catch { /* Keep the original callback and menu label on older interfaces. */ }
        }

        private static void InspectorTitlePostfix(UnityEngine.Object __0, ref string __result)
        {
            try
            {
                if (__0 == null) return;
                var type = __0.GetType();
                if (!type.Assembly.GetName().Name.StartsWith("nadena.dev.modular-avatar", StringComparison.Ordinal)) return;
                if (TranslationCatalog.ComponentTypes.TryGetValue(type.FullName, out var translated))
                {
                    var suffix = __result != null && __result.Contains("(Deprecated)") ? "（已弃用）" : string.Empty;
                    __result = translated + suffix;
                }
            }
            catch { /* Keep the original title for unknown types. */ }
        }
    }
}
