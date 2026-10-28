using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Serialization;
using Cysharp.Threading.Tasks;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    public sealed class IntegrityStateSyncRpc : INetworkRPC
    {
        private struct State
        {
            internal long ReferenceId;
            internal ulong OwnerSteamId;
            internal bool Indestructable;
        }

        private readonly List<State> _states = new List<State>();

        public void SerializeCall(RocketBinaryWriter writer) { }

        public void DeserializeCall(RocketBinaryReader reader) { }

        public void SerializeResult(RocketBinaryWriter writer)
        {
            writer.WriteInt32(_states.Count);
            foreach (State state in _states)
            {
                writer.WriteInt64(state.ReferenceId);
                writer.WriteUInt64(state.OwnerSteamId);
                writer.WriteBoolean(state.Indestructable);
            }
        }

        public void DeserializeResult(RocketBinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000)
                throw new InvalidOperationException("Invalid integrity state count.");

            for (int index = 0; index < count; index++)
            {
                long referenceId = reader.ReadInt64();
                ulong ownerSteamId = reader.ReadUInt64();
                bool indestructable = reader.ReadBoolean();
                Thing target = Thing.Find(referenceId);
                if (target == null) continue;
                target.OwnerClientId = ownerSteamId;
                bool integrityChanged = target.Indestructable != indestructable;
                target.Indestructable = indestructable;
                if (integrityChanged) target.DamageState?.OnDamageUpdated();
            }
        }

        public async UniTask ProcessCall(long clientId)
        {
            if (!NetworkManager.IsServer) return;
            _states.Clear();
            GridController.AllStructuresPool.ForEach(structure =>
            {
                if (structure == null || structure.IsBeingDestroyed) return;
                _states.Add(new State
                {
                    ReferenceId = structure.ReferenceId,
                    OwnerSteamId = structure.OwnerClientId,
                    Indestructable = structure.Indestructable
                });
            });
            await UniTask.CompletedTask;
        }
    }
}
