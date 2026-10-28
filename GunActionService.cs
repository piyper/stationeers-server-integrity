using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Pipes;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace ServerIntegrity
{
    internal static class GunActionService
    {
        private const float BlastProtectionRadius = 6f;

        internal static string DescribeOwner(Thing target)
        {
            ulong ownerSteamId = target?.OwnerClientId ?? 0;
            if (ownerSteamId == 0) return "Unowned";

            Client client = Client.Find(ownerSteamId);
            string name = client?.RegisteredHuman?.DisplayName;
            if (string.IsNullOrWhiteSpace(name)) name = client?.name;

            if (string.IsNullOrWhiteSpace(name))
            {
                // Human.Find expects a Thing reference ID, not a Steam ID. Ownership is
                // persisted as the Steam ID, so resolve it against the loaded bodies (or
                // their brains) instead. This also handles a disconnected player's body.
                Human human = Human.AllHumans.FirstOrDefault(candidate =>
                    candidate != null &&
                    (candidate.OwnerClientId == ownerSteamId ||
                     candidate.OrganBrain?.ClientId == ownerSteamId));
                name = human?.DisplayName;
            }

            return string.IsNullOrWhiteSpace(name)
                ? "Unknown owner"
                : name.Trim();
        }

        internal static bool Apply(Thing selected, Thing gun, Human actor, bool connectedMode,
            int operationMode,
            bool administrator, string ownershipLabel, out string result)
        {
            result = string.Empty;
            if (selected == null || gun == null || actor == null)
            {
                result = "The target, gun, or player could not be resolved.";
                return false;
            }

            ulong actorSteamId = actor.OwnerClientId != 0
                ? actor.OwnerClientId
                : actor.OrganBrain?.ClientId ?? 0;
            List<Thing> targets = IntegrityAttackPatch.FindTargets(selected, connectedMode);
            if (IntegrityGunSystem.IsOwnershipGun(gun))
            {
                if (!administrator)
                {
                    result = "The Ownership Gun is restricted to server administrators.";
                    return false;
                }
                string requestedOwner = (ownershipLabel ?? gun.CustomName ?? string.Empty).Trim();
                ulong ownerSteamId;
                if (string.IsNullOrEmpty(requestedOwner) ||
                    string.Equals(requestedOwner, IntegrityGunSystem.DefaultOwnershipGunName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ownerSteamId = actorSteamId;
                    if (ownerSteamId == 0)
                    {
                        result = "The administrator's Steam ID could not be resolved.";
                        return false;
                    }
                }
                else if (!TryResolveOwner(requestedOwner, out ownerSteamId, out result))
                {
                    return false;
                }

                foreach (Thing target in targets) target.OwnerClientId = ownerSteamId;
                BroadcastOwnership(targets, ownerSteamId);
                result = $"Assigned {targets.Count} object(s) to Steam ID {ownerSteamId}.";
                Plugin.Log?.LogInfo(result);
                return true;
            }

            if (!IntegrityGunSystem.IsIntegrityGun(gun))
            {
                result = "The selected item is not a managed gun.";
                return false;
            }

            operationMode = IntegrityGunSystem.NormalizeMode(operationMode);
            switch ((IntegrityOperationMode)operationMode)
            {
                case IntegrityOperationMode.ToggleConnected:
                    targets = IntegrityAttackPatch.FindTargets(selected, true);
                    break;
                case IntegrityOperationMode.ToggleMatching:
                    targets = IntegrityAttackPatch.FindTargets(selected, true)
                        .Where(target => target.PrefabHash == selected.PrefabHash).ToList();
                    break;
                case IntegrityOperationMode.ToggleBlastAreas:
                    List<Thing> connected = IntegrityAttackPatch.FindTargets(selected, true);
                    List<Thing> hazards = new List<Thing>();
                    GridController.AllStructuresPool.ForEach(candidate =>
                    {
                        if (candidate == null || candidate.IsBeingDestroyed || !IsHazardous(candidate)) return;
                        if (connected.Any(structure =>
                            (candidate.ThingTransformPosition - structure.ThingTransformPosition).sqrMagnitude <=
                            BlastProtectionRadius * BlastProtectionRadius))
                            hazards.Add(candidate);
                    });
                    if (hazards.Count == 0)
                    {
                        result = "No blast-area objects were found near the connected construction.";
                        return false;
                    }
                    targets = new List<Thing>();
                    GridController.AllStructuresPool.ForEach(candidate =>
                    {
                        if (candidate == null || candidate.IsBeingDestroyed) return;
                        if (hazards.Any(hazard =>
                            (candidate.ThingTransformPosition - hazard.ThingTransformPosition).sqrMagnitude <=
                            BlastProtectionRadius * BlastProtectionRadius))
                            targets.Add(candidate);
                    });
                    foreach (Thing hazard in hazards)
                        if (!targets.Contains(hazard)) targets.Add(hazard);
                    break;
                default:
                    targets = new List<Thing> { selected };
                    break;
            }

            if (!administrator)
            {
                if (actorSteamId == 0)
                {
                    result = "The player's Steam ID could not be resolved.";
                    return false;
                }
                targets = targets.Where(target => target.OwnerClientId == actorSteamId).ToList();
                if (targets.Count == 0)
                {
                    result = "You do not own the selected structure.";
                    return false;
                }
            }

            Thing declarativeTarget = operationMode == (int)IntegrityOperationMode.ToggleBlastAreas
                ? targets.First(IsHazardous)
                : selected;
            bool nextState = !declarativeTarget.Indestructable;
            List<Thing> changedTargets = new List<Thing>();
            foreach (Thing target in targets)
            {
                if (target.Indestructable == nextState) continue;
                target.Indestructable = nextState;
                target.DamageState?.OnDamageUpdated();
                changedTargets.Add(target);
            }
            BroadcastIntegrity(changedTargets);
            result = $"Integrity Gun changed {changedTargets.Count} object(s); indestructible={nextState}.";
            Plugin.Log?.LogInfo(result);
            return true;
        }

        private static bool IsHazardous(Thing target) =>
            target is FurnaceBase || target is Tank || target is GasTankStorage || target is InLineTank;

        private static void BroadcastIntegrity(List<Thing> targets)
        {
            if (!NetworkManager.IsServer || targets.Count == 0) return;
            foreach (IGrouping<bool, Thing> group in targets.GroupBy(target => target.Indestructable))
            {
                new IntegrityStateMessage
                {
                    TargetIds = group.Select(target => target.ReferenceId).ToArray(),
                    Indestructable = group.Key
                }.SendAll(-1L);
            }
        }

        private static bool TryResolveOwner(string label, out ulong steamId, out string error)
        {
            string requested = (label ?? string.Empty).Trim();
            if (ulong.TryParse(requested, out steamId))
            {
                error = string.Empty;
                return true;
            }

            List<Client> matches = new List<Client>();
            if (NetworkManager.HostClient != null) matches.Add(NetworkManager.HostClient);
            if (NetworkBase.Clients != null) matches.AddRange(NetworkBase.Clients);
            matches = matches.Where(client => client != null &&
                (string.Equals(client.name, requested, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(client.RegisteredHuman?.DisplayName, requested, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(client => client.ClientId).Select(group => group.First()).ToList();

            if (matches.Count == 1)
            {
                steamId = matches[0].ClientId;
                error = string.Empty;
                return true;
            }
            steamId = 0;
            error = matches.Count == 0
                ? "The gun label is neither an online player name nor a numeric Steam ID."
                : "More than one online player matches that name; label the gun with a Steam ID.";
            return false;
        }

        private static void BroadcastOwnership(List<Thing> targets, ulong ownerSteamId)
        {
            if (!NetworkManager.IsServer) return;
            new OwnershipStateMessage
            {
                TargetIds = targets.Select(target => target.ReferenceId).ToArray(),
                OwnerSteamId = ownerSteamId
            }.SendAll(-1L);
        }
    }
}
