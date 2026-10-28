using System;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using LaunchPadBooster.Networking;

namespace ServerIntegrity
{
    public sealed class IntegrityStateMessage : INetworkMessage
    {
        public long[] TargetIds = Array.Empty<long>();
        public bool Indestructable;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteInt32(TargetIds.Length);
            foreach (long targetId in TargetIds) writer.WriteInt64(targetId);
            writer.WriteBoolean(Indestructable);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000) throw new InvalidOperationException("Invalid integrity target count.");
            TargetIds = new long[count];
            for (int i = 0; i < count; i++) TargetIds[i] = reader.ReadInt64();
            Indestructable = reader.ReadBoolean();
        }

        public void Process(long clientId)
        {
            foreach (long targetId in TargetIds)
            {
                Thing target = Thing.Find(targetId);
                if (target == null) continue;
                if (target.Indestructable == Indestructable) continue;
                target.Indestructable = Indestructable;
                target.DamageState?.OnDamageUpdated();
            }
        }
    }
}
