// LGC_MA_Fury_Localization v2.0.0 — Modular Avatar adapter
// Optional, scoped display-literal patches. Never translate serialized names, resource paths or generated assets.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LgcChina.MaFuryLocalization.ModularAvatar
{
    internal static class DisplayPatches
    {
        private const string HarmonyId = MaLocalization.PackageName + ".ma.display";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly HashSet<MethodBase> activeMethods = new HashSet<MethodBase>();
        private static readonly OpCode[] singleByte = new OpCode[256], doubleByte = new OpCode[256];
        private static readonly List<UiEdit> uiEdits = new List<UiEdit>();
        private static readonly List<WeakReference<VisualElement>> roots = new List<WeakReference<VisualElement>>();
        private static bool enabled;
        internal static bool IsApplied => activeMethods.Count > 0;

        static DisplayPatches()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                var code = (OpCode)field.GetValue(null);
                var value = unchecked((ushort)code.Value);
                if (value < 256) singleByte[value] = code;
                else if ((value & 0xff00) == 0xfe00) doubleByte[value & 0xff] = code;
            }
        }

        internal static void SetApplied(bool value)
        {
            enabled = value;
            if (!value)
            {
                try { Harmony.UnpatchAll(HarmonyId); } catch { }
                activeMethods.Clear();
                RestoreUi();
                return;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!name.StartsWith("nadena.dev.modular-avatar", StringComparison.Ordinal)) continue;
                foreach (var type in SafeTypes(assembly))
                {
                    if (type == null) continue;
                    var isMenuCore = type.Name == "MenuItemCoreGUI";
                    if (!TranslationCatalog.HasLiteralScope(type) && !isMenuCore) continue;
                    var methods = new List<MethodBase>(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
                    methods.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
                    foreach (var method in methods)
                    {
                        try
                        {
                            if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                            if ((!isMenuCore || method.IsConstructor) && !HasTargetLiteral(method)) continue;
                            Patch(method, nameof(LiteralTranspiler), true);
                        }
                        catch { MaLocalization.Note("部分旧版界面文字入口已跳过。"); }
                    }
                }
                var editorBase = assembly.GetType("nadena.dev.modular_avatar.core.editor.MAEditorBase", false);
                var createInspector = editorBase == null ? null : AccessTools.Method(editorBase, "CreateInspectorGUI");
                if (createInspector != null && typeof(VisualElement).IsAssignableFrom(createInspector.ReturnType))
                    Patch(createInspector, nameof(InspectorUiPostfix), false);
                var localizer = assembly.GetType("nadena.dev.modular_avatar.core.editor.UIElementLocalizer", false);
                var localize = localizer == null ? null : AccessTools.Method(localizer, "Localize", new[] { typeof(VisualElement) });
                if (localize != null) Patch(localize, nameof(LocalizedUiPostfix), false);
                var fit = assembly.GetType("nadena.dev.modular_avatar.editor.fit_preview.FitPreviewWindow", false);
                if (fit != null)
                {
                    var onEnable = AccessTools.Method(fit, "OnEnable");
                    if (onEnable != null) Patch(onEnable, nameof(WindowUiPostfix), false);
                }
            }
            foreach (var reference in roots)
            {
                try { if (reference.TryGetTarget(out var root)) TranslateUi(root); }
                catch { /* Old UI properties are optional. */ }
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window != null && window.GetType().Assembly.GetName().Name.StartsWith("nadena.dev.modular-avatar", StringComparison.Ordinal))
                    RememberAndTranslate(window.rootVisualElement);
        }

        private static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types; }
            catch { return Array.Empty<Type>(); }
        }

        private static void Patch(MethodBase method, string name, bool transpiler)
        {
            if (activeMethods.Contains(method)) return;
            try
            {
                var hook = new HarmonyMethod(typeof(DisplayPatches), name);
                Harmony.Patch(method, postfix: transpiler ? null : hook, transpiler: transpiler ? hook : null);
                activeMethods.Add(method);
            }
            catch { MaLocalization.Note("部分旧版界面入口不兼容，已跳过并保留原文。"); }
        }

        private static bool HasTargetLiteral(MethodBase method)
        {
            var bytes = method.GetMethodBody()?.GetILAsByteArray();
            if (bytes == null) return false;
            for (var offset = 0; offset < bytes.Length;)
            {
                var first = bytes[offset++];
                if (first == 0xfe && offset >= bytes.Length) return false;
                var code = first == 0xfe ? doubleByte[bytes[offset++]] : singleByte[first];
                if (code.Size == 0) return false;
                if (code == OpCodes.Ldstr)
                {
                    var text = method.Module.ResolveString(BitConverter.ToInt32(bytes, offset));
                    if (TranslationCatalog.TryLiteral(method.DeclaringType, text, out _)) return true;
                }
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: offset += 1; break;
                    case OperandType.InlineVar: offset += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: offset += 8; break;
                    case OperandType.InlineSwitch:
                        var count = BitConverter.ToInt32(bytes, offset);
                        offset += 4 + count * 4; break;
                    default: offset += 4; break;
                }
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> LiteralTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var propertyMethod = AccessTools.Method(typeof(EditorGUILayout), "PropertyField",
                new[] { typeof(SerializedProperty), typeof(GUIContent), typeof(GUILayoutOption[]) });
            var replacement = AccessTools.Method(typeof(DisplayPatches), nameof(DrawLocalizedProperty));
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal &&
                    TranslationCatalog.TryLiteral(__originalMethod.DeclaringType, literal, out var translated))
                    instruction.operand = translated;
                if (__originalMethod.DeclaringType.Name == "MenuItemCoreGUI" &&
                    instruction.opcode == OpCodes.Call && propertyMethod != null &&
                    instruction.operand is MethodInfo call && call == propertyMethod)
                    instruction.operand = replacement;
                yield return instruction;
            }
        }

        private static bool DrawLocalizedProperty(SerializedProperty property, GUIContent label, GUILayoutOption[] options)
        {
            // Only draw the MA menu-item control-type enum differently. All stored values remain enum indices.
            if (property == null || property.propertyType != SerializedPropertyType.Enum || property.name != "type" ||
                property.serializedObject.targetObject == null ||
                property.serializedObject.targetObject.GetType().FullName != "nadena.dev.modular_avatar.core.ModularAvatarMenuItem")
                return EditorGUILayout.PropertyField(property, label, options);
            var displayNames = property.enumDisplayNames;
            var contents = new GUIContent[displayNames.Length];
            var translatedAny = false;
            for (var i = 0; i < displayNames.Length; i++)
            {
                var original = displayNames[i];
                if (TranslationCatalog.Controls.TryGetValue(original, out var chinese))
                { contents[i] = new GUIContent(chinese); translatedAny = true; }
                else contents[i] = new GUIContent(original);
            }
            if (!translatedAny) return EditorGUILayout.PropertyField(property, label, options);
            var oldMixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup(label, property.enumValueIndex, contents, options);
                if (EditorGUI.EndChangeCheck() && index >= 0 && index < contents.Length) property.enumValueIndex = index;
            }
            finally
            {
                EditorGUI.showMixedValue = oldMixed;
            }
            return false;
        }

        private static void InspectorUiPostfix(VisualElement __result) { RememberAndTranslate(__result); }
        private static void LocalizedUiPostfix(VisualElement __0) { RememberAndTranslate(__0); }
        private static void WindowUiPostfix(object __instance)
        {
            if (!(__instance is EditorWindow window)) return;
            var reference = new WeakReference<EditorWindow>(window);
            EditorApplication.delayCall += () =>
            {
                if (enabled && reference.TryGetTarget(out var target) && target != null)
                    RememberAndTranslate(target.rootVisualElement);
            };
        }

        private static void RememberAndTranslate(VisualElement root)
        {
            if (!enabled || root == null) return;
            try
            {
                var seen = false;
                for (var i = roots.Count - 1; i >= 0; i--)
                {
                    if (!roots[i].TryGetTarget(out var old)) roots.RemoveAt(i);
                    else if (ReferenceEquals(old, root)) seen = true;
                }
                if (!seen) roots.Add(new WeakReference<VisualElement>(root));
                TranslateUi(root);
            }
            catch { MaLocalization.Note("部分旧版界面标签已跳过。"); }
        }

        private static void TranslateUi(VisualElement root)
        {
            TranslateProperty(root, "text");
            TranslateProperty(root, "label");
            TranslateProperty(root, "tooltip");
            foreach (var child in root.Children()) TranslateUi(child);
        }

        private static void TranslateProperty(VisualElement element, string name)
        {
            try
            {
                // Text input values are user data, not UI labels.
                if (name == "text" && !(element is UnityEngine.UIElements.Label) && !(element is Button) && !(element is HelpBox)) return;
                var property = element.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(string) || !property.CanRead || !property.CanWrite) return;
                var original = property.GetValue(element) as string;
                if (original == null || !TranslationCatalog.UiLiterals.TryGetValue(original, out var chinese)) return;
                property.SetValue(element, chinese);
                uiEdits.Add(new UiEdit { element = new WeakReference<VisualElement>(element), property = property, original = original, translated = chinese });
            }
            catch { /* A missing property in an old version is optional. */ }
        }

        private static void RestoreUi()
        {
            foreach (var edit in uiEdits)
            {
                try
                {
                    if (edit.element.TryGetTarget(out var target) && (string)edit.property.GetValue(target) == edit.translated)
                        edit.property.SetValue(target, edit.original);
                }
                catch { }
            }
            uiEdits.Clear();
        }
        private sealed class UiEdit
        {
            internal WeakReference<VisualElement> element;
            internal PropertyInfo property;
            internal string original, translated;
        }
    }
}
