using System;

namespace CarrinhoCriativo
{
    [Serializable]
    public sealed class CartRequest
    {
        public int version = 1;
        public string action;
        public string itemId;
    }

    [Serializable]
    public sealed class CartSnapshot
    {
        public int version = 1;
        public bool exists;
        public float x, y, z, yaw;
        public float speed;
        public long driver = -1;
        public long passenger = -1;
        public string[] cargoIds = new string[0];
    }

    public static class CartRules
    {
        public static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool ValidSnapshot(CartSnapshot state, int capacity)
        {
            if (state == null || state.version != 1 || state.cargoIds == null || state.cargoIds.Length > capacity) return false;
            if (!IsFinite(state.x) || !IsFinite(state.y) || !IsFinite(state.z) || !IsFinite(state.yaw) || !IsFinite(state.speed)) return false;
            if (state.driver < -1 || state.passenger < -1 || (state.driver >= 0 && state.driver == state.passenger)) return false;
            if (!state.exists && (state.driver >= 0 || state.passenger >= 0 || state.cargoIds.Length != 0)) return false;
            if (Math.Abs(state.x) > 100000 || Math.Abs(state.y) > 100000 || Math.Abs(state.z) > 100000 || state.speed < 0 || state.speed > 100) return false;
            System.Collections.Generic.HashSet<ulong> ids = new System.Collections.Generic.HashSet<ulong>();
            foreach (string text in state.cargoIds)
            {
                ulong id;
                if (!ulong.TryParse(text, out id) || !ids.Add(id)) return false;
            }
            return true;
        }
        public static float LoadedSpeed(float emptySpeed, int cargo)
        {
            return Math.Max(1.8f, Math.Min(6f, emptySpeed) - Math.Max(0, cargo) * 0.18f);
        }
        public static bool CanTakeRole(long client, long currentRole, long otherRole)
        {
            return client >= 0 && client != otherRole && (currentRole == -1 || currentRole == client);
        }
    }
}
