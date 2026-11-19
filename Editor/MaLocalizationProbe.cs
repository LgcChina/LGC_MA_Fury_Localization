// LGC MA Localization Probe v1.0.0
// Independent editor localization, minimum MA 1.10.0. Missing version-specific interfaces are skipped.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace LgcChina.MaLocalizationProbe
{
    [InitializeOnLoad]
    internal static class Probe
    {
        internal const string Version = "1.0.0";
        internal const string PackageName = "com.lgcchina.ma-localization-probe";
        private const string EnabledKey = PackageName + ".enabled";
        private static object localizer;
        private static FieldInfo loaderField;
        private static MethodInfo loadMethod;
        private static Func<List<(string, Func<string, string>)>> originalLoader, wrappedLoader;
        private static bool labelsApplied, warned;
        private static readonly List<string> notes = new List<string>();
        internal static bool IsApplied { get; private set; }
        internal static string MaVersion { get; private set; } = "尚未读取";
        internal static string Status { get; private set; } = "等待初始化";
        internal static string Details => string.Join("\n", notes);
        internal static string Language
        {
            get
            {
                try
                {
                    var type = Type.GetType("nadena.dev.ndmf.localization.LanguagePrefs, nadena.dev.ndmf");
                    return type?.GetProperty("Language", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string ?? "未提供语言接口";
                }
                catch { return "未提供语言接口"; }
            }
        }

        static Probe()
        {
            EditorApplication.delayCall += Initialize;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreBeforeReload;
            EditorApplication.quitting += RestoreBeforeReload;
        }

        internal static void Initialize()
        {
            notes.Clear();
            try
            {
                TranslationCatalog.Load();
                var assembly = FindMaEditorAssembly();
                if (assembly == null)
                {
                    SetOff(); Status = "未发现 MA 编辑器程序，已跳过"; return;
                }
                var package = PackageInfo.FindForAssembly(assembly);
                MaVersion = package?.version ?? "未知";
                if (!AtLeastMinimum(MaVersion))
                {
                    SetOff(); Status = "本版支持范围从 MA 1.10.0 开始，当前版本已跳过"; return;
                }
                if (!SessionState.GetBool(EnabledKey, true))
                {
                    SetOff(); Status = "已停用，重新打开界面后恢复原版显示"; return;
                }

                labelsApplied = ApplyLabels(assembly);
                if (!labelsApplied) Note("当前版本未提供可接入的翻译加载器，已保留原版词条。");
                try { MenuProbe.SetApplied(true); }
                catch
                {
                    try { MenuProbe.SetApplied(false); } catch { }
                    Note("当前菜单接口无法接入，已跳过菜单部分。");
                }
                try { DisplayPatches.SetApplied(true); }
                catch
                {
                    try { DisplayPatches.SetApplied(false); } catch { }
                    Note("当前额外界面接口无法接入，已跳过该部分。");
                }
                IsApplied = labelsApplied || MenuProbe.IsApplied || DisplayPatches.IsApplied;
                Status = IsApplied ? (notes.Count == 0 ? "中文已启用" : "中文已启用，部分旧版接口已跳过") : "当前版本暂无可接入界面，已保留原版显示";
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            catch (Exception e)
            {
                SetOff();
                Status = "当前版本未能接入，已保留原版显示";
                Note((e.InnerException ?? e).Message);
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"[LGC MA 汉化 v{Version}] {Status}：{(e.InnerException ?? e).Message}");
                }
            }
        }

        internal static void ReloadTranslations()
        {
            try
            {
                TranslationCatalog.Load(true);
            }
            catch (Exception e)
            {
                notes.Clear();
                Status = IsApplied ? "词典读取失败，保留当前汉化" : "词典读取失败，当前仍为停用状态";
                Note((e.InnerException ?? e).Message);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                return;
            }

            // Rebuild transpilers as well as lookups: some translated strings are embedded in patched IL.
            SetOff();
            Initialize();
            Note("已重新读取 JSON 词典。请重新选中对象，并关闭后重开相关菜单或窗口。");
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        internal static Assembly FindMaEditorAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "nadena.dev.modular-avatar.core.editor") return assembly;
            return null;
        }

        private static bool AtLeastMinimum(string version)
        {
            var stable = (version ?? string.Empty).Split('-')[0].Split('+')[0];
            if (!System.Version.TryParse(stable, out var parsed)) return false;
            return parsed >= new System.Version(1, 10, 0);
        }

        private static bool ApplyLabels(Assembly assembly)
        {
            try
            {
                if (localizer == null)
                {
                    var type = assembly.GetType("nadena.dev.modular_avatar.core.editor.Localization", false);
                    var property = type?.GetProperty("L", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    var instance = property?.GetValue(null) ?? type?.GetField("L",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                    if (instance == null) return false;
                    var field = instance.GetType().GetField("_localizationLoader", BindingFlags.Instance | BindingFlags.NonPublic);
                    var method = instance.GetType().GetMethod("LoadLocalizations", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    var loader = field?.GetValue(instance) as Func<List<(string, Func<string, string>)>>;
                    if (loader == null || method == null) return false;
                    localizer = instance; loaderField = field; loadMethod = method; originalLoader = loader;
                    wrappedLoader = LoadWithOverrides;
                }
                var current = loaderField.GetValue(localizer);
                if (!ReferenceEquals(current, originalLoader) && !ReferenceEquals(current, wrappedLoader))
                { Note("翻译加载器已由其他扩展接管，本包跳过字段覆盖。"); return false; }
                loaderField.SetValue(localizer, wrappedLoader);
                loadMethod.Invoke(localizer, null);
                return true;
            }
            catch
            {
                RestoreLabels();
                return false;
            }
        }

        private static List<(string, Func<string, string>)> LoadWithOverrides()
        {
            var result = new List<(string, Func<string, string>)>();
            foreach (var entry in originalLoader())
            {
                var originalLookup = entry.Item2;
                if (originalLookup == null) { result.Add(entry); continue; }
                result.Add((entry.Item1, key =>
                {
                    var original = originalLookup(key);
                    // Missing old-version keys stay missing. A changed format signature keeps the old original text.
                    if (original != null && key != null && TranslationCatalog.Entries.TryGetValue(key, out var chinese) &&
                        TranslationCatalog.FormatsMatch(original, chinese)) return chinese;
                    return original;
                }));
            }
            return result;
        }

        private static void RestoreLabels()
        {
            if (localizer == null || loaderField == null || originalLoader == null) return;
            try
            {
                if (ReferenceEquals(loaderField.GetValue(localizer), wrappedLoader))
                { loaderField.SetValue(localizer, originalLoader); loadMethod?.Invoke(localizer, null); }
            }
            catch { /* Domain reload also discards this instance. */ }
            labelsApplied = false;
        }

        private static void SetOff()
        {
            try { DisplayPatches.SetApplied(false); } catch { }
            try { MenuProbe.SetApplied(false); } catch { }
            RestoreLabels();
            IsApplied = false;
        }

        internal static void Note(string message)
        {
            if (!string.IsNullOrEmpty(message) && !notes.Contains(message)) notes.Add(message);
        }
        internal static void Toggle()
        {
            SessionState.SetBool(EnabledKey, !IsApplied);
            Initialize();
        }
        private static void RestoreBeforeReload() { SetOff(); }
    }

    internal sealed class ProbeWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("LGC/MA 汉化 v" + Probe.Version)]
        private static void Open() => GetWindow<ProbeWindow>("MA 汉化 v" + Probe.Version);
        private void OnEnable()
        {
            titleContent = new GUIContent("MA 汉化 v" + Probe.Version);
            minSize = new Vector2(550, 360);
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("MA 独立汉化 v" + Probe.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Probe.Status, MessageType.Info);
            EditorGUILayout.LabelField("当前 MA 版本", Probe.MaVersion);
            EditorGUILayout.LabelField("最低支持范围", "MA 1.10.0，具体旧版效果待手动确认");
            EditorGUILayout.LabelField("原语言设置", Probe.Language);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("汉化覆盖原有字段、提示、添加组件名称、组件标题和上下文菜单。"
                + "旧版没有的选项保持原样。切换状态后请重新打开菜单和相关窗口。", MessageType.None);
            if (GUILayout.Button(Probe.IsApplied ? "停用并恢复原版显示" : "启用中文")) Probe.Toggle();
            if (GUILayout.Button("重新加载词典并连接 MA")) Probe.ReloadTranslations();
            EditorGUILayout.HelpBox("修改 zh-Hans.json 并保存后，点击上方按钮重新加载，再重开相关界面。", MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!string.IsNullOrEmpty(Probe.Details)) EditorGUILayout.HelpBox(Probe.Details, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
