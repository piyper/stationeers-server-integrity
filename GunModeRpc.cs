using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Serialization;
using Cysharp.Threading.Tasks;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    public sealed class GunModeRpc : INetworkRPC
    {
        public long GunId;
        public int Mode;

        public void SerializeCall(RocketBinaryWriter writer)
        {
            writer.WriteInt64(GunId);
            writer.WriteInt32(Mode);
        }

        public void DeserializeCall(RocketBinaryReader reader)
        {
            GunId = reader.ReadInt64();
            Mode = reader.ReadInt32();
        }

        public void SerializeResult(RocketBinaryWriter writer) { }
        public void DeserializeResult(RocketBinaryReader reader) { }

        public async UniTask ProcessCall(long clientId)
        {
            if (!NetworkManager.IsServer) return;
            Client client = Client.Find(clientId);
            Thing gun = Thing.Find(GunId);
            if (client?.RegisteredHuman == null || !IntegrityGunSystem.IsIntegrityGun(gun) ||
                gun.RootParentHuman != client.RegisteredHuman) return;
            gun.Mode = IntegrityGunSystem.NormalizeMode(Mode);
            await UniTask.CompletedTask;
        }
    }
}
