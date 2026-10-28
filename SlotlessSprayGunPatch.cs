using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using HarmonyLib;
using UnityEngine;

namespace ServerIntegrity
{
    /// <summary>
    /// The custom guns use the Spray Gun model and On/Off selector, but not its
    /// paint system. Their prefab slot list is empty so inventory UI and
    /// Stationpedia do not advertise a Spray Can slot. These narrow guards keep
    /// inherited SprayGun accessors from indexing the removed slot.
    /// </summary>
    internal static class SlotlessSprayGunPatch
    {
        private static bool IsManaged(SprayGun gun) => IntegrityGunSystem.IsManagedGun(gun);

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.SprayCan), MethodType.Getter)]
        private static class SprayCanGetter
        {
            private static bool Prefix(SprayGun __instance, ref SprayCan __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.IsOperable), MethodType.Getter)]
        private static class IsOperableGetter
        {
            private static bool Prefix(SprayGun __instance, ref bool __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.IsEmpty), MethodType.Getter)]
        private static class IsEmptyGetter
        {
            private static bool Prefix(SprayGun __instance, ref bool __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.GetPaintMaterial))]
        private static class PaintMaterialGetter
        {
            private static bool Prefix(SprayGun __instance, ref Material __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.GetUseAmount))]
        private static class UseAmountGetter
        {
            private static bool Prefix(SprayGun __instance, ref float __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = 0f;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.CheckTogglePower))]
        private static class TogglePowerGuard
        {
            private static bool Prefix(SprayGun __instance, ref bool __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(SprayGun), nameof(SprayGun.OnUseItem))]
        private static class NativeUseGuard
        {
            private static bool Prefix(SprayGun __instance, ref bool __result)
            {
                if (!IsManaged(__instance)) return true;
                __result = false;
                return false;
            }
        }
    }
}
