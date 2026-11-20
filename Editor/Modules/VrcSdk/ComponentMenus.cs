// LGC_VRCMAFury_中文拓展 v2.3.6 — SDK Add Component display
// Localize labels/search captions in the VRChat subtree, preserving every command, ID and original menu path.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class ComponentMenus
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.menus";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static Type itemType;
        private static PropertyInfo contentProperty;
        private static FieldInfo localizedName, searchableName;
        private static readonly List<Edit> edits = new List<Edit>();
        internal static void Apply()
        {
            SetOff();
            try
            {
                var assembly = typeof(Editor).Assembly;
                itemType = assembly.GetType("UnityEditor.AddComponent.ComponentDropdownItem", false);
                var source = assembly.GetType("UnityEditor.AddComponent.AddComponentDataSource", false);
                if (itemType == null || source == null) return;
                localizedName = AccessTools.Field(itemType, "m_LocalizedName");
                searchableName = AccessTools.Field(itemType, "m_SearchableNameLocalized");
                contentProperty = AccessTools.Property(typeof(AdvancedDropdownItem), "content");
                var rebuild = AccessTools.Method(source, "RebuildTree");
                if (rebuild == null || localizedName == null) return;
                Harmony.Patch(rebuild, postfix: new HarmonyMethod(typeof(ComponentMenus), nameof(TreePostfix)));
            }
            catch { VrcSdkLocalization.Note("SDK 添加组件目录入口不兼容，保留原文。"); }
        }
        private static void TreePostfix(AdvancedDropdownItem __result)
        {
            if (!VrcSdkLocalization.IsApplied) return;
            try { Visit(__result, false); }
            catch { VrcSdkLocalization.Note("SDK 部分添加组件目录保留原文。"); }
        }
        private static void Visit(AdvancedDropdownItem item, bool inSdk)
        {
            if (item == null) return;
            inSdk |= item.name == "VRChat";
            if (inSdk && itemType.IsInstanceOfType(item))
            {
                var translated = TranslationCatalog.ComponentMenu(item.name);
                if (translated != item.name)
                {
                    // RebuildTree creates new items. Restore live instances when disabling without changing their real names.
                    edits.Add(new Edit { Item = new WeakReference<AdvancedDropdownItem>(item),
                        Label = localizedName.GetValue(item), Search = searchableName?.GetValue(item),
                        Content = (contentProperty?.GetValue(item) as GUIContent)?.text });
                    localizedName.SetValue(item, translated);
                    searchableName?.SetValue(item, (translated + " " + item.name).ToLowerInvariant());
                    var content = contentProperty?.GetValue(item) as GUIContent;
                    if (content != null) content.text = translated;
                }
            }
            foreach (var child in item.children) Visit(child, inSdk);
        }
        internal static void SetOff()
        {
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            foreach (var edit in edits)
            {
                if (!edit.Item.TryGetTarget(out var item)) continue;
                try
                {
                    localizedName?.SetValue(item, edit.Label);
                    searchableName?.SetValue(item, edit.Search);
                    var content = contentProperty?.GetValue(item) as GUIContent;
                    if (content != null) content.text = edit.Content;
                }
                catch { }
            }
            edits.Clear();
        }
        private sealed class Edit
        {
            internal WeakReference<AdvancedDropdownItem> Item;
            internal object Label, Search;
            internal string Content;
        }
    }
}
