using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using HarmonyLib;
using UnityEngine;

namespace ServerIntegrity
{
    [HarmonyPatch(typeof(Structure), nameof(Structure.SetStructureData),
        new[] { typeof(Quaternion), typeof(ulong), typeof(Grid3), typeof(int) })]
    internal static class PlacementOwnerPatch
    {
        private static void Prefix(ref ulong ownerClientId)
        {
            if (ownerClientId == 0 || ownerClientId > long.MaxValue) return;
            Brain brain = Thing.Find<Brain>((long)ownerClientId);
            if (brain != null && brain.ClientId != 0) ownerClientId = brain.ClientId;
        }

        private static void Postfix(Structure __instance)
        {
            if (!NetworkManager.IsServer || __instance == null || __instance.OwnerClientId == 0) return;

            // Native placement state can reach clients without the finalized Steam-ID
            // owner. Send it after creation has completed and the reference is resolvable.
            Plugin.QueuePlacedOwnerSync(__instance);
        }
    }
}
