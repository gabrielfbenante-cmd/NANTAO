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

    public static class FlightRules
    {
        public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool Valid(FlightMessage message, bool launch)
        {
            ulong id;
            if (message == null || message.version != 1 || !ulong.TryParse(message.item, out id) ||
                !Finite(message.x) || !Finite(message.y) || !Finite(message.z) ||
                Math.Abs(message.x) > 100000 || Math.Abs(message.y) > 100000 || Math.Abs(message.z) > 100000) return false;
            if (!launch) return message.action == "explode" && message.shot > 0;
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
    }
}
