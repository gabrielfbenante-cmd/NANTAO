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
        public float size = 1f; // escala do carrinho (decidida pelo host)
        public string[] cargoIds = new string[0];
    }

    public static class CartRules
    {
        public static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool ValidSnapshot(CartSnapshot state, int capacity)
        {
            if (state == null || state.version != 1 || state.cargoIds == null || state.cargoIds.Length > capacity) return false;
            if (!IsFinite(state.x) || !IsFinite(state.y) || !IsFinite(state.z) || !IsFinite(state.yaw) || !IsFinite(state.speed)) return false;
            if (!IsFinite(state.size) || state.size < .5f || state.size > 3f) return false;
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
        /// <summary>
        /// Aceleração ladeira abaixo (só horizontal), a partir da normal do chão.
        /// Abaixo de minDegrees o chão conta como reto (o carrinho não sai andando sozinho).
        /// </summary>
        public static void SlopeAcceleration(float nx, float ny, float nz, float gravity, float minDegrees,
                                             out float ax, out float az)
        {
            ax = 0; az = 0;
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-6 || !IsFinite(gravity) || ny <= 0) return;
            double cos = ny / length;
            double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI;
            if (angle < minDegrees) return;
            // Gravidade projetada no plano do chão: g - (g·n)n, com g = (0, -|g|, 0).
            double g = Math.Abs(gravity);
            double ux = nx / length, uy = ny / length, uz = nz / length;
            double dot = -g * uy;
            ax = (float)(-dot * ux);
            az = (float)(-dot * uz);
        }

        /// <summary>Velocidade máxima empurrando: maior na descida, menor na subida.</summary>
        public static float SlopeSpeedLimit(float baseSpeed, float downhillAmount)
        {
            if (!IsFinite(downhillAmount)) downhillAmount = 0;
            float k = Math.Max(-.5f, Math.Min(1.5f, downhillAmount));
            return Math.Max(1f, baseSpeed * (1f + k));
        }

        public static bool CanTakeRole(long client, long currentRole, long otherRole)
        {
            return client >= 0 && client != otherRole && (currentRole == -1 || currentRole == client);
        }
    }
}
