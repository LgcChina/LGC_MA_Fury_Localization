// LGC_MAFury_中文拓展 v2.0.0 — Modular Avatar adapter
// Stable translation resources. Missing entries keep the original text.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace LgcChina.MaFuryLocalization.ModularAvatar
{
    internal static class TranslationCatalog
    {
        internal static readonly Dictionary<string, string> Entries = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> Components = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> ComponentTypes = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> Menus = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> UiLiterals = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> Controls = new Dictionary<string, string>();
        internal static Literal[] Literals = Array.Empty<Literal>();
        private static bool loaded;
        private static readonly Regex placeholders = new Regex(@"(?<!\{)\{(\d+)[^{}]*\}(?!\})");

        internal static void Load(bool force = false)
        {
            if (loaded && !force) return;
            var package = PackageInfo.FindForAssembly(typeof(MaLocalization).Assembly);
            var root = package != null ? package.resolvedPath : "Packages/" + MaLocalization.PackageName;
            var data = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(root, "Editor/Localization/ma.zh-Hans.json")));
            if (data == null || data.entries == null)
                throw new InvalidOperationException("中文词典无法读取，请检查 JSON 格式和 entries 字段。");

            // Build the replacement before touching the active catalog, so invalid edits keep the current translation.
            var entries = ReadPairs(data.entries);
            var menus = ReadPairs(data.menus);
            var uiLiterals = ReadPairs(data.uiLiterals);
            var controls = ReadPairs(data.controls);
            var components = new Dictionary<string, string>();
            var componentTypes = new Dictionary<string, string>();
            foreach (var item in data.components ?? Array.Empty<ComponentEntry>())
            {
                if (item == null || string.IsNullOrEmpty(item.path) || string.IsNullOrEmpty(item.text)) continue;
                components[NormalizeComponentPath(item.path)] = item.text;
                if (!string.IsNullOrEmpty(item.typeName)) componentTypes[item.typeName] = item.text;
            }
            // Earlier MA releases used the old name for the same component.
            if (components.TryGetValue("Modular Avatar/MA Merge Motion", out var motion))
                components["Modular Avatar/MA Merge Blend Tree"] = motion;
            var literals = new List<Literal>();
            foreach (var rule in data.literals ?? Array.Empty<Literal>())
                if (rule != null && !string.IsNullOrEmpty(rule.typePrefix) && rule.source != null && rule.value != null)
                    literals.Add(rule);
            var literalArray = literals.ToArray();

            ReplacePairs(Entries, entries);
            ReplacePairs(Menus, menus);
            ReplacePairs(UiLiterals, uiLiterals);
            ReplacePairs(Controls, controls);
            ReplacePairs(Components, components);
            ReplacePairs(ComponentTypes, componentTypes);
            Literals = literalArray;
            loaded = true;
        }

        private static Dictionary<string, string> ReadPairs(Pair[] pairs)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in pairs ?? Array.Empty<Pair>())
                if (pair != null && !string.IsNullOrEmpty(pair.key) && pair.value != null)
                    result[pair.key] = pair.value;
            return result;
        }

        private static void ReplacePairs(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            target.Clear();
            foreach (var pair in source) target.Add(pair.Key, pair.Value);
        }

        internal static string NormalizeComponentPath(string path)
        {
            if (path == null) return string.Empty;
            path = path.TrimStart('/');
            return path.StartsWith("Component/", StringComparison.Ordinal) ? path.Substring(10) : path;
        }

        internal static bool TryLiteral(Type type, string text, out string translated)
        {
            translated = null;
            if (type == null || text == null) return false;
            var name = type.FullName ?? string.Empty;
            foreach (var rule in Literals)
                if (rule.source == text && (name == rule.typePrefix || name.StartsWith(rule.typePrefix + "+", StringComparison.Ordinal)))
                { translated = rule.value; return translated != null; }
            return false;
        }

        internal static bool FormatsMatch(string original, string translated)
        {
            if (original == null || translated == null) return false;
            var before = new HashSet<string>();
            var after = new HashSet<string>();
            foreach (Match match in placeholders.Matches(original)) before.Add(match.Groups[1].Value);
            foreach (Match match in placeholders.Matches(translated)) after.Add(match.Groups[1].Value);
            return before.SetEquals(after);
        }

        internal static bool HasLiteralScope(Type type)
        {
            var name = type.FullName ?? string.Empty;
            foreach (var rule in Literals)
                if (name == rule.typePrefix || name.StartsWith(rule.typePrefix + "+", StringComparison.Ordinal)) return true;
            return false;
        }

        [Serializable] private sealed class Catalog
        {
            public Pair[] entries, menus, uiLiterals, controls;
            public ComponentEntry[] components;
            public Literal[] literals;
        }
        [Serializable] private sealed class Pair { public string key, value; }
        [Serializable] private sealed class ComponentEntry { public string path, text, typeName; }
        [Serializable] internal sealed class Literal { public string typePrefix, source, value; }
    }
}
