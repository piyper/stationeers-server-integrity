using System;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    public sealed class OwnershipStateMessage : INetworkMessage
    {
        public long[] TargetIds = Array.Empty<long>();
        public ulong OwnerSteamId;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteInt32(TargetIds.Length);
            foreach (long targetId in TargetIds) writer.WriteInt64(targetId);
            writer.WriteUInt64(OwnerSteamId);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000) throw new InvalidOperationException("Invalid ownership target count.");
            TargetIds = new long[count];
            for (int i = 0; i < count; i++) TargetIds[i] = reader.ReadInt64();
            OwnerSteamId = reader.ReadUInt64();
        }

        public void Process(long clientId)
        {
            foreach (long targetId in TargetIds)
            {
                Thing target = Thing.Find(targetId);
                if (target != null) target.OwnerClientId = OwnerSteamId;
            }
        }
    }
}
