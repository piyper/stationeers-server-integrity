using System.Reflection;
using Assets.Scripts.Objects;
using HarmonyLib;

namespace ServerIntegrity
{
    internal static class GunModeStorage
    {
        private static readonly FieldInfo ModeField =
            AccessTools.Field(typeof(Thing), "_mode");

        internal static int Read(Thing thing) =>
            IntegrityGunSystem.NormalizeMode((int)ModeField.GetValue(thing));

        internal static void Write(Thing thing, int value) =>
            ModeField.SetValue(thing, IntegrityGunSystem.NormalizeMode(value));
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Mode), MethodType.Getter)]
    internal static class GunModeGetterPatch
    {
        private static bool Prefix(Thing __instance, ref int __result)
        {
            if (!IntegrityGunSystem.IsIntegrityGun(__instance)) return true;
            __result = GunModeStorage.Read(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Mode), MethodType.Setter)]
    internal static class GunModeSetterPatch
    {
        private static bool Prefix(Thing __instance, int value)
        {
            if (!IntegrityGunSystem.IsIntegrityGun(__instance)) return true;
            GunModeStorage.Write(__instance, value);
            return false;
        }
    }
}
