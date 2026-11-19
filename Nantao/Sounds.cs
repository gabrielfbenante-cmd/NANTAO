using System;
using UnityEngine;

namespace Nantao
{
    /// <summary>Todos os sons são sintetizados na hora (nada copiado de outro jogo).</summary>
    internal static class Sounds
    {
        private const int Rate = 44100;
        private static float TwoPi => Mathf.PI * 2f;

        private static AudioClip _runHum, _runOn, _runOff, _anchorPlace, _tpWindup, _tpArrive, _fail,
                                 _reviveLoop, _reviveDone, _smokeThrow, _smokeBloom, _pick,
                                 _shadowWindup, _shadowArrive, _riftOpen, _fireLoop, _ignite, _howl, _beep;

        public static AudioClip RunHum => _runHum ?? (_runHum = MakeHum());
        public static AudioClip RunOn => _runOn ?? (_runOn = MakeZap(true));
        public static AudioClip RunOff => _runOff ?? (_runOff = MakeZap(false));
        public static AudioClip AnchorPlace => _anchorPlace ?? (_anchorPlace = MakeClink());
        public static AudioClip TpWindup => _tpWindup ?? (_tpWindup = MakeRise(0.6f));
        public static AudioClip TpArrive => _tpArrive ?? (_tpArrive = MakeWhoosh(0.7f, true));
        public static AudioClip Fail => _fail ?? (_fail = MakeFail());
        public static AudioClip ReviveLoop => _reviveLoop ?? (_reviveLoop = MakeChoir());
        public static AudioClip ReviveDone => _reviveDone ?? (_reviveDone = MakeArp());
        public static AudioClip SmokeThrow => _smokeThrow ?? (_smokeThrow = MakeThump());
        public static AudioClip SmokeBloom => _smokeBloom ?? (_smokeBloom = MakeWhoosh(1.6f, false));
        public static AudioClip Pick => _pick ?? (_pick = MakeBlip());
        public static AudioClip ShadowWindup => _shadowWindup ?? (_shadowWindup = MakeShadow(0.9f));
        public static AudioClip ShadowArrive => _shadowArrive ?? (_shadowArrive = MakeWhoosh(0.6f, false));
        public static AudioClip RiftOpen => _riftOpen ?? (_riftOpen = MakeWarble());
        public static AudioClip FireLoop => _fireLoop ?? (_fireLoop = MakeFire());
        public static AudioClip Ignite => _ignite ?? (_ignite = MakeIgnite());
        public static AudioClip Howl => _howl ?? (_howl = MakeHowl());
        public static AudioClip Beep => _beep ?? (_beep = MakeBeep());

        public static void Setup3D(AudioSource s)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = 35f;
            s.dopplerLevel = 0f;
            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (local != null && local.movementAudio != null) s.outputAudioMixerGroup = local.movementAudio.outputAudioMixerGroup;
        }

        /// <summary>Toca um som solto num ponto do mundo.</summary>
        public static void PlayAt(Vector3 pos, AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("NantaoSfx");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            Setup3D(s);
            s.clip = clip;
            s.volume = Mathf.Clamp01(ModConfig.Volume.Value) * volume;
            s.Play();
            UnityEngine.Object.Destroy(go, clip.length + 0.1f);
        }

        /// <summary>Som 2D (interface).</summary>
        public static void PlayUi(AudioClip clip, float volume = 1f)
        {
            var local = PlayerAgent.Local;
            if (local != null) local.PlaySfx(clip, volume);
        }

        // ------------------------------------------------------------------ síntese

        private static AudioClip Build(string name, float seconds, Func<float, int, System.Random, float> f, int seed = 1)
        {
            int len = Mathf.Max(1, (int)(Rate * seconds));
            var data = new float[len];
            var rng = new System.Random(seed);
            for (int i = 0; i < len; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i, rng), -1f, 1f);
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <summary>Zumbido elétrico em loop (2s, ciclos inteiros pra não "pular").</summary>
        private static AudioClip MakeHum()
        {
            float crackle = 0f;
            return Build("NantaoRunHum", 2f, (t, i, r) =>
            {
                float wob = 0.5f * Mathf.Sin(TwoPi * 3f * t);
                float buzz = 0.32f * Mathf.Sin(TwoPi * 110f * t + wob)
                             + 0.08f * Mathf.Sign(Mathf.Sin(TwoPi * 55f * t))
                             + 0.12f * Mathf.Sin(TwoPi * 220f * t + wob * 2f)
                             + 0.05f * Mathf.Sin(TwoPi * 330f * t);
                if (r.NextDouble() < 0.0006) crackle = 0.6f + (float)r.NextDouble() * 0.4f;
                crackle *= 0.9965f;
                return (buzz * 0.55f + crackle * Noise(r) * 0.55f + crackle * 0.08f * Mathf.Sin(TwoPi * 1800f * t)) * 0.7f;
            }, 1337);
        }

