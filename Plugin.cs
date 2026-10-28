using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace ServerIntegrity
{
    public sealed class Plugin : MonoBehaviour
    {
        public const string PluginGuid = "ca.piyper.stationeers.integritygun";
        public const string PluginVersion = "0.6.7";
        internal static ManualLogSource Log;
        internal static IModNetworking Networking;
        private LaunchPadBooster.Mod _mod;
        private Harmony _harmony;
        private long _syncConnectionId = -1;
        private float _syncAt;
        private static readonly List<PendingOwnerSync> PendingOwnerSyncs = new List<PendingOwnerSync>();

        private sealed class PendingOwnerSync
        {
            internal Structure Structure;
            internal float SendAt;
        }

        internal static void QueuePlacedOwnerSync(Structure structure)
        {
            if (structure == null) return;
            PendingOwnerSync existing = PendingOwnerSyncs.Find(item => item.Structure == structure);
            if (existing != null)
            {
                existing.SendAt = Time.unscaledTime + 0.25f;
                return;
            }
            PendingOwnerSyncs.Add(new PendingOwnerSync
            {
                Structure = structure,
                SendAt = Time.unscaledTime + 0.25f
            });
        }

        private void Update()
        {
            if (Networking != null)
                Networking.Required = NetworkManager.IsServer;

            FlushPlacedOwnerSyncs();

            if (GameManager.GameState == GameState.Running)
                IndestructableRenameRequestPatch.Install(_harmony);

            if (!NetworkManager.IsClient || NetworkManager.IsServer ||
                GameManager.GameState != GameState.Running || NetworkClient.ConnectionId < 0)
            {
                _syncConnectionId = -1;
                _syncAt = 0f;
                return;
            }

            long connectionId = NetworkClient.ConnectionId;
            if (_syncConnectionId != connectionId)
            {
                _syncConnectionId = connectionId;
                _syncAt = Time.unscaledTime + 1f;
            }
            if (_syncAt <= 0f || Time.unscaledTime < _syncAt) return;
            _syncAt = 0f;
            new IntegrityStateSyncRpc().CallHost().Forget();
        }

        private static void FlushPlacedOwnerSyncs()
        {
            if (!NetworkManager.IsServer)
            {
                PendingOwnerSyncs.Clear();
                return;
            }

            float now = Time.unscaledTime;
            for (int index = PendingOwnerSyncs.Count - 1; index >= 0; index--)
            {
                PendingOwnerSync pending = PendingOwnerSyncs[index];
                if (pending.SendAt > now) continue;
                PendingOwnerSyncs.RemoveAt(index);

                Structure structure = pending.Structure;
                if (structure == null || structure.IsBeingDestroyed ||
                    structure.ReferenceId == 0 || structure.OwnerClientId == 0) continue;

                new OwnershipStateMessage
                {
                    TargetIds = new[] { structure.ReferenceId },
                    OwnerSteamId = structure.OwnerClientId
                }.SendAll(-1L);
            }
        }

        private void OnLoaded(ConfigFile config)
        {
            Log = BepInEx.Logging.Logger.CreateLogSource("ServerIntegrity");
            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                _mod = new LaunchPadBooster.Mod(PluginGuid, PluginVersion);
                Networking = _mod.Networking;
                Networking.Required = GameManager.IsBatchMode;
                Networking.RegisterRPC<GunActionRpc>();
                Networking.RegisterRPC<GunModeRpc>();
                Networking.RegisterRPC<IntegrityStateSyncRpc>();
                Networking.RegisterMessage<IntegrityStateMessage>();
                Networking.RegisterMessage<OwnershipStateMessage>();
                IntegrityGunSystem.Initialize();
                Log.LogInfo("Server Integrity " + PluginVersion + " loaded.");
            }
            catch (Exception exception)
            {
                Log.LogError(exception);
            }
        }

        private void OnDestroy()
        {
            PendingOwnerSyncs.Clear();
            IntegrityGunSystem.Shutdown();
            IndestructableRenameRequestPatch.Reset();
            Networking = null;
            _harmony?.UnpatchSelf();
        }
    }
}
