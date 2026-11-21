// LGC_VRCMAFury_中文拓展 v2.3.7 — VRChat SDK adapter
// Translate explicit account and settings render methods; preserve credentials, settings values and SDK callbacks.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ILGenerator = System.Reflection.Emit.ILGenerator;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class AccountPatches
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.account";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly List<TabEdit> tabs = new List<TabEdit>();
        private static Type panelType;
        internal static bool IsApplied { get; private set; }
        internal static int SettingsMethodCount { get; private set; }

        internal static bool Apply(Type panel)
        {
            SetOff();
            panelType = panel;
            if (panelType == null) return false;
            var count = 0;
            foreach (var name in new[] { "AccountWindowGUI", "OnCreatorStatusGUI", "OnTwoFactorAuthenticationGUI", "ShowAccount", "ShowContentPublishPermissionsDialog" })
            {
                // This allowlist excludes SignIn, AttemptLogin, API callbacks, status comparisons and credential storage.
                try
                {
                    var method = AccessTools.DeclaredMethod(panelType, name);
                    if (method == null) { VrcSdkLocalization.Note("SDK 登录界面入口不存在，已跳过：" + name); continue; }
                    Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(AccountPatches), nameof(DisplayTranspiler)));
                    count++;
                }
                catch { VrcSdkLocalization.Note("SDK 登录界面入口不兼容，已跳过：" + name); }
            }
            // Settings methods use call-site translation only: never rewrite preference keys or setting values.
            PatchSettingsMethod(panelType, "ShowSettings");
            PatchSettingsMethod(panelType, "OnVRCInstallPathGUI");
            var content = AccessTools.DeclaredMethod(panelType, "ShowContent", Type.EmptyTypes);
            if (content != null)
            {
                try { Harmony.Patch(content, transpiler: new HarmonyMethod(typeof(AccountPatches), nameof(ContentTranspiler))); }
                catch (Exception e) { VrcSdkLocalization.Note("SDK 内容管理按钮入口已跳过：" + e.GetBaseException().Message); }
            }
            Type avatarBuilder = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { avatarBuilder = assembly.GetType("VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder", false); }
                catch { continue; }
                if (avatarBuilder != null) break;
            }
            if (avatarBuilder != null) PatchSettingsMethod(avatarBuilder, "ShowSettingsOptions");
            count += SettingsMethodCount;
            // Locate only the compiler-generated display callback containing this exact SDK warning.
            var renderingTypes = new List<Type> { panelType };
            renderingTypes.AddRange(panelType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            foreach (var type in renderingTypes)
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsAbstract || method.ContainsGenericParameters || !method.Name.Contains("<")) continue;
                try
                {
                    var warning = false;
                    foreach (var instruction in PatchProcessor.GetOriginalInstructions(method))
                        if (instruction.opcode == OpCodes.Ldstr && instruction.operand as string == "Unity Application is running ...\nStop it to access the Control Panel") { warning = true; break; }
                    if (warning) Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(AccountPatches), nameof(DisplayTranspiler)));
                }
                catch { /* Unknown compiler-generated callbacks keep their original code. */ }
            }
            IsApplied = count > 0;
            if (!IsApplied) return false;
            var createTabs = AccessTools.DeclaredMethod(panelType, "CreateTabs", Type.EmptyTypes);
            if (createTabs != null)
            {
                try { Harmony.Patch(createTabs, postfix: new HarmonyMethod(typeof(AccountPatches), nameof(TabsPostfix))); }
                catch { VrcSdkLocalization.Note("SDK 登录页签接口不兼容，保留原文。"); }
            }
            // The exact title limits this optional dialog hook to SDK sign-in errors.
            var dialog = AccessTools.Method(typeof(EditorUtility), "DisplayDialog",
                new[] { typeof(string), typeof(string), typeof(string) });
            if (dialog != null)
            {
                try { Harmony.Patch(dialog, prefix: new HarmonyMethod(typeof(AccountPatches), nameof(DialogPrefix))); }
                catch { VrcSdkLocalization.Note("SDK 登录错误弹窗入口不兼容，保留原文。"); }
            }
            RefreshTabs();
            return true;
        }

        private static void PatchSettingsMethod(Type type, string name)
        {
            try
            {
                var method = AccessTools.DeclaredMethod(type, name, Type.EmptyTypes);
                if (method == null || method.GetMethodBody() == null)
                { VrcSdkLocalization.Note("SDK 设置页入口不存在，已跳过：" + name); return; }
                Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(AccountPatches), nameof(SettingsTranspiler)));
                SettingsMethodCount++;
            }
            catch { VrcSdkLocalization.Note("SDK 设置页入口不兼容，已跳过：" + name); }
        }

        private static IEnumerable<CodeInstruction> ContentTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            // Translate only these two fixed button captions; URLs, fetches and user content stay unchanged.
            foreach (var original in instructions)
            {
                var instruction = new CodeInstruction(original);
                if (instruction.opcode == OpCodes.Ldstr && (instruction.operand as string == "Worlds" || instruction.operand as string == "Avatars"))
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TranslationCatalog), nameof(TranslationCatalog.ContentText)));
                }
                else yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> SettingsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => DisplayCalls.Rewrite(instructions, generator, true);

        private static IEnumerable<CodeInstruction> DisplayTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var translate = AccessTools.Method(typeof(TranslationCatalog), nameof(TranslationCatalog.AccountText));
            foreach (var instruction in DisplayCalls.Rewrite(instructions, generator, true))
            {
                yield return instruction;
                // Keep accepted fixed captions (including permission values) without touching status comparisons or control IDs.
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text &&
                    text != "Connected" && text != "input_user" && text != "input_pass" &&
                    text != "Authentication Code Field" && !text.StartsWith("https://", StringComparison.Ordinal) &&
                    TranslationCatalog.HasAccountLiteral(text))
                    yield return new CodeInstruction(OpCodes.Call, translate);
            }
        }

        private static void DialogPrefix(ref string __0, ref string __1, ref string __2)
        {
            if (!IsApplied || !VrcSdkLocalization.IsApplied || __0 != "Error logging in") return;
            __0 = TranslationCatalog.AccountText(__0);
            __1 = TranslationCatalog.AccountText(__1);
            __2 = TranslationCatalog.AccountText(__2);
        }

        private static void TabsPostfix(EditorWindow __instance)
        {
            try { if (IsApplied && VrcSdkLocalization.IsApplied) TranslateTab(__instance); }
            catch { VrcSdkLocalization.Note("SDK 登录页签无法更新，保留当前显示。"); }
        }

        internal static void RefreshTabs()
        {
            if (!IsApplied || panelType == null) return;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window != null && window.GetType() == panelType) TranslateTab(window);
        }

        private static void TranslateTab(EditorWindow window)
        {
            // Only four named toolbar buttons are queried; login inputs and user data are never traversed.
            foreach (var name in new[] { "tab-authentication", "tab-builder", "tab-content-manager", "tab-settings" })
            {
                var button = window.rootVisualElement.Q<Button>(name);
                if (button == null) continue;
                var seen = false;
                foreach (var edit in tabs)
                    if (edit.Button.TryGetTarget(out var existing) && existing == button) { seen = true; break; }
                if (seen) continue;
                var original = button.text;
                var source = name == "tab-authentication" ? "Authentication" : name == "tab-builder" ? "Builder" :
                    name == "tab-content-manager" ? "Content Manager" : "Settings";
                var translated = TranslationCatalog.AccountText(source);
                if (translated == original) continue;
                tabs.Add(new TabEdit { Button = new WeakReference<Button>(button), Original = original, Translated = translated });
                button.text = translated;
            }
            window.Repaint();
        }

        internal static void SetOff()
        {
            IsApplied = false;
            SettingsMethodCount = 0;
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            foreach (var edit in tabs)
                if (edit.Button.TryGetTarget(out var button) && button.text == edit.Translated)
                    button.text = edit.Original;
            tabs.Clear();
        }

        private sealed class TabEdit
        {
            internal WeakReference<Button> Button;
            internal string Original, Translated;
        }
    }
}