        private static AudioClip MakeZap(bool up)
        {
            float phase = 0f;
            float dur = up ? 0.45f : 0.3f;
            return Build(up ? "NantaoRunOn" : "NantaoRunOff", dur, (t, i, r) =>
            {
                float k = t / dur;
                float freq = up ? Mathf.Lerp(260f, 1500f, Mathf.Sqrt(k)) : Mathf.Lerp(1100f, 160f, k);
                phase += TwoPi * freq / Rate;
                float tone = Mathf.Sin(phase) + 0.35f * Mathf.Sign(Mathf.Sin(phase * 0.5f));
                return (tone * 0.35f * Mathf.Exp(-t * (up ? 6f : 10f)) + Noise(r) * 0.6f * Mathf.Exp(-t * 28f)) * 0.8f;
            }, up ? 7 : 11);
        }

        /// <summary>"Clinc" metálico (parciais inarmônicos).</summary>
        private static AudioClip MakeClink() => Build("NantaoClink", 0.9f, (t, i, r) =>
        {
            float s = 0.5f * Mathf.Sin(TwoPi * 1240f * t) * Mathf.Exp(-t * 6f)
                      + 0.35f * Mathf.Sin(TwoPi * 1873f * t) * Mathf.Exp(-t * 8f)
                      + 0.25f * Mathf.Sin(TwoPi * 2617f * t) * Mathf.Exp(-t * 11f)
                      + 0.3f * Mathf.Sin(TwoPi * 310f * t) * Mathf.Exp(-t * 14f);
            return (s + Noise(r) * 0.3f * Mathf.Exp(-t * 60f)) * 0.6f;
        }, 3);

        /// <summary>Brilho subindo (carregando o teleporte).</summary>
        private static AudioClip MakeRise(float dur)
        {
            float ph1 = 0f, ph2 = 0f;
            return Build("NantaoRise", dur, (t, i, r) =>
            {
                float k = t / dur;
                ph1 += TwoPi * Mathf.Lerp(300f, 1200f, k * k) / Rate;
                ph2 += TwoPi * Mathf.Lerp(450f, 1800f, k * k) / Rate;
                float trem = 0.6f + 0.4f * Mathf.Sin(TwoPi * Mathf.Lerp(8f, 30f, k) * t);
                float env = Mathf.SmoothStep(0f, 1f, k) * (1f - Mathf.Pow(k, 8f));
                return (Mathf.Sin(ph1) * 0.4f + Mathf.Sin(ph2) * 0.25f) * trem * env * 0.8f;
            }, 5);
        }

