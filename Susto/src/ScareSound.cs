using System;

namespace SustoNantao
{
    /// <summary>Grito do susto, sintetizado na hora (código puro, sem Unity).</summary>
    public static class ScareSound
    {
        /// <summary>Grito estridente com pancada inicial. Retorna amostras mono -1..1.</summary>
        public static float[] Scream(int rate, int seed)
        {
            const float duration = 1.5f;
            int n = (int)(rate * duration);
            var data = new float[n];
            var rng = new Random(seed);
            double[] phase = new double[6];
            double[] detune = { 1.0, 1.013, .987, 1.502, 1.996, .5 };
            double[] gain = { .3, .25, .25, .14, .09, .22 };
            float lp = 0, hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float k = t / duration;
                // Tom sobe rápido e fica tremendo (vibrato nervoso).
                double f0 = 650 + 520 * Math.Min(1.0, t / .12) + 60 * Math.Sin(2 * Math.PI * 9 * t) + 25 * Math.Sin(2 * Math.PI * 23 * t);
                double tone = 0;
                for (int o = 0; o < phase.Length; o++)
                {
                    phase[o] += f0 * detune[o] / rate;
                    phase[o] -= Math.Floor(phase[o]);
                    tone += (phase[o] * 2 - 1) * gain[o]; // dente de serra = áspero
                }
                // Chiado (passa-alta) dá o "rasgado" da voz.
                float noise = (float)(rng.NextDouble() * 2 - 1);
                hp = .9f * (hp + noise - prev); prev = noise;
                lp += (noise - lp) * .3f;

                float attack = Math.Min(1f, t / .025f);
                float release = k > .75f ? 1f - (k - .75f) / .25f : 1f;
                float env = attack * release * (.85f + .15f * (float)Math.Sin(2 * Math.PI * 13 * t));
                float scream = (float)tone * env + hp * .35f * env;

                // Pancada no começo (o "BUM" que faz pular).
                float hit = (float)Math.Sin(2 * Math.PI * 55 * t) * (float)Math.Exp(-t * 9) * 1.2f + lp * (float)Math.Exp(-t * 25) * 1.4f;

                data[i] = (float)Math.Tanh((scream + hit) * 2.4) * .95f;
            }
            return data;
        }
    
        // ------------------------------------------------------------------ sons de suspense (todos originais)

        /// <summary>Filtro passa-faixa simples (biquad RBJ) — usado pras "vogais" e madeira.</summary>
        private sealed class BandPass
        {
            private readonly double b0, b2, a1, a2;
            private double x1, x2, y1, y2;
            public BandPass(double freq, double q, int rate)
            {
                double w0 = 2 * Math.PI * freq / rate, alpha = Math.Sin(w0) / (2 * q), a0 = 1 + alpha;
                b0 = alpha / a0; b2 = -alpha / a0; a1 = -2 * Math.Cos(w0) / a0; a2 = (1 - alpha) / a0;
            }
            public float Run(float x)
            {
                double y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return (float)y;
            }
        }

        private static float[] Normalize(float[] d, float peak = .9f)
        {
            float max = 1e-6f;
            foreach (float v in d) max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / max * peak;
            return d;
        }

        private static void AddEcho(float[] d, int rate, params float[] delayGain)
        {
            var dry = (float[])d.Clone();
            for (int k = 0; k + 1 < delayGain.Length; k += 2)
            {
                int delay = (int)(delayGain[k] * rate);
                for (int i = delay; i < d.Length; i++) d[i] += dry[i - delay] * delayGain[k + 1];
            }
        }

