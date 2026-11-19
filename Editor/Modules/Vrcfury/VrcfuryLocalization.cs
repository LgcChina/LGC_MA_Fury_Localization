// LGC_MA_Fury_Localization v2.0.0 — VRCFury adapter
// Optional display hooks for features, actions and SPS. Serialized values and original package files are untouched.
using System;
using System.Collections.Generic;
using System.Reflection;
using OpCodes = System.Reflection.Emit.OpCodes;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.UIElements;

namespace LgcChina.MaFuryLocalization.Vrcfury
{
    internal static class VrcfuryLocalization
    {
        internal const string Version = LocalizationTool.Version;
        internal const string PackageName = LocalizationTool.PackageName;
        private const string HarmonyId = PackageName + ".vrcfury.ui";
        private const string EnabledKey = PackageName + ".vrcfury.enabled";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly HashSet<MethodBase> patched = new HashSet<MethodBase>();
        private static readonly List<WeakReference<VisualElement>> roots = new List<WeakReference<VisualElement>>();
        private static readonly List<UiEdit> edits = new List<UiEdit>();
        private static readonly List<EnumEdit> enums = new List<EnumEdit>();
        private static readonly ConditionalWeakTable<VisualElement, Dictionary<string, UiEdit>> elementEdits =
            new ConditionalWeakTable<VisualElement, Dictionary<string, UiEdit>>();
        private static readonly List<string> notes = new List<string>();
        private static double nextScan;
        internal static bool IsInstalled { get; private set; }
        internal static bool IsApplied { get; private set; }
        internal static string Status { get; private set; } = "等待接入 VRCFury";
        internal static string VrcfuryVersion { get; private set; } = "未知";
        internal static string Details => string.Join("\n", notes);

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var type = assembly.GetType(name, false); if (type != null) return type; }
                catch { }
            }
            return null;
        }

        internal static void Note(string message)
        {
            if (!string.IsNullOrEmpty(message) && !notes.Contains(message)) notes.Add(message);
        }

        internal static void Initialize()
        {
            SetOff();
            notes.Clear();
            try
            {
                var finder = FindType("VF.Feature.Base.FeatureFinder");
                IsInstalled = finder != null;
                if (!IsInstalled)
                {
                    VrcfuryVersion = "未安装";
                    Status = "未安装 VRCFury，已跳过";
                    return;
                }
                VrcfuryVersion = PackageInfo.FindForAssembly(finder.Assembly)?.version ?? "未知";
                if (!SessionState.GetBool(EnabledKey, true))
                {
                    Status = "已停用，重新选中对象后恢复原版显示";
                    return;
                }
                TranslationCatalog.Load();
                foreach (var warning in TranslationCatalog.Warnings) Note(warning);
                // Hooks installed during initialization are inert until IsApplied becomes true.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = assembly.GetName().Name;
                    if (!name.StartsWith("VRCFury-Editor", StringComparison.Ordinal)) continue;
                    foreach (var type in SafeTypes(assembly))
                    {
                        if (type == null || type.ContainsGenericParameters) continue;
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            if (method.IsAbstract || method.ContainsGenericParameters) continue;
                            if (typeof(VisualElement).IsAssignableFrom(method.ReturnType))
                            {
                                var inspector = (type.Namespace ?? "").StartsWith("VF.Inspector", StringComparison.Ordinal);
                                var root = method.Name == "RenderFeatureEditor" && type == finder ||
                                    method.Name == "CreatePropertyGUI" && typeof(PropertyDrawer).IsAssignableFrom(type) ||
                                    method.Name == "CreateEditor" && typeof(Editor).IsAssignableFrom(type) ||
                                    inspector && (method.Name == "CreateEditor" || method.Name == "CreateInspectorGUI" ||
                                        method.Name == "Render" || method.Name == "render" || method.Name == "Create");
                                if (root) TryPatch(method, nameof(RootPostfix), false);
                            }
                        }

                    }
                }
                var helper = FindType("VF.Inspector.VRCFuryEditorUtils");
                if (helper != null)
                    foreach (var method in helper.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (method.ContainsGenericParameters) continue;
                        if (method.Name == "Prop" && method.GetParameters().Length > 0 &&
                            method.GetParameters()[0].ParameterType == typeof(SerializedProperty) &&
                            typeof(VisualElement).IsAssignableFrom(method.ReturnType))
                            TryPatch(method, nameof(PropPostfix), false);
                        else if (typeof(VisualElement).IsAssignableFrom(method.ReturnType) &&
                            (method.Name == "Info" || method.Name == "Warn" || method.Name == "Error" ||
                                method.Name == "Section" || method.Name == "WrappedLabel" || method.Name == "DebugBox"))
                            TryPatch(method, nameof(InlinePostfix), false);
                    }
                var dialogs = FindType("VF.Utils.DialogUtils");
                if (dialogs != null)
                    foreach (var method in dialogs.GetMethods(BindingFlags.Static | BindingFlags.Public))
                        if (method.Name == "DisplayDialog" || method.Name == "DisplayDialogComplex")
                            TryPatch(method, nameof(DialogPrefix), true);
                var search = FindType("VF.Inspector.VRCFurySearchWindowProvider")?.GetMethod("CreateSearchTree",
                    BindingFlags.Public | BindingFlags.Instance);
                if (search != null) TryPatch(search, nameof(SearchPostfix), false);
                PatchTitle(FindType("VF.Inspector.VRCFuryComponentHeader"), "RenderHeader");
                PatchTitle(FindType("VF.Inspector.VRCFuryActionDrawer"), "Title");
                var materialHeader = FindType("VF.Inspector.SpsMaterialEditor")?.GetMethod("DrawHeader",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (materialHeader != null)
                    TryPatch(materialHeader, nameof(MaterialHeaderTranspiler), false, true);
                IsApplied = patched.Count > 0;
                if (!IsApplied) { Status = "未找到可接入的 VRCFury 界面，已跳过"; return; }
                try { MenuPatches.SetApplied(true); }
                catch { MenuPatches.SetApplied(false); Note("菜单接口不兼容，已跳过菜单部分。"); }
                foreach (var field in enums) field.Apply();
                EditorApplication.update -= ScanRoots;
                EditorApplication.update += ScanRoots;
                Status = notes.Count == 0 ? "VRCFury 中文已启用" : "VRCFury 中文已启用，部分可选接口已跳过";
                RefreshRoots();
            }
            catch (Exception e)
            {
                SetOff();
                Status = "当前接口或词典无法接入，已保留原版显示";
                Note((e.InnerException ?? e).Message);
            }
            finally { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types; }
            catch { return Array.Empty<Type>(); }
        }

        private static void PatchTitle(Type type, string name)
        {
            if (type == null) return;
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.Name == name && method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(string))
                    TryPatch(method, nameof(TitlePrefix), true);
        }

        private static void TryPatch(MethodBase method, string hookName, bool prefix, bool transpiler = false)
        {
            if (method == null || patched.Contains(method)) return;
            try
            {
                var hook = new HarmonyMethod(typeof(VrcfuryLocalization), hookName);
                Harmony.Patch(method, prefix: prefix ? hook : null,
                    postfix: !prefix && !transpiler ? hook : null, transpiler: transpiler ? hook : null);
                patched.Add(method);
            }
            catch { Note("部分界面接口不兼容，已保留该部分原版显示。"); }
        }

        private static void TitlePrefix(ref string __0)
        {
            try { if (IsApplied) __0 = TranslationCatalog.Title(__0); } catch { }
        }
        private static void DialogPrefix(object[] __args, MethodBase __originalMethod)
        {
            if (!IsApplied) return;
            try
            {
                var parameters = __originalMethod.GetParameters();
                for (var i = 0; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == typeof(string) && __args[i] is string text)
                        __args[i] = i == 0 ? TranslationCatalog.Title(text) : TranslationCatalog.Text(text);
            }
            catch { }
        }
        private static void SearchPostfix(object __result)
        {
            if (!IsApplied || !(__result is System.Collections.IEnumerable entries)) return;
            try
            {
                foreach (var entry in entries)
                {
                    if (entry == null) continue;
                    var type = entry.GetType();
                    var level = AccessTools.Field(type, "level")?.GetValue(entry) ?? AccessTools.Property(type, "level")?.GetValue(entry);
                    if (!(level is int depth) || depth != 0) continue;
                    var field = AccessTools.Field(type, "content");
                    var property = AccessTools.Property(type, "content");
                    var original = (field?.GetValue(entry) ?? property?.GetValue(entry)) as GUIContent;
                    if (original == null) continue;
                    var translated = TranslationCatalog.Title(original.text);
                    if (translated == original.text) continue;
                    var replacement = new GUIContent(original) { text = translated };
                    if (field != null) field.SetValue(entry, replacement);
                    else if (property?.CanWrite == true) property.SetValue(entry, replacement);
                }
            }
            catch { /* Asset and object search entries retain their original names and userData. */ }
        }
        private static void RootPostfix(VisualElement __result)
        {
            try { if (IsApplied && __result != null) Remember(__result); } catch { }
        }
        private static void InlinePostfix(VisualElement __result)
        {
            try { if (IsApplied && __result != null) Translate(__result); } catch { }
        }
        private static void PropPostfix(SerializedProperty __0, VisualElement __result)
        {
            if (!IsApplied || __result == null) return;
            try
            {
                if (__0 != null && __0.propertyType == SerializedPropertyType.Enum)
                    ConfigureEnumFields(__result, true);
                Translate(__result);
            }
            catch { }
        }
        private static IEnumerable<CodeInstruction> MaterialHeaderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var translate = AccessTools.Method(typeof(TranslationCatalog), nameof(TranslationCatalog.Text));
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand as string == "SPS Properties")
                    yield return new CodeInstruction(OpCodes.Call, translate);
            }
        }

        private static void Remember(VisualElement root)
        {
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                if (!roots[i].TryGetTarget(out var existing)) roots.RemoveAt(i);
                else if (ReferenceEquals(existing, root)) { Translate(root); return; }
            }
            roots.Add(new WeakReference<VisualElement>(root));
            root.RegisterCallback<ContextualMenuPopulateEvent>(TranslateContextMenu);
            Translate(root);
        }

        private static void TranslateContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (!IsApplied) return;
            // Run on the translated editor's root after its child menu callbacks have populated the menu.
            // Retain each action object, callback, userData and status function; only its caption changes.
            foreach (var item in evt.menu.MenuItems())
            {
                if (!(item is DropdownMenuAction action)) continue;
                var translated = TranslationCatalog.Menu(action.name);
                if (translated == action.name) continue;
                try
                {
                    var property = action.GetType().GetProperty("name");
                    var setter = property?.GetSetMethod(true);
                    if (setter != null) setter.Invoke(action, new object[] { translated });
                    else
                    {
                        var field = AccessTools.Field(action.GetType(), "<name>k__BackingField") ??
                            AccessTools.Field(action.GetType(), "m_Name");
                        field?.SetValue(action, translated);
                    }
                }
                catch { /* A private caption interface is optional. */ }
            }
        }

        private static void ScanRoots()
        {
            if (!IsApplied || EditorApplication.timeSinceStartup < nextScan) return;
            nextScan = EditorApplication.timeSinceStartup + 0.5;
            RefreshRoots();
        }
        private static void RefreshRoots()
        {
            var live = new HashSet<VisualElement>();
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                if (!roots[i].TryGetTarget(out var root)) roots.RemoveAt(i);
                else if (root.panel != null) live.Add(root);
            }
            foreach (var root in live)
            {
                var nested = false;
                for (var parent = root.parent; parent != null; parent = parent.parent)
                    if (live.Contains(parent)) { nested = true; break; }
                if (!nested) { try { Translate(root); } catch { } }
            }
            for (var i = edits.Count - 1; i >= 0; i--)
                if (!edits[i].Element.TryGetTarget(out _)) edits.RemoveAt(i);
            for (var i = enums.Count - 1; i >= 0; i--)
                if (!enums[i].Element.TryGetTarget(out _)) enums.RemoveAt(i);
        }

        private static bool IsInput(VisualElement element) =>
            element.ClassListContains("unity-base-field__input") ||
            element.ClassListContains("unity-base-text-field__input") ||
            element.ClassListContains("unity-object-field__input") ||
            element.ClassListContains("unity-base-popup-field__input");

        private static void Translate(VisualElement element)
        {
            if (!IsApplied || IsInput(element)) return;
            if (element is PopupField<string> popup && TranslationCatalog.IsDisplayChoiceSet(popup.choices))
                ConfigureEnum(popup);
            // Never write a field's value, text input, object reference, choices or bindingPath.
            if (element is Label || element is Button || element is Foldout || element is HelpBox)
                TranslateProperty(element, "text");
            TranslateProperty(element, "label");
            TranslateProperty(element, "tooltip");
            foreach (var child in element.Children()) Translate(child);
        }

        private static void TranslateProperty(VisualElement element, string name)
        {
            try
            {
                var property = element.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(string) || !property.CanRead || !property.CanWrite ||
                    property.GetIndexParameters().Length != 0) return;
                var original = property.GetValue(element) as string;
                var translated = TranslationCatalog.Text(original);
                if (original == null || translated == original) return;
                var records = elementEdits.GetOrCreateValue(element);
                if (!records.TryGetValue(name, out var edit))
                {
                    edit = new UiEdit { Element = new WeakReference<VisualElement>(element), Property = property };
                    records[name] = edit;
                    edits.Add(edit);
                }
                edit.Original = original;
                edit.Translated = translated;
                property.SetValue(element, translated);
            }
            catch { }
        }

        private static void ConfigureEnumFields(VisualElement root, bool isEnum)
        {
            if (root is PopupField<string> field && isEnum) ConfigureEnum(field);
            if (IsInput(root)) return;
            foreach (var child in root.Children()) ConfigureEnumFields(child, isEnum);
        }
        private static void ConfigureEnum(PopupField<string> field)
        {
            foreach (var edit in enums)
                if (edit.Element.TryGetTarget(out var existing) && ReferenceEquals(existing, field)) { edit.Apply(); return; }
            var added = new EnumEdit(field);
            enums.Add(added);
            added.Apply();
        }

        internal static void SetOff()
        {
            IsApplied = false;
            EditorApplication.update -= ScanRoots;
            try { MenuPatches.SetApplied(false); } catch { }
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            patched.Clear();
            foreach (var edit in enums) { try { edit.Restore(); } catch { } }
            for (var i = edits.Count - 1; i >= 0; i--)
            {
                var edit = edits[i];
                try
                {
                    if (edit.Element.TryGetTarget(out var element) && (string)edit.Property.GetValue(element) == edit.Translated)
                        edit.Property.SetValue(element, edit.Original);
                }
                catch { }
            }
            // Keep weak roots and enum descriptors so reloading a dictionary can update an already open inspector.
            foreach (var edit in edits)
                if (edit.Element.TryGetTarget(out var element) && elementEdits.TryGetValue(element, out var records)) records.Clear();
            edits.Clear();
        }
        internal static void Toggle()
        {
            SessionState.SetBool(EnabledKey, !IsApplied);
            Initialize();
        }
        internal static void Reload()
        {
            try { TranslationCatalog.Load(true); }
            catch (Exception e)
            {
                Status = "词典读取失败，保留当前显示状态";
                notes.Clear();
                Note((e.InnerException ?? e).Message);
                return;
            }
            Initialize();
            Note("已重新读取 VRCFury 词典；添加组件窗口、菜单和旧检查器请关闭后重开。");
        }

        private sealed class UiEdit
        {
            internal WeakReference<VisualElement> Element;
            internal PropertyInfo Property;
            internal string Original, Translated;
        }
        private sealed class EnumEdit
        {
            internal readonly WeakReference<PopupField<string>> Element;
            private readonly Func<string, string> originalSelected, originalList, translatedSelected, translatedList;
            private bool applied;
            internal EnumEdit(PopupField<string> field)
            {
                Element = new WeakReference<PopupField<string>>(field);
                originalSelected = field.formatSelectedValueCallback;
                originalList = field.formatListItemCallback;
                translatedSelected = source => TranslationCatalog.EnumText(originalSelected != null ? originalSelected(source) : source);
                translatedList = source => TranslationCatalog.EnumText(originalList != null ? originalList(source) : source);
            }
            internal void Apply()
            {
                if (applied || !Element.TryGetTarget(out var field)) return;
                if (field.formatSelectedValueCallback != originalSelected || field.formatListItemCallback != originalList) return;
                field.formatSelectedValueCallback = translatedSelected;
                field.formatListItemCallback = translatedList;
                field.SetValueWithoutNotify(field.value);
                applied = true;
            }
            internal void Restore()
            {
                if (!applied || !Element.TryGetTarget(out var field)) return;
                if (field.formatSelectedValueCallback == translatedSelected) field.formatSelectedValueCallback = originalSelected;
                if (field.formatListItemCallback == translatedList) field.formatListItemCallback = originalList;
                field.SetValueWithoutNotify(field.value);
                applied = false;
            }
        }
    }
}
