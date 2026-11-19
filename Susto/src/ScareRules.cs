using System;

namespace SustoNantao
{
    /// <summary>Estado do jogador local usado para decidir se pode tomar susto (código puro, testável).</summary>
    public struct PlayerState
    {
        public bool Alive, Controlled, InTerminal, Typing, MenuOpen, InShip, InsideFacility, Landed;
    }

    /// <summary>Regras do susto (código puro, sem Unity).</summary>
    public static class ScareRules
    {
        /// <summary>Pode assustar agora? (fora de menus, vivo, com a nave pousada e, se pedido, dentro do prédio)</summary>
        public static bool CanScare(PlayerState p, bool onlyInside)
        {
            if (!p.Alive || !p.Controlled || p.InTerminal || p.Typing || p.MenuOpen || p.InShip || !p.Landed) return false;
            return !onlyInside || p.InsideFacility;
        }

        /// <summary>Probabilidade de susto num intervalo de <paramref name="seconds"/>, dada a chance por minuto (%).</summary>
        public static double ChanceInInterval(float chancePerMinutePercent, float seconds)
        {
            if (float.IsNaN(chancePerMinutePercent) || float.IsNaN(seconds) || chancePerMinutePercent <= 0 || seconds <= 0) return 0;
            double perMinute = Math.Min(1.0, chancePerMinutePercent / 100.0);
            if (perMinute >= 1.0) return 1.0;
            return 1.0 - Math.Pow(1.0 - perMinute, seconds / 60.0);
        }

        /// <summary>Sorteio: true se o número aleatório (0..1) cair dentro da chance do intervalo.</summary>
        public static bool Roll(double random01, float chancePerMinutePercent, float seconds) =>
            random01 < ChanceInInterval(chancePerMinutePercent, seconds);

        /// <summary>Já passou o tempo mínimo desde o último susto e o tempo inicial dentro do prédio?</summary>
        public static bool Ready(float now, float lastScare, float minInterval, float eligibleTime, float firstDelay) =>
            now - lastScare >= minInterval && eligibleTime >= firstDelay;

        /// <summary>
        /// Lê um arquivo WAV (PCM 8/16/24/32 bits ou float 32). Retorna amostras intercaladas -1..1, ou null se inválido.
        /// </summary>
        public static float[] ParseWav(byte[] d, out int channels, out int rate)
        {
            channels = 0; rate = 0;
            if (d == null || d.Length < 44 || Ascii(d, 0) != "RIFF" || Ascii(d, 8) != "WAVE") return null;
            int format = 0, bits = 0, pos = 12, dataStart = -1, dataLen = 0;
            while (pos + 8 <= d.Length)
            {
                string id = Ascii(d, pos);
                int len = BitConverter.ToInt32(d, pos + 4);
                if (len < 0) return null;
                int body = pos + 8;
                if (id == "fmt " && body + 16 <= d.Length)
                {
                    format = BitConverter.ToInt16(d, body);
                    channels = BitConverter.ToInt16(d, body + 2);
                    rate = BitConverter.ToInt32(d, body + 4);
                    bits = BitConverter.ToInt16(d, body + 14);
                }
                else if (id == "data")
                {
                    dataStart = body;
                    dataLen = Math.Min(len, d.Length - body);
                    break;
                }
                pos = body + len + (len & 1);
            }
            if (dataStart < 0 || channels < 1 || channels > 8 || rate < 4000 || rate > 192000) return null;
            bool pcm = format == 1 || format == -2; // -2 = WAVE_FORMAT_EXTENSIBLE (0xFFFE)
            bool flt = format == 3;
            int bytes = bits / 8;
            if (bytes < 1 || (!pcm && !flt) || (flt && bits != 32)) return null;
            int count = dataLen / bytes;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                int o = dataStart + i * bytes;
                float v;
                if (flt) v = BitConverter.ToSingle(d, o);
                else if (bits == 8) v = (d[o] - 128) / 128f;
                else if (bits == 16) v = BitConverter.ToInt16(d, o) / 32768f;
                else if (bits == 24) v = ((d[o] | (d[o + 1] << 8) | ((sbyte)d[o + 2] << 16))) / 8388608f;
                else if (bits == 32) v = BitConverter.ToInt32(d, o) / 2147483648f;
                else return null;
                samples[i] = v < -1 ? -1 : v > 1 ? 1 : v;
            }
            return samples;
        }

        private static string Ascii(byte[] d, int o) =>
            o + 4 <= d.Length ? new string(new[] { (char)d[o], (char)d[o + 1], (char)d[o + 2], (char)d[o + 3] }) : "";
    }
}