        /// <summary>Três batidas secas na parede (toc... toc... toc).</summary>
        public static float[] Knock(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(rate * 1.2f);
            var d = new float[n];
            var wood = new BandPass(240, 6, rate);
            double[] times = { 0, .3 + rng.NextDouble() * .1, .62 + rng.NextDouble() * .12 };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, x = 0;
                foreach (double h in times)
                {
                    double dt = t - h;
                    if (dt < 0 || dt > .25) continue;
                    x += (float)((rng.NextDouble() * 2 - 1) * Math.Exp(-dt * 120) * .8);       // estalo
                    x += (float)(Math.Sin(2 * Math.PI * 155 * dt) * Math.Exp(-dt * 32) * .9); // corpo
                }
                d[i] = x + wood.Run(x) * 2.5f;
            }
            return Normalize(d);
        }

        /// <summary>Um passo (calcanhar + ponta), com variação pela semente.</summary>
        public static float[] Step(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(rate * .3f);
            var d = new float[n];
            float lp = 0;
            double f = 70 + rng.NextDouble() * 30, toe = .055 + rng.NextDouble() * .03;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * .12f;
                double heel = Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t * 28) + lp * 2.2 * Math.Exp(-t * 45);
                double tt = t - toe;
                double toeHit = tt > 0 ? (Math.Sin(2 * Math.PI * f * 1.4 * tt) * .5 + lp * 1.6) * Math.Exp(-tt * 55) : 0;
                d[i] = (float)(heel + toeHit);
            }
            return Normalize(d, .85f);
        }

        /// <summary>Porta velha rangendo (atrito "agarra-solta" excitando madeira).</summary>
        public static float[] Creak(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(rate * 1.6f);
            var d = new float[n];
            var r1 = new BandPass(820 + rng.Next(200), 12, rate);
            var r2 = new BandPass(1650 + rng.Next(300), 10, rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate, k = t / 1.6;
                double rateHz = 28 + 55 * Math.Sin(Math.PI * k) + 12 * Math.Sin(2 * Math.PI * 3.1 * t); // acelera e desacelera
                phase += rateHz / rate;
                float pulse = 0;
                if (phase >= 1) { phase -= 1; pulse = (float)(.6 + rng.NextDouble() * .4); }
                float env = (float)(Math.Sin(Math.PI * Math.Min(1, k * 1.15)) * (k < .9 ? 1 : (1 - k) * 10));
                d[i] = (r1.Run(pulse) * 1.2f + r2.Run(pulse) * .8f + (float)(rng.NextDouble() - .5) * .02f) * env;
            }
            return Normalize(d, .8f);
        }

        /// <summary>Sussurro sem palavras (ar passando por "sílabas").</summary>
        public static float[] Whisper(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(rate * 1.8f);
            var d = new float[n];
            var hiss = new BandPass(3000, 1.4, rate);
            var hiss2 = new BandPass(5200, 2, rate);
            var breath = new BandPass(700, 1.2, rate);
            // Sílabas: inícios e durações aleatórios.
            var syll = new System.Collections.Generic.List<double[]>();
            for (double t = .05; t < 1.6; t += .12 + rng.NextDouble() * .16)
                syll.Add(new[] { t, .07 + rng.NextDouble() * .14, .5 + rng.NextDouble() * .5 });
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                double env = 0;
                foreach (var s in syll)
                {
                    double x = (t - s[0]) / s[1];
                    if (x > 0 && x < 1) env = Math.Max(env, Math.Sin(Math.PI * x) * s[2]);
                }
                d[i] = (float)((hiss.Run(noise) * 1.1 + hiss2.Run(noise) * .6 + breath.Run(noise) * .35) * env);
            }
            return Normalize(d, .75f);
        }

        /// <summary>Risadinha aguda ("hi-hi-hi") com eco, sintetizada por formantes. Nada gravado.</summary>
        public static float[] Giggle(int rate, int seed)
        {
            var rng = new Random(seed);
            int count = 4 + rng.Next(3);
            double syl = .1, gap = .045 + rng.NextDouble() * .03;
            int n = (int)(rate * (count * (syl + gap) + .7));
            var d = new float[n];
            var f1 = new BandPass(380, 5, rate);
            var f2 = new BandPass(2350 + rng.Next(250), 7, rate);
            var f3 = new BandPass(3100, 8, rate);
            double phase = 0, baseF = 480 + rng.NextDouble() * 120;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                int idx = (int)(t / (syl + gap));
                double local = t - idx * (syl + gap);
                bool voiced = idx < count && local < syl;
                double f0 = baseF * (1 + .12 * (1 - local / syl)) * (1 - idx * .035) * (1 + .01 * Math.Sin(2 * Math.PI * 6 * t));
                phase += f0 / rate; phase -= Math.Floor(phase);
                double glottal = voiced ? (phase * 2 - 1) : 0;                    // fonte "voz"
                double env = voiced ? Math.Sin(Math.PI * local / syl) : 0;
                double h = voiced && local < .022 ? (rng.NextDouble() * 2 - 1) * .7 : 0; // o "h" do "hi"
                float src = (float)(glottal * env + h + (rng.NextDouble() * 2 - 1) * .08 * env);
                d[i] = f1.Run(src) * .6f + f2.Run(src) * 1.3f + f3.Run(src) * .5f;
            }
            AddEcho(d, rate, .09f, .45f, .17f, .3f, .29f, .18f); // corredor vazio
            return Normalize(d, .85f);
        }
    }
}
