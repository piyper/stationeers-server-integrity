using System;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Serialization;
using Cysharp.Threading.Tasks;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    public sealed class GunActionRpc : INetworkRPC
    {
        public long GunId;
        public long TargetId;
        public bool ConnectedMode;
        public int OperationMode;
        public string Secret = string.Empty;
        public string OwnershipLabel = string.Empty;
        public bool Succeeded;
        public string Result = string.Empty;

        public void SerializeCall(RocketBinaryWriter writer)
        {
            writer.WriteInt64(GunId);
            writer.WriteInt64(TargetId);
            writer.WriteBoolean(ConnectedMode);
            writer.WriteInt32(OperationMode);
            writer.WriteString(Secret ?? string.Empty);
            writer.WriteString(OwnershipLabel ?? string.Empty);
        }

        public void DeserializeCall(RocketBinaryReader reader)
        {
            GunId = reader.ReadInt64();
            TargetId = reader.ReadInt64();
            ConnectedMode = reader.ReadBoolean();
            OperationMode = reader.ReadInt32();
            Secret = reader.ReadString();
            OwnershipLabel = reader.ReadString();
        }

        public void SerializeResult(RocketBinaryWriter writer)
        {
            writer.WriteBoolean(Succeeded);
            writer.WriteString(Result ?? string.Empty);
        }

        public void DeserializeResult(RocketBinaryReader reader)
        {
            Succeeded = reader.ReadBoolean();
            Result = reader.ReadString();
        }

        public async UniTask ProcessCall(long clientId)
        {
            if (!NetworkManager.IsServer)
            {
                Result = "The request did not reach the authoritative server.";
                return;
            }
            Client client = Client.Find(clientId);
            Human actor = client?.RegisteredHuman ?? (client == null ? null : Human.Find(client.ClientId));
            Thing gun = Thing.Find(GunId);
            Thing target = Thing.Find(TargetId);
            if (client == null || actor == null || gun == null || gun.RootParentHuman != actor)
            {
                Result = "The authenticated player or held gun could not be resolved.";
                return;
            }

            bool administrator = !string.IsNullOrEmpty(Settings.CurrentData.ServerAuthSecret) &&
                string.Equals(Secret, Settings.CurrentData.ServerAuthSecret, StringComparison.Ordinal);
            Succeeded = GunActionService.Apply(target, gun, actor, ConnectedMode, OperationMode,
                administrator, OwnershipLabel, out string message);
            Result = message;
            await UniTask.CompletedTask;
        }
    }
}
