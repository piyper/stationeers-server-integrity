using System;
using System.Reflection;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.UI.ImGuiUi;
using UnityEngine;

namespace ServerIntegrity
{
    internal enum IntegrityOperationMode
    {
        ToggleTarget = 0,
        ToggleConnected = 1,
        ToggleMatching = 2,
        ToggleBlastAreas = 3
    }

    internal static class IntegrityGunSystem
    {
        internal const string PrefabName = "ItemIntegrityGun";
        internal const string OwnershipPrefabName = "ItemOwnershipGun";
        internal const string ScannerPrefabName = "ItemSensorProcessingUnitIntegrityScanner";
        internal const string DefaultOwnershipGunName = "Ownership Gun";
        private const string SourcePrefabName = "ItemSprayGun";
        private const string SourceScannerPrefabName = "ItemSensorProcessingUnitMesonScanner";
        private static bool _initialized;
        private static bool _registered;

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Prefab.OnPrefabsLoaded += Register;
            if (Prefab.PrefabsGameObject != null) Register();
        }

        internal static void Shutdown()
        {
            Prefab.OnPrefabsLoaded -= Register;
            _initialized = false;
            _registered = false;
        }

        private static void Register()
        {
            if (_registered || Prefab.PrefabsGameObject == null) return;
            SprayGun source = Prefab.Find<SprayGun>(SourcePrefabName);
            if (source == null)
            {
                Plugin.Log?.LogError("Could not find the native spray gun prefab.");
                return;
            }

            SprayGun gun = UnityEngine.Object.Instantiate(source, Prefab.PrefabsGameObject.transform);
            gun.name = PrefabName;
            gun.PrefabName = PrefabName;
            gun.PrefabHash = Animator.StringToHash(PrefabName);
            gun.CustomName = "Integrity Gun";
            gun.IsCustomThing = true;
            gun.HasModeState = true;
            gun.ThingTransformPosition = Vector3.zero;
            gun.ThingTransform.rotation = Quaternion.identity;
            HidePaintSlot(gun);
            Prefab.RegisterExisting(gun);
            ImguiCreativeSpawnMenu.AddDynamicItem(gun);

            SprayGun ownershipGun = UnityEngine.Object.Instantiate(source, Prefab.PrefabsGameObject.transform);
            ownershipGun.name = OwnershipPrefabName;
            ownershipGun.PrefabName = OwnershipPrefabName;
            ownershipGun.PrefabHash = Animator.StringToHash(OwnershipPrefabName);
            ownershipGun.CustomName = DefaultOwnershipGunName;
            ownershipGun.IsCustomThing = true;
            ownershipGun.ThingTransformPosition = Vector3.zero;
            ownershipGun.ThingTransform.rotation = Quaternion.identity;
            HidePaintSlot(ownershipGun);
            Prefab.RegisterExisting(ownershipGun);
            ImguiCreativeSpawnMenu.AddDynamicItem(ownershipGun);

            SPUMesonScanner scannerSource = Prefab.Find<SPUMesonScanner>(SourceScannerPrefabName);
            if (scannerSource == null)
            {
                UnityEngine.Object.DestroyImmediate(gun.gameObject);
                Plugin.Log?.LogError("Could not find the native T-Ray scanner SPU prefab.");
                return;
            }

            GameObject scannerClone = UnityEngine.Object.Instantiate(scannerSource.gameObject, Prefab.PrefabsGameObject.transform);
            SPUMesonScanner originalScanner = scannerClone.GetComponent<SPUMesonScanner>();
            IntegrityScannerSPU scanner = scannerClone.AddComponent<IntegrityScannerSPU>();
            CopyComponentFields(originalScanner, scanner);
            UnityEngine.Object.DestroyImmediate(originalScanner);
            scanner.name = ScannerPrefabName;
            scanner.PrefabName = ScannerPrefabName;
            scanner.PrefabHash = Animator.StringToHash(ScannerPrefabName);
            scanner.CustomName = "Integrity Scanner SPU";
            scanner.IsCustomThing = true;
            scanner.ThingTransformPosition = Vector3.zero;
            scanner.ThingTransform.rotation = Quaternion.identity;
            Prefab.RegisterExisting(scanner);
            ImguiCreativeSpawnMenu.AddDynamicItem(scanner);
            _registered = true;
            Plugin.Log?.LogInfo("Registered Integrity Gun, Ownership Gun, and Integrity Scanner SPU from native prefabs.");
        }

        private static void HidePaintSlot(SprayGun gun)
        {
            if (gun?.Slots == null || gun.Slots.Count == 0) return;

            // SprayGun directly indexes Slots[0], so retain the slot internally while
            // removing it from inventory windows and the world interaction lookup.
            Slot paintSlot = gun.Slots[0];
            paintSlot.IsInteractable = false;
            paintSlot.IsSwappable = false;
            paintSlot.AllowDragging = false;
            paintSlot.IsLocked = true;
            paintSlot.HidesOccupant = true;
            if (paintSlot.Collider != null) paintSlot.Collider.enabled = false;
            gun.Slots.Clear();
        }

        private static void CopyComponentFields(Component source, Component target)
        {
            for (Type type = source.GetType(); type != null && typeof(Component).IsAssignableFrom(type); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsInitOnly) continue;
                    FieldInfo targetField = FindField(target.GetType(), field.Name);
                    if (targetField == null || targetField.IsInitOnly ||
                        !targetField.FieldType.IsAssignableFrom(field.FieldType)) continue;
                    try { targetField.SetValue(target, field.GetValue(source)); } catch { }
                }
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                                                      BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        internal static bool IsIntegrityGun(Thing thing) =>
            thing != null && thing.PrefabName == PrefabName;

        internal static bool IsOwnershipGun(Thing thing) =>
            thing != null && thing.PrefabName == OwnershipPrefabName;

        internal static bool IsManagedGun(Thing thing) => IsIntegrityGun(thing) || IsOwnershipGun(thing);

        internal static int NormalizeMode(int mode) => mode >= 0 && mode <= 3 ? mode : 0;

        internal static string GetModeName(int mode)
        {
            switch ((IntegrityOperationMode)NormalizeMode(mode))
            {
                case IntegrityOperationMode.ToggleConnected: return "Toggle connected";
                case IntegrityOperationMode.ToggleMatching: return "Toggle matching";
                case IntegrityOperationMode.ToggleBlastAreas: return "Toggle blast areas";
                default: return "Toggle target";
            }
        }
    }
}
