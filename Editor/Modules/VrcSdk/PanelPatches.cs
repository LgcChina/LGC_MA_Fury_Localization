// LGC_VRCMAFury_中文拓展 v2.3.6 — SDK upload page display adapter
// Scope edits to SDK upload controls and its save/discard sibling. Preserve data, choices and validation records.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine.UIElements;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class PanelPatches
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.panel";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static Type panelType;
        private static FieldInfo builderField;
        private static double nextUpdate;
        private static readonly List<Edit> edits = new List<Edit>();
        private static ConditionalWeakTable<VisualElement, Dictionary<string, Edit>> byElement =
            new ConditionalWeakTable<VisualElement, Dictionary<string, Edit>>();
        internal static bool IsApplied { get; private set; }

        internal static bool Apply(Type panel)
        {
            SetOff();
            panelType = panel;
            builderField = panelType == null ? null : AccessTools.Field(panelType, "_builderPanel");
            if (builderField == null || !typeof(VisualElement).IsAssignableFrom(builderField.FieldType))
            { VrcSdkLocalization.Note("SDK 上传页根节点接口不存在，已跳过。"); return false; }
            IsApplied = true;
            EditorApplication.update += Update;
            // Translate warnings at their display container, leaving Issue.issueText and comparisons untouched.
            foreach (var name in new[] { "CreateIssueBoxGUI", "CreateLinkBoxGUI" })
            {
                var method = AccessTools.DeclaredMethod(panelType, name);
                if (method == null || !typeof(VisualElement).IsAssignableFrom(method.ReturnType)) continue;
                try { Harmony.Patch(method, postfix: new HarmonyMethod(typeof(PanelPatches), nameof(DisplayPostfix))); }
                catch (Exception e) { VrcSdkLocalization.Note("SDK 上传提示入口已跳过：" + name + "（" + e.GetBaseException().Message + "）"); }
            }
            // Translate option labels after creation, keeping option IDs and original name getters unchanged.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type warnings;
                try { warnings = assembly.GetType("VRC.SDKBase.Editor.Elements.ContentWarningsField", false); }
                catch { continue; }
                if (warnings == null) continue;
                var method = AccessTools.Method(warnings, "CreateOption", new[] { typeof(string) });
                if (method != null && typeof(VisualElement).IsAssignableFrom(method.ReturnType))
                {
                    try { Harmony.Patch(method, postfix: new HarmonyMethod(typeof(PanelPatches), nameof(OptionPostfix))); }
                    catch (Exception e) { VrcSdkLocalization.Note("SDK 内容警告选项入口已跳过：" + e.GetBaseException().Message); }
                }
                break;
            }
            VrcSdkLocalization.Note("SDK 上传页已接入：显示标签、下拉显示格式、提示与状态；原上传数据和检查记录保持原样。");
            try { UploadDisplayPatches.Apply(); }
            catch (Exception e)
            {
                UploadDisplayPatches.SetOff();
                VrcSdkLocalization.Note("SDK 上传独立弹窗入口已跳过：" + e.GetBaseException().Message);
            }
            Update();
            return true;
        }

        private static void DisplayPostfix(VisualElement __result)
        {
            if (IsApplied && VrcSdkLocalization.IsApplied && __result != null) Walk(__result);
        }
        private static void OptionPostfix(VisualElement __instance, VisualElement __result)
        {
            if (IsApplied && VrcSdkLocalization.IsApplied && __result != null &&
                __instance?.GetType().FullName == "VRC.SDKBase.Editor.Elements.ContentWarningsField") Walk(__result);
        }
        private static void Update()
        {
            if (!IsApplied || !VrcSdkLocalization.IsApplied || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.4;
            foreach (var window in UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == null || window.GetType() != panelType) continue;
                try
                {
                    var root = builderField.GetValue(window) as VisualElement;
                    if (root?.panel != null) Walk(root);
                    // SDK mounts save/discard controls beside the builder, not inside its subtree.
                    var saveChanges = window.rootVisualElement.Q("save-changes-block");
                    if (saveChanges != null) Walk(saveChanges);
                }
                catch (Exception e) { VrcSdkLocalization.Note("SDK 上传页显示更新已跳过：" + e.GetBaseException().GetType().Name); }
            }
            // Release records for detached elements when validation rows or avatars are rebuilt.
            edits.RemoveAll(edit => !edit.Target.TryGetTarget(out _));
        }

        private static bool IsStringPopup(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition().Name == "PopupField`1" &&
                    t.GetGenericArguments()[0] == typeof(string)) return true;
            return false;
        }
        private static bool IsInput(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.Name == "ObjectField" || t.IsGenericType && t.GetGenericTypeDefinition().Name == "TextInputBaseField`1") return true;
            return typeof(TextField).IsAssignableFrom(type) || type.Name == "ObjectField";
        }
        private static void Walk(VisualElement element)
        {
            try { WalkCore(element); }
            catch (Exception e)
            {
                VrcSdkLocalization.Note("SDK 上传页可选控件已跳过：" + element.GetType().Name + "（" + e.GetBaseException().Message + "）");
            }
        }
        private static void WalkCore(VisualElement element)
        {
            // Avatar names, user tags, identifiers, dates, versions and object selectors are data.
            if (element.name == "avatar-selector-dropdown" || element.name == "tags-row" ||
                element.name == "last-updated-label" || element.name == "version-label" ||
                element.name == "content-platform-info") return;
            var type = element.GetType();
            if (IsInput(type))
            {
                var label = AccessTools.Property(type, "labelElement")?.GetValue(element) as Label;
                if (label != null) Caption(label);
                if (type.FullName == "VRC.SDKBase.Editor.Elements.VRCTextField" && element is TextField input)
                    Placeholder(input);
                return; // Never walk the text editor or translate its entered value.
            }
            if (IsStringPopup(type))
            {
                var label = AccessTools.Property(type, "labelElement")?.GetValue(element) as Label;
                if (label != null) Caption(label);
                var allowed = element.name == "build-type-dropdown" || element.name == "avatar-primary-style" ||
                    element.name == "avatar-secondary-style" || element.parent?.name == "visibility-block";
                if (!allowed) return;
                ConfigurePopup(element);
            }
            if (element is TextElement caption) Caption(caption);
            var tip = TranslationCatalog.PanelText(element.tooltip);
            if (tip != element.tooltip) Set(element, "tooltip", element.tooltip, tip, (v, text) => v.tooltip = text, v => v.tooltip);
            for (var i = 0; i < element.hierarchy.childCount; i++) Walk(element.hierarchy[i]);
        }
        private static void Caption(TextElement element)
        {
            var original = element.text;
            var translated = TranslationCatalog.PanelText(original);
            if (translated != original)
                Set(element, "text", original, translated, (v, text) => ((TextElement)v).text = text, v => ((TextElement)v).text);
        }
        private static void Placeholder(TextField input)
        {
            // SDK marks placeholders with a dedicated class. Update its sentinel and visible placeholder together.
            var field = AccessTools.Field(input.GetType(), "_placeholder");
            if (field == null || field.FieldType != typeof(string)) return;
            var original = field.GetValue(input) as string;
            var translated = TranslationCatalog.PanelText(original);
            if (original == translated) return;
            Set(input, "placeholder", original, translated, (v, text) =>
            {
                var control = (TextField)v;
                var previous = field.GetValue(control) as string;
                var isPlaceholder = AccessTools.Method(control.GetType(), "IsPlaceholder", Type.EmptyTypes);
                if (isPlaceholder != null && isPlaceholder.Invoke(control, null) is bool active && active && control.value == previous)
                    control.SetValueWithoutNotify(text);
                field.SetValue(control, text);
            }, v => field.GetValue(v) as string);
        }
        private static void ConfigurePopup(VisualElement element)
        {
            foreach (var name in new[] { "formatListItemCallback", "formatSelectedValueCallback" })
            {
                var property = AccessTools.Property(element.GetType(), name);
                if (property == null || !property.CanRead || !property.CanWrite || property.PropertyType != typeof(Func<string, string>)) continue;
                var records = byElement.GetOrCreateValue(element);
                if (records.TryGetValue(name, out var previous) && ReferenceEquals(property.GetValue(element), previous.TranslatedObject)) continue;
                var original = property.GetValue(element) as Func<string, string>;
                Func<string, string> wrapper = value => TranslationCatalog.PanelText(original == null ? value : original(value));
                var edit = new Edit { Target = new WeakReference<VisualElement>(element), OriginalObject = original, TranslatedObject = wrapper,
                    Read = v => property.GetValue(v), Write = (v, value) => property.SetValue(v, value) };
                records[name] = edit;
                edits.Add(edit);
                property.SetValue(element, wrapper);
            }
        }
        private static void Set(VisualElement element, string key, string original, string translated,
            Action<VisualElement, string> write, Func<VisualElement, string> read)
        {
            var records = byElement.GetOrCreateValue(element);
            if (!records.TryGetValue(key, out var edit))
            {
                edit = new Edit { Target = new WeakReference<VisualElement>(element), Read = v => read(v), Write = (v, value) => write(v, (string)value) };
                records[key] = edit;
                edits.Add(edit);
            }
            edit.OriginalObject = original;
            edit.TranslatedObject = translated;
            write(element, translated);
        }
        internal static void SetOff()
        {
            IsApplied = false;
            UploadDisplayPatches.SetOff();
            EditorApplication.update -= Update;
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            for (var i = edits.Count - 1; i >= 0; i--)
            {
                var edit = edits[i];
                try
                {
                    if (edit.Target.TryGetTarget(out var target) && Equals(edit.Read(target), edit.TranslatedObject))
                        edit.Write(target, edit.OriginalObject);
                }
                catch { /* An unavailable optional display property keeps its current value. */ }
            }
            edits.Clear();
            byElement = new ConditionalWeakTable<VisualElement, Dictionary<string, Edit>>();
            nextUpdate = 0;
        }
        private sealed class Edit
        {
            internal WeakReference<VisualElement> Target;
            internal object OriginalObject, TranslatedObject;
            internal Func<VisualElement, object> Read;
            internal Action<VisualElement, object> Write;
        }
    }
}