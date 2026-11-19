using System;

namespace ChineloArremessavel
{
    [Serializable]
    public sealed class FlightMessage
    {
        public int version = 1;
        public string action;
        public string item;
        public long shot;
        public ulong player;
        public float x, y, z, dx, dy, dz, range, speed;
    }

    /// <summary>Host → todos: "jogador X tomou uma porrada e desmaiou".</summary>
    [Serializable]
    public sealed class KnockMessage
    {
        public int version = 1;
        public ulong target;
        public float kx, ky, kz;   // empurrão (m/s)
        public float seconds;      // tempo desmaiado
        public int damage;         // dano (0 = só empurra)
    }

    public static class FlightRules
    {
        public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool Valid(FlightMessage message, bool launch)
        {
            ulong id;
            if (message == null || message.version != 1 || !ulong.TryParse(message.item, out id) ||
                !Finite(message.x) || !Finite(message.y) || !Finite(message.z) ||
                Math.Abs(message.x) > 100000 || Math.Abs(message.y) > 100000 || Math.Abs(message.z) > 100000) return false;
            // Fim do voo: "explode" (10% de chance) ou "land" (cai no chão e pode ser pego de novo).
            if (!launch) return (message.action == "explode" || message.action == "land") && message.shot > 0;
            float norm = message.dx * message.dx + message.dy * message.dy + message.dz * message.dz;
            return (message.action == "request" || message.action == "launch") && Finite(norm) && norm >= .9f && norm <= 1.1f &&
                (message.action == "request" || (message.shot > 0 && Finite(message.range) && message.range >= 3 && message.range <= 25 &&
                Finite(message.speed) && message.speed >= 6 && message.speed <= 30));
        }
        public static float Distance(float elapsed, float speed, float range)
        {
            if (!Finite(elapsed) || !Finite(speed) || !Finite(range) || elapsed <= 0 || speed <= 0 || range <= 0) return 0;
            return Math.Min(range, elapsed * speed);
        }

        public static bool ValidKnock(KnockMessage message)
        {
            if (message == null || message.version != 1) return false;
            if (!Finite(message.kx) || !Finite(message.ky) || !Finite(message.kz) || !Finite(message.seconds)) return false;
            float force = message.kx * message.kx + message.ky * message.ky + message.kz * message.kz;
            return force <= 200f * 200f && message.seconds >= .5f && message.seconds <= 20f &&
                message.damage >= 0 && message.damage <= 100;
        }

        /// <summary>Sorteio do host: explode quando o número (0..100) cai abaixo da chance.</summary>
        public static bool RollExplosion(double roll0to100, float chancePercent)
        {
            if (!Finite(chancePercent) || chancePercent <= 0) return false;
            return roll0to100 < Math.Min(100f, chancePercent);
        }

        /// <summary>Empurrão da explosão: mais forte perto do centro, metade na borda.</summary>
        public static float BlastFalloff(float distance, float radius)
        {
            if (!Finite(distance) || !Finite(radius) || radius <= 0 || distance > radius) return 0;
            return 1f - .5f * Math.Max(0f, distance) / radius;
        }
    }
}
