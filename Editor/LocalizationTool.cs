// LGC_MAFury_中文拓展 v2.0.0
// Shared window and lifecycle. Each optional plugin owns its translations, state and Harmony patches.
using System;
using UnityEditor;
using UnityEngine;

namespace LgcChina.MaFuryLocalization
{
    [InitializeOnLoad]
    internal static class LocalizationTool
    {
        internal const string Version = "2.0.0";
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
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void RunModule(Action action, string name)
        {
            try { action(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[LGC_MAFury_中文拓展 v{Version}] {name} 汉化已跳过：{(e.InnerException ?? e).Message}");
            }
        }

        private static void RestoreAll()
        {
            try { ModularAvatar.MaLocalization.SetOff(); } catch { }
            try { Vrcfury.VrcfuryLocalization.SetOff(); } catch { }
        }
    }

    internal sealed class LocalizationWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("LGC/LGC_MAFury_中文拓展 v" + LocalizationTool.Version)]
        private static void Open() => GetWindow<LocalizationWindow>("LGC_MAFury_中文拓展 v" + LocalizationTool.Version);

        private void OnEnable()
        {
            titleContent = new GUIContent("LGC_MAFury_中文拓展 v" + LocalizationTool.Version);
            minSize = new Vector2(580, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LGC_MAFury_中文拓展 v" + LocalizationTool.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("自动检测已安装的 MA 和 VRCFury，分别启用对应汉化。未安装的插件会跳过。"
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