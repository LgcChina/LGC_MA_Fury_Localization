// LGC_VRCMAFury_中文拓展 v2.3.7 — VRChat SDK adapter
// Load an immutable replacement snapshot; malformed JSON keeps the last successful dictionaries.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class TranslationCatalog
    {
        private sealed class Snapshot
        {
            internal readonly Dictionary<string, string> Entries, Account, ComponentEnums, ComponentTitles, Dialogs, ComponentMenus;
            internal Dictionary<string, string> World = new Dictionary<string, string>();
            internal Dictionary<string, string> Panel = new Dictionary<string, string>();
            internal Dictionary<string, string> PanelFragments = new Dictionary<string, string>();
            internal TextTemplate[] FragmentTemplates = Array.Empty<TextTemplate>();
            internal TextTemplate[] Templates = Array.Empty<TextTemplate>();
            internal Snapshot(Dictionary<string, string> entries, Dictionary<string, string> account, Dictionary<string, string> componentEnums, Dictionary<string, string> componentTitles, Dictionary<string, string> dialogs, Dictionary<string, string> componentMenus)
            {
                Entries = entries; Account = account; ComponentEnums = componentEnums; ComponentTitles = componentTitles; Dialogs = dialogs; ComponentMenus = componentMenus;
            }
        }
        private static Snapshot active = new Snapshot(new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>());
        private static bool loaded;

        internal static void Load(bool force = false)
        {
            if (loaded && !force) return;
            var package = PackageInfo.FindForAssembly(typeof(VrcSdkLocalization).Assembly);
            var root = package != null ? package.resolvedPath : "Packages/" + LocalizationTool.PackageName;
            var json = File.ReadAllText(Path.Combine(root, "Editor/Localization/vrcsdk.zh-Hans.json"));
            var data = JsonUtility.FromJson<Catalog>(json);
            if (data?.entries == null) throw new InvalidOperationException("SDK 词典格式错误，请检查 entries 字段。");
            var next = new Snapshot(ReadPairs(data.entries), ReadPairs(data.accountEntries), ReadPairs(data.componentEnums), ReadPairs(data.componentTitles), ReadPairs(data.dialogs), ReadPairs(data.componentMenus));
            next.World = ReadPairs(data.worldEntries);
            next.Panel = ReadPairs(data.panelEntries);
            next.PanelFragments = ReadPairs(data.panelFragments);
            next.FragmentTemplates = next.PanelFragments.Where(pair => Regex.IsMatch(pair.Key, @"\{\d+\}"))
                .Select(pair => new TextTemplate(pair.Key, pair.Value)).OrderByDescending(t => t.Specificity).ToArray();
            var templates = new List<TextTemplate>();
            foreach (var pair in data.panelTemplates ?? Array.Empty<Pair>())
                if (pair != null && !string.IsNullOrEmpty(pair.key) && pair.value != null)
                    templates.Add(new TextTemplate(pair.key, pair.value));
            // Prefer complete warnings over short metric prefixes with a trailing wildcard.
            next.Templates = templates.OrderByDescending(t => t.Specificity).ToArray();
            active = next;
            loaded = true;
        }

        private static Dictionary<string, string> ReadPairs(Pair[] pairs)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in pairs ?? Array.Empty<Pair>())
                if (pair != null && !string.IsNullOrEmpty(pair.key) && pair.value != null)
                    result[pair.key] = pair.value;
            return result;
        }

        internal static string Text(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied) return source;
            return active.Entries.TryGetValue(source, out var text) ? text : source;
        }

        internal static bool HasAccountLiteral(string source) => source != null && active.Account.ContainsKey(source);

        internal static string AccountText(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied || !AccountPatches.IsApplied) return source;
            if (active.Account.TryGetValue(source, out var text)) return text;
            if (source.StartsWith("Signing in as ", StringComparison.Ordinal)) return "正在登录：" + source.Substring("Signing in as ".Length);
            if (source.StartsWith("Error in authenticating: ", StringComparison.Ordinal)) return "身份验证错误：" + source.Substring("Error in authenticating: ".Length);
            if (source.StartsWith("Connected to ", StringComparison.Ordinal)) return "已连接到 " + source.Substring("Connected to ".Length);
            if (source.StartsWith("Wrong Unity version. Please use ", StringComparison.Ordinal)) return "Unity 版本错误，请使用 " + source.Substring("Wrong Unity version. Please use ".Length);
            return source;
        }

        internal static string ComponentText(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied) return source;
            return active.Entries.TryGetValue(source, out var text) ? text : source;
        }

        internal static string WorldComponentText(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied) return source;
            return active.World.TryGetValue(source, out var text) ? text : ComponentText(source);
        }

        internal static string ComponentEnum(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied) return source;
            return active.ComponentEnums.TryGetValue(source, out var text) ? text : ComponentText(source);
        }

        internal static string ComponentTitle(string typeName)
        {
            if (typeName == null || !VrcSdkLocalization.IsApplied) return null;
            return active.ComponentTitles.TryGetValue(typeName, out var text) ? text : null;
        }

        internal static string ComponentMenu(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied) return source;
            return active.ComponentMenus.TryGetValue(source, out var text) ? text : source;
        }

        internal static string ContentText(string source)
        {
            if (!VrcSdkLocalization.IsApplied || !AccountPatches.IsApplied) return source;
            if (source != "Worlds" && source != "Avatars") return source;
            return active.Account.TryGetValue(source, out var translated) ? translated : source;
        }

        internal static string PanelText(string source)
        {
            if (source == null || !VrcSdkLocalization.IsApplied || !PanelPatches.IsApplied) return source;
            if (active.Panel.TryGetValue(source, out var text)) return text;
            foreach (var template in active.Templates)
                if (template.TryTranslate(source, out text)) return text;
            return source;
        }

        private sealed class TextTemplate
        {
            private readonly Regex regex;
            private readonly string prefix, translation;
            internal int Specificity { get; }
            internal TextTemplate(string key, string value)
            {
                var placeholders = Regex.Matches(key, @"\{(\d+)\}");
                Specificity = Regex.Replace(key, @"\{\d+\}", "").Length;
                var pattern = new StringBuilder(@"\A");
                var offset = 0;
                prefix = placeholders.Count == 0 ? key : key.Substring(0, placeholders[0].Index);
                foreach (Match token in placeholders)
                {
                    pattern.Append(Regex.Escape(key.Substring(offset, token.Index - offset)));
                    pattern.Append("(?<p" + token.Groups[1].Value + ">.*?)");
                    offset = token.Index + token.Length;
                }
                pattern.Append(Regex.Escape(key.Substring(offset))).Append(@"\z");
                regex = new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                translation = value;
            }
            internal bool TryTranslate(string source, out string text, int depth = 0)
            {
                text = source;
                if (!source.StartsWith(prefix, StringComparison.Ordinal)) return false;
                try
                {
                    var match = regex.Match(source);
                    if (!match.Success) return false;
                    text = Regex.Replace(translation, @"\{(\d+)(?::(panel|fields|bones|face|details))?\}", token =>
                    {
                        var captured = match.Groups["p" + token.Groups[1].Value].Value;
                        var mode = token.Groups[2].Value;
                        if (mode == "panel") return Fragment(captured, depth + 1);
                        if (mode == "details")
                            return Regex.Replace(captured, @"(?<=\n)- ([^\r\n]+)", row => "- " + Fragment(row.Groups[1].Value, depth + 1));
                        // These captures contain SDK-controlled field/bone names, never avatar names or descriptions.
                        if (mode == "fields" || mode == "bones")
                            return string.Join(mode == "fields" ? "、" : ", ", captured.Split(new[] { ", " }, StringSplitOptions.None).Select(value => Fragment(value, depth + 1)));
                        if (mode == "face")
                        {
                            // Preserve user mesh/blendshape examples; translate only the SDK's final overflow row.
                            return Regex.Replace(captured, @"(?<=\n)- \.\.\. and (\d+) more(?=\r?\n|\z)", overflow => "- ……另有 " + overflow.Groups[1].Value + " 项");
                        }
                        return captured;
                    });
                    return true;
                }
                catch (RegexMatchTimeoutException) { return false; }
            }
        }

        private static string Fragment(string source, int depth)
        {
            if (depth > 4) return source;
            if (active.PanelFragments.TryGetValue(source, out var text)) return text;
            foreach (var template in active.FragmentTemplates)
                if (template.TryTranslate(source, out text, depth)) return text;
            return source;
        }

        internal static bool HasComponentDialog(string title) => title != null && active.Dialogs.ContainsKey(title);

        [Serializable] private sealed class Catalog { public Pair[] entries, worldEntries, accountEntries, componentEnums, componentTitles, dialogs, componentMenus, panelEntries, panelTemplates, panelFragments; }
        [Serializable] private sealed class Pair { public string key, value; }
    }
}
