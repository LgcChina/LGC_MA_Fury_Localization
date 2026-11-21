// LGC_VRCMAFury_中文拓展 v2.3.7 — VRChat SDK adapter
// Independently detect the SDK account panel and VRCHeadChop; missing optional interfaces are skipped.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class VrcSdkLocalization
    {
        private const string HeadChopTypeName = "VRC.SDK3.Avatars.Components.VRCHeadChop";
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.headchop";
        private const string EnabledKey = LocalizationTool.PackageName + ".vrcsdk.enabled";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly List<string> notes = new List<string>();
        private static Type headChopType;
        internal static bool IsInstalled { get; private set; }
        internal static bool IsApplied { get; private set; }
        internal static bool HeadChopApplied { get; private set; }
        internal static string SdkVersion { get; private set; } = "未知";
        internal static string Status { get; private set; } = "等待接入 VRChat SDK";
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
            headChopType = FindType(HeadChopTypeName);
            var panel = FindType("VRCSdkControlPanel");
            IsInstalled = headChopType != null || panel != null;
            if (!IsInstalled)
            {
                SdkVersion = "未安装";
                Status = "未检测到 SDK 登录面板或 VRCHeadChop，已跳过";
                return;
            }
            try
            {
                SdkVersion = PackageInfo.FindForAssembly((panel ?? headChopType).Assembly)?.version ?? "未知";
                if (!SessionState.GetBool(EnabledKey, true))
                {
                    Status = "已停用 SDK 汉化，组件、登录与设置界面恢复原文";
                    return;
                }
                TranslationCatalog.Load();
                IsApplied = true;
                ApplyHeadChop();
                var components = false;
                try { components = ComponentPatches.Apply(); }
                catch (Exception e) { ComponentPatches.SetOff(); Note("SDK 常用组件接口无法接入，已跳过：" + (e.InnerException ?? e).Message); }
                var account = false;
                try { account = AccountPatches.Apply(panel); }
                catch (Exception e)
                {
                    AccountPatches.SetOff();
                    Note("SDK 登录界面无法接入，已跳过：" + (e.InnerException ?? e).Message);
                }
                var upload = false;
                try { upload = PanelPatches.Apply(panel); }
                catch (Exception e) { PanelPatches.SetOff(); Note("SDK 上传页入口无法接入：" + e.GetBaseException().Message); }
                IsApplied = HeadChopApplied || account || components || upload;
                Status = IsApplied ? "已启用 SDK 中文：" +
                    (HeadChopApplied ? "VRCHeadChop" : "") +
                    (HeadChopApplied && account ? "、" : "") + (account ? "登录页面" + (AccountPatches.SettingsMethodCount > 0 ? " / 设置" : "") : "") + (components ? ((HeadChopApplied || account) ? "、" : "") + "组件字段（" + ComponentPatches.MethodCount + " 个绘制入口）" : "") + (upload ? "、上传页面" : "")
                    : "当前 SDK 可选接口无法接入，已保留原版";
                Note("SDK 窗口重新打开即可查看；组件请重新选中对象。设置页已接入 " + AccountPatches.SettingsMethodCount + " 个绘制入口；缺少接口时跳过。");
                AccountPatches.RefreshTabs();
            }
            catch (Exception e)
            {
                SetOff();
                Status = "SDK 词典或界面接口无法接入，已保留原版";
                Note((e.InnerException ?? e).Message);
            }
            finally { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
        }

        private static void ApplyHeadChop()
        {
            if (headChopType == null) { Note("SDK 未包含 VRCHeadChop，已跳过组件汉化。"); return; }
            try
            {
                var attributes = typeof(Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes", false);
                var find = attributes == null ? null : AccessTools.Method(attributes,
                    "FindCustomEditorTypeByType", new[] { typeof(Type), typeof(bool) });
                if (find == null || find.ReturnType != typeof(Type))
                {
                    Note("当前 Unity 缺少检查器接入接口，已跳过 VRCHeadChop。");
                    return;
                }
                Harmony.Patch(find, postfix: new HarmonyMethod(typeof(VrcSdkLocalization), nameof(EditorTypePostfix)));
                HeadChopApplied = true;
                var selected = find.Invoke(null, new object[] { headChopType, false }) as Type;
                if (selected != typeof(HeadChopEditor))
                {
                    HeadChopApplied = false;
                    Harmony.UnpatchAll(HarmonyId);
                    Note("VRCHeadChop 已有专用检查器，已保留原有界面。");
                    return;
                }
                var title = AccessTools.Method(typeof(ObjectNames), "GetInspectorTitle",
                    new[] { typeof(UnityEngine.Object), typeof(bool) });
                if (title != null)
                {
                    try { Harmony.Patch(title, postfix: new HarmonyMethod(typeof(VrcSdkLocalization), nameof(TitlePostfix))); }
                    catch { Note("组件标题接口不兼容，字段汉化仍可使用。"); }
                }
            }
            catch (Exception e)
            {
                HeadChopApplied = false;
                try { Harmony.UnpatchAll(HarmonyId); } catch { }
                Note("VRCHeadChop 接口不兼容，已跳过：" + (e.InnerException ?? e).Message);
            }
        }

        internal static bool IsHeadChop(UnityEngine.Object target) => target != null && target.GetType() == headChopType;

        private static void EditorTypePostfix(Type __0, ref Type __result)
        {
            if (IsApplied && HeadChopApplied && __0 == headChopType &&
                (__result == null || __result.FullName == "UnityEditor.GenericInspector"))
                __result = typeof(HeadChopEditor);
        }

        private static void TitlePostfix(UnityEngine.Object __0, ref string __result)
        {
            if (IsApplied && HeadChopApplied && IsHeadChop(__0))
            {
                var title = TranslationCatalog.Text("VRC Head Chop");
                __result = __result != null && __result.EndsWith(" (Script)", StringComparison.Ordinal)
                    ? title + " (Script)" : title;
            }
        }

        internal static void SetOff()
        {
            IsApplied = false;
            HeadChopApplied = false;
            PanelPatches.SetOff();
            AccountPatches.SetOff();
            ComponentPatches.SetOff();
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
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
                Status = "SDK 词典读取失败，保留当前显示状态";
                notes.Clear();
                Note((e.InnerException ?? e).Message);
                return;
            }
            Initialize();
        }
    }
}
