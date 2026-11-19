// LGC_MA_Fury_Localization v2.0.0 — VRCFury adapter
// Atomic dictionary reload; exact text, display titles, enum captions and anchored templates.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.Vrcfury
{
    internal static class TranslationCatalog
    {
        private static Snapshot active = new Snapshot();
        private static bool loaded;
        internal static IEnumerable<string> Warnings => active.Warnings;
        internal static void Load(bool force = false)
        {
            if (loaded && !force) return;
            var package = PackageInfo.FindForAssembly(typeof(VrcfuryLocalization).Assembly);
            var root = package != null ? package.resolvedPath : "Packages/" + VrcfuryLocalization.PackageName;
            var data = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(root, "Editor/Localization/vrcfury.zh-Hans.json")));
            if (data == null || data.entries == null)
                throw new InvalidOperationException("词典格式不正确，请检查 entries 字段。");
            var next = new Snapshot();
            ReadPairs(data.entries, next.Texts);
            ReadPairs(data.titles, next.Titles);
            ReadPairs(data.enumEntries, next.Enums);
            ReadPairs(data.componentTitles, next.ComponentTitles);
            foreach (var pair in data.patterns ?? Array.Empty<Pair>())
            {
                if (pair == null || string.IsNullOrEmpty(pair.key) || pair.value == null) continue;
                try { next.Patterns.Add(new Template(pair.key, pair.value)); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    // One invalid optional rule must not disable all other VRCFury translations.
                    next.Warnings.Add("已跳过动态词条 [" + pair.key + "]：" + e.Message);
                }
            }
            foreach (var set in data.choiceSets ?? Array.Empty<ChoiceSet>())
                if (set?.values != null && set.values.Length > 0) next.ChoiceSets.Add(set.values);
            active = next;
            loaded = true;
        }

        private static void ReadPairs(Pair[] source, Dictionary<string, string> destination)
        {
            foreach (var pair in source ?? Array.Empty<Pair>())
                if (pair != null && !string.IsNullOrEmpty(pair.key) && pair.value != null)
                    destination[pair.key] = pair.value;
        }

        internal static string Text(string source)
        {
            if (source == null || !VrcfuryLocalization.IsApplied) return source;
            var snapshot = active;
            if (snapshot.Texts.TryGetValue(source, out var translated)) return translated;
            foreach (var rule in snapshot.Patterns)
            {
                try
                {
                    var match = rule.Matcher.Match(source);
                    if (match.Success) return rule.Render(match);
                }
                catch (RegexMatchTimeoutException) { /* Keep this original display. */ }
            }
            return source;
        }

        internal static string EnumText(string source)
        {
            if (source == null || !VrcfuryLocalization.IsApplied) return source;
            if (active.Enums.TryGetValue(source, out var translated)) return translated;
            return Text(source);
        }

        internal static string Title(string source)
        {
            if (source == null || !VrcfuryLocalization.IsApplied) return source;
            return active.Titles.TryGetValue(source, out var translated) ? translated : Text(source);
        }

        internal static string ComponentTitle(string typeName)
        {
            if (!VrcfuryLocalization.IsApplied || typeName == null) return null;
            return active.ComponentTitles.TryGetValue(typeName, out var title) ? Title(title) : null;
        }

        internal static bool IsDisplayChoiceSet(IList<string> choices)
        {
            if (choices == null) return false;
            foreach (var set in active.ChoiceSets)
            {
                if (set.Length != choices.Count) continue;
                var same = true;
                for (var i = 0; i < set.Length; i++)
                    if (set[i] != choices[i]) { same = false; break; }
                if (same) return true;
            }
            return false;
        }

        internal static string Menu(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            var translated = Title(source);
            if (translated != source) return translated;
            var segments = source.Split('/');
            for (var i = 0; i < segments.Length; i++) segments[i] = Title(segments[i]);
            return string.Join("/", segments);
        }

        private sealed class Snapshot
        {
            internal readonly Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> Titles = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> Enums = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> ComponentTitles = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly List<Template> Patterns = new List<Template>();
            internal readonly List<string> Warnings = new List<string>();
            internal readonly List<string[]> ChoiceSets = new List<string[]>();
        }

        private sealed class Template
        {
            private static readonly Regex Slots = new Regex(@"\{([^{}]+)\}");
            internal readonly Regex Matcher;
            private readonly string value;
            private readonly Dictionary<string, string> groups = new Dictionary<string, string>();
            internal Template(string source, string translated)
            {
                value = translated;
                var sourceSlots = new HashSet<string>();
                var outputSlots = new HashSet<string>();
                foreach (Match slot in Slots.Matches(source)) sourceSlots.Add(slot.Value);
                foreach (Match slot in Slots.Matches(translated)) outputSlots.Add(slot.Value);
                if (sourceSlots.Count == 0 || !sourceSlots.SetEquals(outputSlots))
                    throw new InvalidOperationException("动态译文占位符不匹配：" + source);
                // Each template must include literal UI wording; a bare placeholder could match user data.
                if (Slots.Replace(source, "").Trim().Length < 4)
                    throw new InvalidOperationException("动态译文需要足够明确的原文：" + source);
                var expression = "\\A";
                var offset = 0;
                foreach (Match slot in Slots.Matches(source))
                {
                    expression += Regex.Escape(source.Substring(offset, slot.Index - offset));
                    if (!groups.TryGetValue(slot.Value, out var group))
                    {
                        // Placeholder text may contain C# expressions; regex names use a safe index.
                        group = "slot" + groups.Count;
                        groups.Add(slot.Value, group);
                        expression += "(?<" + group + ">.*?)";
                    }
                    else expression += "\\k<" + group + ">";
                    offset = slot.Index + slot.Length;
                }
                expression += Regex.Escape(source.Substring(offset)) + "\\z";
                Matcher = new Regex(expression, RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
            }
            internal string Render(Match match) => Slots.Replace(value, slot => match.Groups[groups[slot.Value]].Value);
        }
        [Serializable] private sealed class Catalog
        {
            public Pair[] entries, titles, enumEntries, patterns, componentTitles;
            public ChoiceSet[] choiceSets;
        }
        [Serializable] private sealed class Pair { public string key, value; }
        [Serializable] private sealed class ChoiceSet { public string[] values; }
    }
}
