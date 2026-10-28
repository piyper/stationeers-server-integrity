using Assets.Scripts;
using Assets.Scripts.Inventory;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Serialization;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    [HarmonyPatch(typeof(InventoryManager), "UseItemComplete")]
    internal static class GunUsePatch
    {
        private static bool Prefix(InventoryManager __instance)
        {
            Thing gun = __instance.ActiveHand?.Slot?.Get<Thing>();
            if (!IntegrityGunSystem.IsManagedGun(gun)) return true;

            // Harmony prefixes execute before Stationeers' own incomplete-action
            // guard. Preserve that guard explicitly so neither a local mutation nor
            // an RPC is issued until the delayed action has fully completed.
            if (__instance.LastCompletedRatio < 1f) return false;

            Thing target = CursorManager.CursorThing;
            if (target == null) return false;
            bool connected = gun is SprayGun sprayGun && sprayGun.OnOff;
            int operationMode = IntegrityGunSystem.IsIntegrityGun(gun)
                ? IntegrityGunSystem.NormalizeMode(gun.Mode)
                : 0;

            if (NetworkManager.IsClient)
            {
                new GunActionRpc
                {
                    GunId = gun.ReferenceId,
                    TargetId = target.ReferenceId,
                    ConnectedMode = connected,
                    OperationMode = operationMode,
                    Secret = Settings.CurrentData.ServerAuthSecret ?? string.Empty,
                    OwnershipLabel = gun.CustomName ?? string.Empty
                }.CallHost().Forget();
                return false;
            }

            if (GameManager.RunSimulation)
            {
                GunActionService.Apply(target, gun, InventoryManager.ParentHuman, connected, operationMode,
                    true, gun.CustomName, out _);
                return false;
            }
            return false;
        }
    }
}
