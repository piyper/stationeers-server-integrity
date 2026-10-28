using Assets.Scripts.Networking;
using HarmonyLib;
using Util.Commands;

namespace ServerIntegrity
{
    [HarmonyPatch(typeof(ThingCommand), nameof(ThingCommand.Execute))]
    internal static class ThingCommandGuardPatch
    {
        private const string DeniedMessage =
            "You do not have permission to use mutating thing commands.";

        private static bool Prefix(string[] args, ref string __result)
        {
            // A single-player/local-host session can be both client and server.
            // Server authority means this is not an unauthenticated remote client.
            if (!NetworkManager.IsClient || NetworkManager.IsServer || !IsMutating(args)) return true;

            __result = DeniedMessage;
            return false;
        }

        private static bool IsMutating(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrEmpty(args[0])) return false;

            switch (args[0].ToLowerInvariant())
            {
                case "spawn":
                case "spawnvel":
                case "delete":
                    return true;
                default:
                    return false;
            }
        }
    }
}
