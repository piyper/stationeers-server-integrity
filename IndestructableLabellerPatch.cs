using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using HarmonyLib;

namespace ServerIntegrity
{
    /// <summary>
    /// Prevents a Labeller (including a modified client sending the same request)
    /// from changing the name of an indestructible object. This deliberately does
    /// not patch Thing.RenameThing, so save loading and authoritative internal
    /// naming continue to work normally.
    /// </summary>
    [HarmonyPatch(typeof(Labeller), nameof(Labeller.Rename))]
    internal static class IndestructableLabellerPatch
    {
        private static bool Prefix(Labeller __instance, Thing thing)
        {
            if (thing == null || !thing.Indestructable)
                return true;

            __instance.PlayCancelSound();
            ConsoleWindow.Print("Labeller: this secured object cannot be renamed.");
            return false;
        }
    }

    internal static class IndestructableRenameRequestPatch
    {
        private static bool _installed;

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;

            harmony.Patch(
                AccessTools.Method(typeof(ThingRenameMessage), nameof(ThingRenameMessage.Process)),
                prefix: new HarmonyMethod(typeof(IndestructableRenameRequestPatch), nameof(Prefix)));
            _installed = true;
            Plugin.Log?.LogInfo("Installed the post-load indestructible rename-request guard.");
        }

        internal static void Reset() => _installed = false;

        private static bool Prefix(ThingRenameMessage __instance)
        {
            if (!Thing.TryFind(__instance.ThingId, out Thing thing) || !thing.Indestructable)
                return true;

            Plugin.Log?.LogWarning(
                $"Rejected rename request for indestructible object {thing.ReferenceId} ({thing.PrefabName}).");
            return false;
        }
    }
}
