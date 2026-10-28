using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Entities;
using HarmonyLib;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace ServerIntegrity
{
    [HarmonyPatch]
    internal static class IntegrityAttackPatch
    {
        private const float ActivationCooldownSeconds = 0.25f;
        private const float ContactTolerance = 0.15f;
        private static readonly Dictionary<long, float> NextActivationTime = new Dictionary<long, float>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(Thing).Assembly.GetTypes()
                .Where(type => typeof(Thing).IsAssignableFrom(type))
                .Select(type => type.GetMethod(nameof(Thing.AttackWith),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(Attack), typeof(bool) }, null))
                .Where(method => method != null);
        }

        private static bool Prefix(Thing __instance, Attack attack, bool doAction,
            ref Thing.DelayedActionInstance __result)
        {
            if (!IntegrityGunSystem.IsManagedGun(attack.SourceItem)) return true;

            bool ownershipGun = IntegrityGunSystem.IsOwnershipGun(attack.SourceItem);
            bool connectedMode = attack.SourceItem is SprayGun gun && gun.OnOff;
            int operationMode = ownershipGun ? 0 : IntegrityGunSystem.NormalizeMode(attack.SourceItem.Mode);
            bool blastMode = operationMode == (int)IntegrityOperationMode.ToggleBlastAreas;
            bool nextState = !__instance.Indestructable;
            __result = new Thing.DelayedActionInstance
            {
                Duration = 0.25f,
                ActionMessage = ownershipGun ? "Set Ownership" :
                    (blastMode ? "Toggle Blast Areas" :
                     nextState ? "Make Indestructible" : "Make Destructible"),
                ExtendedMessage = $"Owner: {GunActionService.DescribeOwner(__instance)}\n" +
                    (ownershipGun
                        ? (connectedMode ? "Mode: Connected construction\n" : "Mode: Target only\n")
                        : $"Mode: {IntegrityGunSystem.GetModeName(operationMode)}\n") +
                    (ownershipGun
                        ? "Assigns ownership using the player name or Steam ID written on the gun."
                        : blastMode
                        ? "Finds hazardous equipment within 6 m of the connected construction, then toggles every structure within 6 m of each hazard."
                        : nextState
                        ? "Protects the selected object(s) from damage and deconstruction."
                        : "Restores normal damage and deconstruction for the selected object(s).")
            }.Succeed();

            // AttackWith is invoked while the primary-use action is being evaluated,
            // before its progress timer has completed. It must only describe the
            // delayed action. GunUsePatch applies the mutation from UseItemComplete,
            // making that completion callback the single authoritative action point.
            return false;
        }

        internal static List<Thing> FindTargets(Thing target, bool connectedMode)
        {
            if (!connectedMode || !(target is Structure root)) return new List<Thing> { target };

            List<Structure> allStructures = new List<Structure>();
            Dictionary<long, Bounds> worldBounds = new Dictionary<long, Bounds>();
            GridController.AllStructuresPool.ForEach(structure =>
            {
                if (structure == null || structure.IsBeingDestroyed) return;
                allStructures.Add(structure);
                worldBounds[structure.ReferenceId] = GetWorldBounds(structure);
            });

            HashSet<long> visited = new HashSet<long>();
            Queue<Structure> pending = new Queue<Structure>();
            List<Thing> result = new List<Thing>();
            pending.Enqueue(root);
            while (pending.Count > 0 && result.Count < 10000)
            {
                Structure current = pending.Dequeue();
                if (current == null || !visited.Add(current.ReferenceId)) continue;
                result.Add(current);

                Enqueue(current.NeighborStructures, pending, visited);
                Enqueue(current.AttachedDevices, pending, visited);

                // NeighborStructures only represents some grid collision types. Frames,
                // windows, faces, and several small-grid objects can physically touch
                // without appearing in that graph, so bridge those gaps by world bounds.
                Bounds currentBounds = worldBounds[current.ReferenceId];
                currentBounds.Expand(ContactTolerance * 2f);
                foreach (Structure candidate in allStructures)
                {
                    if (!visited.Contains(candidate.ReferenceId) &&
                        currentBounds.Intersects(worldBounds[candidate.ReferenceId]))
                        pending.Enqueue(candidate);
                }

                // Attached devices know their host through the host's list, so walk the
                // reverse edge as well when the initially selected object is a device.
                foreach (Structure candidate in allStructures)
                {
                    if (candidate.AttachedDevices != null && candidate.AttachedDevices.Contains(current as SmallGrid) &&
                        !visited.Contains(candidate.ReferenceId))
                        pending.Enqueue(candidate);
                }
            }
            return result;
        }

        private static Bounds GetWorldBounds(Structure structure)
        {
            Bounds local = structure.GetLocalBounds;
            Vector3 min = local.min;
            Vector3 max = local.max;
            Transform transform = structure.ThingTransform;
            Vector3 first = transform.TransformPoint(min);
            Bounds world = new Bounds(first, Vector3.zero);
            world.Encapsulate(transform.TransformPoint(new Vector3(max.x, min.y, min.z)));
            world.Encapsulate(transform.TransformPoint(new Vector3(min.x, max.y, min.z)));
            world.Encapsulate(transform.TransformPoint(new Vector3(min.x, min.y, max.z)));
            world.Encapsulate(transform.TransformPoint(new Vector3(max.x, max.y, min.z)));
            world.Encapsulate(transform.TransformPoint(new Vector3(max.x, min.y, max.z)));
            world.Encapsulate(transform.TransformPoint(new Vector3(min.x, max.y, max.z)));
            world.Encapsulate(transform.TransformPoint(max));
            return world;
        }

        private static void Enqueue<T>(IEnumerable<T> structures, Queue<Structure> pending,
            HashSet<long> visited) where T : Structure
        {
            if (structures == null) return;
            foreach (Structure structure in structures)
                if (structure != null && !visited.Contains(structure.ReferenceId)) pending.Enqueue(structure);
        }
    }
}