        /// <summary>Sopro de ar filtrado (+ sininho opcional).</summary>
        private static AudioClip MakeWhoosh(float dur, bool chime)
        {
            float lp = 0f;
            return Build(chime ? "NantaoArrive" : "NantaoBloom", dur, (t, i, r) =>
            {
                float k = t / dur;
                float cutoff = chime ? Mathf.Lerp(0.5f, 0.05f, k) : Mathf.Lerp(0.15f, 0.03f, k);
                lp += (Noise(r) - lp) * cutoff;
                float env = chime ? Mathf.Exp(-t * 5f) : Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.3f)) * (1f - k);
                float s = lp * 1.6f * env;
                if (chime) s += 0.25f * Mathf.Sin(TwoPi * 1568f * t) * Mathf.Exp(-t * 4f) + 0.15f * Mathf.Sin(TwoPi * 2093f * t) * Mathf.Exp(-t * 5f);
                return s * 0.8f;
            }, chime ? 13 : 17);
        }

        private static AudioClip MakeFail() => Build("NantaoFail", 0.35f, (t, i, r) =>
        {
            float gate = (t < 0.13f || (t > 0.18f && t < 0.31f)) ? 1f : 0f;
            return Mathf.Sign(Mathf.Sin(TwoPi * 140f * t)) * 0.25f * gate * Mathf.Exp(-(t % 0.18f) * 8f);
        });

        /// <summary>Acorde suave em loop (canalizando a cura). 2s com ciclos inteiros.</summary>
        private static AudioClip MakeChoir() => Build("NantaoReviveLoop", 2f, (t, i, r) =>
        {
            float vib = 0.003f * Mathf.Sin(TwoPi * 5f * t);
            float s = 0.3f * Mathf.Sin(TwoPi * 261.5f * t * (1f + vib))
                      + 0.22f * Mathf.Sin(TwoPi * 329.5f * t * (1f - vib))
                      + 0.2f * Mathf.Sin(TwoPi * 392f * t)
                      + 0.1f * Mathf.Sin(TwoPi * 523f * t * (1f + vib));
            float shimmer = 0.05f * Noise(r) * (0.5f + 0.5f * Mathf.Sin(TwoPi * 2f * t));
            return (s * (0.75f + 0.25f * Mathf.Sin(TwoPi * 0.5f * t)) + shimmer) * 0.55f;
        }, 19);

        /// <summary>Arpejo brilhante (reviveu!).</summary>
        private static AudioClip MakeArp() => Build("NantaoReviveDone", 1.4f, (t, i, r) =>
        {
            float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
            float s = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float st = n * 0.09f;
                if (t < st) continue;
                float lt = t - st;
                s += 0.25f * Mathf.Sin(TwoPi * notes[n] * lt) * Mathf.Exp(-lt * 3f);
            }
            return s * 0.8f;
        });

        private static AudioClip MakeThump() => Build("NantaoThrow", 0.4f, (t, i, r) =>
        {
            float body = Mathf.Sin(TwoPi * Mathf.Lerp(140f, 60f, t / 0.4f) * t) * Mathf.Exp(-t * 14f);
            return (body * 0.7f + Noise(r) * 0.4f * Mathf.Exp(-t * 18f)) * 0.8f;
        }, 23);

        private static AudioClip MakeBlip() => Build("NantaoPick", 0.18f, (t, i, r) =>
            Mathf.Sin(TwoPi * (t < 0.07f ? 880f : 1320f) * t) * 0.35f * Mathf.Exp(-t * 12f));
    
        /// <summary>Grave escuro subindo (sumindo nas sombras).</summary>
        private static AudioClip MakeShadow(float dur)
        {
            float ph = 0f, lp = 0f;
            return Build("NantaoShadow", dur, (t, i, r) =>
            {
                float k = t / dur;
                ph += TwoPi * Mathf.Lerp(70f, 160f, k) / Rate;
                lp += (Noise(r) - lp) * 0.04f;
                float env = Mathf.SmoothStep(0f, 1f, k) * (1f - Mathf.Pow(k, 10f));
                return (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 1.5f) * 0.2f + lp * 1.2f) * env * 0.7f;
            }, 29);
        }

        /// <summary>Ondulação metálica (fenda abrindo).</summary>
        private static AudioClip MakeWarble()
        {
            float ph = 0f;
            return Build("NantaoRift", 0.7f, (t, i, r) =>
            {
                ph += TwoPi * (600f + 250f * Mathf.Sin(TwoPi * 18f * t)) / Rate;
                return (Mathf.Sin(ph) * 0.4f + Noise(r) * 0.15f) * Mathf.Exp(-t * 4f) * 0.8f;
            }, 31);
        }

        /// <summary>Fogo crepitando em loop (2s).</summary>
        private static AudioClip MakeFire()
        {
            float lp = 0f, pop = 0f;
            return Build("NantaoFire", 2f, (t, i, r) =>
            {
                lp += (Noise(r) - lp) * 0.08f;
                if (r.NextDouble() < 0.0012) pop = 0.5f + (float)r.NextDouble() * 0.5f;
                pop *= 0.985f;
                return (lp * 0.9f * (0.7f + 0.3f * Mathf.Sin(TwoPi * 1.5f * t)) + Noise(r) * pop * 0.6f) * 0.6f;
            }, 37);
        }

        /// <summary>"Vush" de fogo acendendo.</summary>
        private static AudioClip MakeIgnite()
        {
            float lp = 0f;
            return Build("NantaoIgnite", 0.9f, (t, i, r) =>
            {
                lp += (Noise(r) - lp) * Mathf.Lerp(0.3f, 0.05f, t / 0.9f);
                float body = Mathf.Sin(TwoPi * 55f * t) * Mathf.Exp(-t * 8f);
                return (lp * 1.4f * Mathf.Exp(-t * 3f) + body * 0.5f) * 0.8f;
            }, 41);
        }

        /// <summary>Uivo sintético: tom que sobe, sustenta e cai, com vibrato.</summary>
        private static AudioClip MakeHowl()
        {
            float ph = 0f;
            const float dur = 1.2f;
            return Build("NantaoHowl", dur, (t, i, r) =>
            {
                float k = t / dur;
                float f = k < 0.25f ? Mathf.Lerp(320f, 560f, k / 0.25f)
                        : k < 0.7f ? 560f + 8f * Mathf.Sin(TwoPi * 6f * t)
                        : Mathf.Lerp(560f, 300f, (k - 0.7f) / 0.3f);
                ph += TwoPi * f / Rate;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 6f)) * (1f - Mathf.SmoothStep(0.75f, 1f, k));
                return (Mathf.Sin(ph) * 0.45f + Mathf.Sin(ph * 2f) * 0.12f + Noise(r) * 0.04f) * env * 0.8f;
            }, 43);
        }

        private static AudioClip MakeBeep() => Build("NantaoBeep", 0.07f, (t, i, r) =>
            Mathf.Sign(Mathf.Sin(TwoPi * 1900f * t)) * 0.18f * (1f - t / 0.07f));
    }
}