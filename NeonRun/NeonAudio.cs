using UnityEngine;

namespace NeonRun
{
    /// <summary>Sons sintetizados na hora (nada copiado de outro jogo).</summary>
    internal static class NeonAudio
    {
        private const int Rate = 44100;

        private static AudioClip _hum, _on, _off;

        public static AudioClip Hum => _hum != null ? _hum : (_hum = MakeHum());
        public static AudioClip On => _on != null ? _on : (_on = MakeZap(true));
        public static AudioClip Off => _off != null ? _off : (_off = MakeZap(false));

        private static float TwoPi => Mathf.PI * 2f;

        /// <summary>Zumbido elétrico em loop (2s, frequências fecham ciclos inteiros pra não "pular").</summary>
        private static AudioClip MakeHum()
        {
            int len = Rate * 2;
            var data = new float[len];
            var rng = new System.Random(1337);
            float crackleEnv = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float wobble = 0.5f * Mathf.Sin(TwoPi * 3f * t);
                float buzz = 0.32f * Mathf.Sin(TwoPi * 110f * t + wobble)
                             + 0.16f * Mathf.Sign(Mathf.Sin(TwoPi * 55f * t)) * 0.5f
                             + 0.12f * Mathf.Sin(TwoPi * 220f * t + wobble * 2f)
                             + 0.05f * Mathf.Sin(TwoPi * 330f * t);

                if (rng.NextDouble() < 0.0006) crackleEnv = 0.6f + (float)rng.NextDouble() * 0.4f;
                crackleEnv *= 0.9965f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float crackle = crackleEnv * noise * 0.55f + crackleEnv * 0.08f * Mathf.Sin(TwoPi * 1800f * t);

                data[i] = (buzz * 0.55f + crackle) * 0.7f;
            }
            var clip = AudioClip.Create("NeonRunHum", len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Estalo de ligar (sobe) ou desligar (desce).</summary>
        private static AudioClip MakeZap(bool up)
        {
            int len = (int)(Rate * (up ? 0.45f : 0.3f));
            var data = new float[len];
            var rng = new System.Random(up ? 7 : 11);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float k = t / (len / (float)Rate);
                float freq = up ? Mathf.Lerp(260f, 1500f, Mathf.Sqrt(k)) : Mathf.Lerp(1100f, 160f, k);
                phase += TwoPi * freq / Rate;
                float tone = Mathf.Sin(phase) + 0.35f * Mathf.Sign(Mathf.Sin(phase * 0.5f));
                float env = Mathf.Exp(-t * (up ? 6f : 10f));
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 28f);
                data[i] = (tone * 0.35f * env + noise * 0.6f) * 0.8f;
            }
            var clip = AudioClip.Create(up ? "NeonRunOn" : "NeonRunOff", len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
