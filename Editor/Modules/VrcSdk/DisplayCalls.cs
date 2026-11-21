// LGC_VRCMAFury_中文拓展 v2.3.7 — SDK GUI call-site translation
// Rewrite only display arguments at SDK-owned GUI call sites. Input values, paths and API calls are untouched.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using ILGenerator = System.Reflection.Emit.ILGenerator;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class DisplayCalls
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source, ILGenerator generator, bool account, bool panel = false)
        {
            foreach (var instruction in source)
            {
                var called = instruction.operand as MethodInfo;
                if (panel && called != null && called.DeclaringType == typeof(EditorWindow) && called.Name == "set_titleContent" &&
                    (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
                {
                    var title = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DisplayCalls), nameof(PanelContent)));
                    var setter = new CodeInstruction(instruction);
                    title.labels.AddRange(setter.labels);
                    setter.labels.Clear();
                    foreach (var block in setter.blocks.ToArray())
                        if (block.blockType != ExceptionBlockType.EndExceptionBlock)
                        { title.blocks.Add(block); setter.blocks.Remove(block); }
                    yield return title;
                    yield return setter;
                    continue;
                }
                if (instruction.opcode != OpCodes.Call || called == null || !called.IsStatic)
                { yield return instruction; continue; }
                // Preserve overloads and return values by invoking the original GUI call after translation.
                var replacement = PropertyReplacement(called);
                if (!account && !panel && replacement != null)
                {
                    var copy = new CodeInstruction(instruction) { operand = replacement };
                    yield return copy;
                    continue;
                }
                var slots = DisplaySlots(called);
                if (slots.Count == 0) { yield return instruction; continue; }
                var parameters = called.GetParameters();
                if (parameters.Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer))
                { yield return instruction; continue; }
                var locals = parameters.Select(p => generator.DeclareLocal(p.ParameterType)).ToArray();
                var prefix = new List<CodeInstruction>();
                for (var i = locals.Length - 1; i >= 0; i--) prefix.Add(new CodeInstruction(OpCodes.Stloc, locals[i]));
                for (var i = 0; i < locals.Length; i++)
                {
                    prefix.Add(new CodeInstruction(OpCodes.Ldloc, locals[i]));
                    if (!slots.Contains(i)) continue;
                    var helper = parameters[i].ParameterType == typeof(string)
                        ? panel ? nameof(PanelText) : account ? nameof(AccountText) : nameof(ComponentText)
                        : panel ? nameof(PanelContent) : account ? nameof(AccountContent) : nameof(ComponentContent);
                    prefix.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DisplayCalls), helper)));
                }
                var call = new CodeInstruction(instruction);
                prefix[0].labels.AddRange(call.labels);
                call.labels.Clear();
                // Begin blocks follow the first injected instruction; end blocks stay after the call.
                foreach (var block in call.blocks.ToArray())
                    if (block.blockType != ExceptionBlockType.EndExceptionBlock)
                    { prefix[0].blocks.Add(block); call.blocks.Remove(block); }
                foreach (var added in prefix) yield return added;
                yield return call;
            }
        }

        internal static bool HasDisplayCall(MethodBase method)
        {
            foreach (var instruction in PatchProcessor.GetOriginalInstructions(method))
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo called &&
                    (PropertyReplacement(called) != null || DisplaySlots(called).Count > 0 ||
                        called.DeclaringType == typeof(EditorWindow) && called.Name == "set_titleContent")) return true;
            return false;
        }

        private static List<int> DisplaySlots(MethodInfo method)
        {
            var result = new List<int>();
            var owner = method.DeclaringType;
            var unityGui = owner == typeof(EditorGUILayout) || owner == typeof(EditorGUI) || owner == typeof(GUILayout) || owner == typeof(GUI);
            var dialog = owner == typeof(EditorUtility) && (method.Name == "DisplayDialog" || method.Name == "DisplayDialogComplex");
            var filePicker = owner == typeof(EditorUtility) && (method.Name == "OpenFilePanel" || method.Name == "OpenFilePanelWithFilters" ||
                method.Name == "SaveFilePanel" || method.Name == "OpenFolderPanel");
            var progressBar = owner == typeof(EditorUtility) && (method.Name == "DisplayProgressBar" || method.Name == "DisplayCancelableProgressBar");
            var progress = owner == typeof(Progress) && (method.Name == "Start" || method.Name == "Report");
            if (!unityGui && !dialog && !filePicker && !progressBar && !progress) return result;
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType != typeof(string) && p.ParameterType != typeof(GUIContent)) continue;
                // Never include value/text arguments of TextField, PasswordField, object selectors or control IDs.
                var label = p.Name == "label" || p.Name == "prefixLabel" || p.Name == "leftLabel" || p.Name == "rightLabel";
                var content = p.Name == "content" && (method.Name.Contains("Foldout") || method.Name.Contains("Button") || method.Name == "Toggle" || method.Name == "Box" || method.Name == "Label" || method.Name == "HelpBox");
                var text = p.Name == "text" && (owner == typeof(GUILayout) || owner == typeof(GUI)) &&
                    (method.Name == "Button" || method.Name == "RepeatButton" || method.Name == "Toggle" || method.Name == "Label" || method.Name == "Box");
                var message = method.Name == "HelpBox" && p.Name == "message";
                // Only the file picker title is display text; directory and extension arguments stay unchanged.
                var pickerTitle = filePicker && p.Name == "title";
                var progressText = progressBar && (p.Name == "title" || p.Name == "info") ||
                    progress && (p.Name == "name" || p.Name == "description");
                if (label || content || text || message || dialog || pickerTitle || progressText) result.Add(i);
            }
            return result;
        }

        private static MethodInfo PropertyReplacement(MethodInfo original)
        {
            if (original.Name != "PropertyField") return null;
            var helper = original.DeclaringType == typeof(EditorGUILayout) ? "LayoutProperty" :
                original.DeclaringType == typeof(EditorGUI) ? "RectProperty" : null;
            if (helper == null) return null;
            return AccessTools.Method(typeof(DisplayCalls), helper, original.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        internal static string ComponentText(string source) => TranslationCatalog.ComponentText(source);
        internal static string AccountText(string source) => TranslationCatalog.AccountText(source);
        internal static string PanelText(string source) => TranslationCatalog.PanelText(source);
        internal static GUIContent PanelContent(GUIContent original)
        {
            if (original == null) return null;
            var text = PanelText(original.text);
            var tooltip = PanelText(original.tooltip);
            return text == original.text && tooltip == original.tooltip ? original : new GUIContent(original) { text = text, tooltip = tooltip };
        }
        private static GUIContent Translate(GUIContent original, bool account)
        {
            if (original == null) return null;
            var text = account ? AccountText(original.text) : ComponentText(original.text);
            var tooltip = account ? AccountText(original.tooltip) : ComponentText(original.tooltip);
            return text == original.text && tooltip == original.tooltip ? original : new GUIContent(original) { text = text, tooltip = tooltip };
        }
        internal static GUIContent ComponentContent(GUIContent original) => Translate(original, false);
        internal static GUIContent AccountContent(GUIContent original) => Translate(original, true);
        private static GUIContent PropertyLabel(SerializedProperty property, GUIContent label)
        {
            if (!VrcSdkLocalization.IsApplied || property == null || property.propertyPath == "m_Script") return label;
            return WorldComponentPatches.IsWorldTarget(property.serializedObject.targetObject)
                ? WorldComponentPatches.PropertyContent(property, label)
                : ComponentContent(label ?? new GUIContent(property.displayName, property.tooltip));
        }
        // Matching original signatures keeps SDK call sites and property behavior intact, including curves and lists.
        private static bool LayoutProperty(SerializedProperty p, GUILayoutOption[] options) => EditorGUILayout.PropertyField(p, PropertyLabel(p, null), options);
        private static bool LayoutProperty(SerializedProperty p, GUIContent label, GUILayoutOption[] options) => EditorGUILayout.PropertyField(p, PropertyLabel(p, label), options);
        private static bool LayoutProperty(SerializedProperty p, bool children, GUILayoutOption[] options) => EditorGUILayout.PropertyField(p, PropertyLabel(p, null), children, options);
        private static bool LayoutProperty(SerializedProperty p, GUIContent label, bool children, GUILayoutOption[] options) => EditorGUILayout.PropertyField(p, PropertyLabel(p, label), children, options);
        private static bool RectProperty(Rect r, SerializedProperty p) => EditorGUI.PropertyField(r, p, PropertyLabel(p, null));
        private static bool RectProperty(Rect r, SerializedProperty p, bool children) => EditorGUI.PropertyField(r, p, PropertyLabel(p, null), children);
        private static bool RectProperty(Rect r, SerializedProperty p, GUIContent label) => EditorGUI.PropertyField(r, p, PropertyLabel(p, label));
        private static bool RectProperty(Rect r, SerializedProperty p, GUIContent label, bool children) => EditorGUI.PropertyField(r, p, PropertyLabel(p, label), children);
    }
}
