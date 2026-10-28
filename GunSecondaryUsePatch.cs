using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    [HarmonyPatch(typeof(Item), nameof(Item.OnUseSecondary))]
    internal static class GunSecondaryUsePatch
    {
        private static bool Prefix(Item __instance, bool doAction,
            ref Thing.DelayedActionInstance __result)
        {
            if (!IntegrityGunSystem.IsIntegrityGun(__instance)) return true;

            if (!doAction)
            {
                int nextMode = (IntegrityGunSystem.NormalizeMode(__instance.Mode) + 1) % 4;
                __instance.Mode = nextMode;
                if (NetworkManager.IsClient)
                {
                    new GunModeRpc { GunId = __instance.ReferenceId, Mode = nextMode }
                        .CallHost().Forget();
                }
            }

            __result = new Thing.DelayedActionInstance
            {
                Duration = 0f,
                ActionMessage = IntegrityGunSystem.GetModeName(__instance.Mode),
                ExtendedMessage = "Cycles the Integrity Gun operation mode."
            }.Succeed();
            return false;
        }
    }
}
