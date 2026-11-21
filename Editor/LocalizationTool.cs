// LGC_VRCMAFury_中文拓展 v2.3.7
// Shared window and lifecycle. Each optional plugin owns its translations, state and Harmony patches.
using System;
using UnityEditor;
using UnityEngine;

namespace LgcChina.MaFuryLocalization
{
    [InitializeOnLoad]
    internal static class LocalizationTool
    {
        internal const string Version = "2.3.7";
        internal const string PackageName = "com.lgcchina.ma-fury-localization";

        static LocalizationTool()
        {
            EditorApplication.delayCall += InitializeAll;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
            EditorApplication.quitting += RestoreAll;
        }

        internal static void InitializeAll()
        {
            RunModule(ModularAvatar.MaLocalization.Initialize, "MA");
            RunModule(Vrcfury.VrcfuryLocalization.Initialize, "VRCFury");
            RunModule(VrcSdk.VrcSdkLocalization.Initialize, "VRChat SDK");
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void RunModule(Action action, string name)
        {
            try { action(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[LGC_VRCMAFury_中文拓展 v{Version}] {name} 汉化已跳过：{(e.InnerException ?? e).Message}");
            }
        }

        private static void RestoreAll()
        {
            try { ModularAvatar.MaLocalization.SetOff(); } catch { }
            try { Vrcfury.VrcfuryLocalization.SetOff(); } catch { }
            try { VrcSdk.VrcSdkLocalization.SetOff(); } catch { }
        }
    }

    internal sealed class LocalizationWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("LGC/LGC_VRCMAFury_中文拓展 v" + LocalizationTool.Version)]
        private static void Open() => GetWindow<LocalizationWindow>("LGC_VRCMAFury_中文拓展 v" + LocalizationTool.Version);

        private void OnEnable()
        {
            titleContent = new GUIContent("LGC_VRCMAFury_中文拓展 v" + LocalizationTool.Version);
            minSize = new Vector2(580, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LGC_VRCMAFury_中文拓展 v" + LocalizationTool.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("自动检测已安装的 MA、VRCFury 与 SDK 试点组件，分别启用对应汉化。未安装的插件会跳过。"
                + "保存词典后点击对应的重载按钮，再重新选中对象或重开相关界面。", MessageType.None);
            if (GUILayout.Button("重新检测已安装插件")) LocalizationTool.InitializeAll();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawModule("Modular Avatar", ModularAvatar.MaLocalization.MaVersion,
                ModularAvatar.MaLocalization.Status, ModularAvatar.MaLocalization.Details,
                ModularAvatar.MaLocalization.IsInstalled, ModularAvatar.MaLocalization.IsApplied,
                ModularAvatar.MaLocalization.Toggle, ModularAvatar.MaLocalization.ReloadTranslations,
                "MA", "ma.zh-Hans.json");
            EditorGUILayout.LabelField("MA 最低适配目标：1.10.0；不兼容的可选界面会跳过。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            DrawModule("VRCFury", Vrcfury.VrcfuryLocalization.VrcfuryVersion,
                Vrcfury.VrcfuryLocalization.Status, Vrcfury.VrcfuryLocalization.Details,
                Vrcfury.VrcfuryLocalization.IsInstalled, Vrcfury.VrcfuryLocalization.IsApplied,
                Vrcfury.VrcfuryLocalization.Toggle, Vrcfury.VrcfuryLocalization.Reload,
                "VRCFury", "vrcfury.zh-Hans.json");
            EditorGUILayout.HelpBox("VRCFury 已扩展至组件、动作、菜单及 SPS 面板。组件与动作名称保留英文便于搜索。"
                + "新建或展开界面后可稍等片刻；未匹配的词条保持原文。", MessageType.None);

            EditorGUILayout.Space();
            DrawModule("VRChat SDK · 组件、登录、上传与设置", VrcSdk.VrcSdkLocalization.SdkVersion,
                VrcSdk.VrcSdkLocalization.Status, VrcSdk.VrcSdkLocalization.Details,
                VrcSdk.VrcSdkLocalization.IsInstalled, VrcSdk.VrcSdkLocalization.IsApplied,
                VrcSdk.VrcSdkLocalization.Toggle, VrcSdk.VrcSdkLocalization.Reload,
                "SDK", "vrcsdk.zh-Hans.json");
            EditorGUILayout.HelpBox("SDK 已包含常用组件、可播放层、眼球运动 / 面捕提示、登录页面、上传页面及设置页正文。"
                + "SDK 窗口可关闭后重开；组件可重新选中，未匹配文字保持原文。", MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawModule(string title, string version, string status, string details,
            bool installed, bool applied, Action toggle, Action reload, string shortName, string dictionary)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("插件版本", version);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUI.BeginDisabledGroup(!installed);
            if (GUILayout.Button(applied ? "停用 " + shortName + " 并恢复原版显示" : "启用 " + shortName + " 中文")) toggle();
            if (GUILayout.Button("重新加载 " + shortName + " 词典")) reload();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.LabelField("词典：" + dictionary, EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(details)) EditorGUILayout.HelpBox(details, MessageType.None);
            EditorGUILayout.EndVertical();
        }
    }
}