// LGC_VRCMAFury_中文拓展 v2.3.6 — SDK upload dialogs and progress display
// Patch display arguments in an explicit SDK type allowlist, including its generated callbacks.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ILGenerator = System.Reflection.Emit.ILGenerator;

namespace LgcChina.MaFuryLocalization.VrcSdk
{
    internal static class UploadDisplayPatches
    {
        private const string HarmonyId = LocalizationTool.PackageName + ".vrcsdk.upload-display";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);

        internal static void Apply()
        {
            SetOff();
            var count = 0;
            var seen = new HashSet<Type>();
            foreach (var name in new[]
            {
                "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder",
                "VRC.SDKBase.VRCMultiPlatformBuild",
                "VRC.SDKBase.Editor.Elements.ThumbnailBlock",
                "VRC.SDKBase.Editor.Elements.ThumbnailAccuracyWarningWindow"
            })
            {
                Type root = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { root = assembly.GetType(name, false); } catch { }
                    if (root != null) break;
                }
                if (root == null) continue;
                try { PatchType(root, seen, ref count); }
                catch (Exception e)
                {
                    VrcSdkLocalization.Note("SDK 上传可选类型已跳过：" + name + "（" + e.GetBaseException().Message + "）");
                }
            }
            VrcSdkLocalization.Note("SDK 上传独立弹窗与进度：已接入 " + count + " 个显示入口；缺少的方法自动跳过。");
        }

        private static void PatchType(Type type, HashSet<Type> seen, ref int count)
        {
            if (!seen.Add(type) || type.ContainsGenericParameters) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var methods = new List<MethodBase>(type.GetMethods(flags));
            methods.AddRange(type.GetConstructors(flags));
            foreach (var method in methods)
            {
                if (method.IsAbstract || method.ContainsGenericParameters || method.Name == "ShowSettingsOptions") continue;
                try
                {
                    if (method.GetMethodBody() == null || !DisplayCalls.HasDisplayCall(method)) continue;
                    Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(UploadDisplayPatches), nameof(Transpiler)));
                    count++;
                }
                catch (Exception e)
                {
                    VrcSdkLocalization.Note("SDK 上传可选显示入口已跳过：" + type.Name + "." + method.Name + "（" + e.GetBaseException().Message + "）");
                }
            }
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                PatchType(nested, seen, ref count);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => DisplayCalls.Rewrite(instructions, generator, false, true);

        internal static void SetOff()
        {
            try { Harmony.UnpatchAll(HarmonyId); } catch { }
        }
    }
}
