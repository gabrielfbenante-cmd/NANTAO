using System;

namespace SustoNantao
{
    /// <summary>Eventos de suspense, do mais leve ao mais pesado.</summary>
    public enum SuspenseEvent { Creak = 0, Knock = 1, Footsteps = 2, Lights = 3, Giggle = 4, Whisper = 5 }

    /// <summary>Regras do medidor de tensão (código puro, testável).</summary>
    public static class SuspenseRules
    {
        /// <summary>Tensão mínima para cada evento começar a acontecer.</summary>
        public static readonly float[] Unlock = { .05f, .15f, .25f, .4f, .5f, .65f };

        /// <summary>
        /// Atualiza a tensão (0..1). Sobe dentro do prédio (bem mais rápido sozinho e no escuro),
        /// cai perto dos amigos e fora do prédio. <paramref name="buildMinutes"/> = minutos sozinho no escuro até o máximo.
        /// </summary>
        public static float UpdateTension(float tension, float dt, bool inside, bool alone, bool dark, float buildMinutes)
        {
            if (float.IsNaN(tension)) tension = 0;
            if (dt <= 0 || float.IsNaN(dt)) return Clamp01(tension);
            float build = 1f / Math.Max(.1f, buildMinutes) / 60f; // por segundo
            float rate;
            if (!inside) rate = -build * 4f;
            else if (!alone) rate = -build * 1.5f;
            else rate = build * (dark ? 1.4f : .7f);
            return Clamp01(tension + rate * dt);
        }

        /// <summary>Eventos por minuto: nenhum com tensão zero, até o máximo configurado no topo.</summary>
        public static float EventsPerMinute(float tension, float maxPerMinute)
        {
            if (float.IsNaN(tension) || maxPerMinute <= 0) return 0;
            return maxPerMinute * (float)Math.Pow(Clamp01(tension), 1.3);
        }

        /// <summary>Chance de um evento num intervalo de <paramref name="seconds"/> (processo de Poisson).</summary>
        public static double EventChance(float tension, float maxPerMinute, float seconds)
        {
            if (seconds <= 0) return 0;
            return 1 - Math.Exp(-EventsPerMinute(tension, maxPerMinute) * seconds / 60.0);
        }

        /// <summary>Multiplica a chance do susto na tela quando a tensão está alta (até 4x no máximo).</summary>
        public static float ScareMultiplier(float tension) => 1f + 3f * Clamp01((tension - .6f) / .4f);

        /// <summary>
        /// Escolhe um evento liberado pela tensão, evitando repetir o último.
        /// Retorna -1 se nenhum estiver liberado.
        /// </summary>
        public static int PickEvent(float tension, double random01, int last)
        {
            int count = 0;
            for (int i = 0; i < Unlock.Length; i++) if (tension >= Unlock[i] && i != last) count++;
            if (count == 0)
            {
                if (last >= 0 && last < Unlock.Length && tension >= Unlock[last]) return last;
                return -1;
            }
            int pick = Math.Min(count - 1, (int)(Clamp01((float)random01) * count));
            for (int i = 0; i < Unlock.Length; i++)
            {
                if (tension < Unlock[i] || i == last) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        private static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }
}
