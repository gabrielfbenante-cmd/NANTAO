using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ChineloArremessavel
{
    /// <summary>
    /// Explosão grande (os 10% do chinelo): explosões do jogo ampliadas, bola de fogo, onda de choque,
    /// estilhaços, fumaça, clarão de luz, clarão branco na tela e estrondo longo. Roda em cada PC.
    /// </summary>
    public sealed class BigExplosionFx : MonoBehaviour
    {
        private const float Life = 4.5f;

        private float scale, t, volume;
        private Transform fireball, smoke;
        private Material fireMat, smokeMat, ringMat, sparkMat;
        private LineRenderer ring;
        private readonly List<LineRenderer> sparks = new List<LineRenderer>();
        private readonly List<Vector3> sparkPos = new List<Vector3>();
        private readonly List<Vector3> sparkVel = new List<Vector3>();
        private HDAdditionalLightData flashLight;
        private float screenFlash;
        private static AudioClip bigBoom;
        private static Shader unlit;

        public static void Spawn(Vector3 position, float visualScale, float volume)
        {
            GameObject go = new GameObject("ChineloBigExplosion");
            go.transform.position = position;
            BigExplosionFx fx = go.AddComponent<BigExplosionFx>();
            fx.scale = Mathf.Clamp(visualScale, 1f, 8f);
            fx.volume = Mathf.Clamp01(volume);
            fx.Build();
        }

        // ------------------------------------------------------------------ montagem

        private void Build()
        {
            VanillaExplosions();
            Vector3 c = transform.position;

            fireMat = Glow(new Color(1f, .55f, .15f), 6f);
            fireball = Sphere("BolaDeFogo", fireMat);

            smokeMat = Transparent(new Color(.08f, .07f, .06f, 0f));
            if (smokeMat != null) smoke = Sphere("Fumaca", smokeMat);

            ringMat = Glow(new Color(1f, .75f, .4f), 4f);
            ring = Line("OndaDeChoque", ringMat, 64);
            ring.loop = true;

            sparkMat = Glow(new Color(1f, .6f, .2f), 5f);
            System.Random rng = new System.Random();
            int count = Mathf.RoundToInt(18 * scale);
            for (int i = 0; i < count; i++)
            {
                LineRenderer s = Line("Estilhaco", sparkMat, 2);
                Vector3 dir = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * .9f + .1f, (float)rng.NextDouble() * 2 - 1).normalized;
                sparks.Add(s);
                sparkPos.Add(c);
                sparkVel.Add(dir * (6f + (float)rng.NextDouble() * 10f) * Mathf.Sqrt(scale));
            }

            try
            {
                GameObject lightGo = new GameObject("Clarao");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 1.5f;
                flashLight = lightGo.AddHDLight(HDLightTypeAndShape.Point);
                flashLight.EnableShadows(false);
                flashLight.SetColor(new Color(1f, .7f, .4f));
                flashLight.SetRange(12f * scale);
            }
            catch (Exception) { flashLight = null; }

            // Clarão branco na tela de quem está perto.
            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (local != null && !local.isPlayerDead)
            {
                float d = Vector3.Distance(local.transform.position, c);
                screenFlash = Mathf.Clamp01(1f - d / (10f * scale)) * .85f;
            }

            PlaySound(c);
        }

        /// <summary>A explosão do próprio jogo, ampliada, no centro e num anel em volta.</summary>
        private void VanillaExplosions()
        {
            if (StartOfRound.Instance == null || StartOfRound.Instance.explosionPrefab == null) return;
            int ringCount = Mathf.Clamp(Mathf.RoundToInt(scale * 2f), 3, 10);
            for (int i = 0; i <= ringCount; i++)
            {
                Vector3 offset = Vector3.zero;
                float size = scale;
                if (i > 0)
                {
                    float angle = i * Mathf.PI * 2f / ringCount;
                    offset = new Vector3(Mathf.Cos(angle), .4f, Mathf.Sin(angle)) * 1.6f * scale;
                    size = scale * .6f;
                }
                GameObject effect = Instantiate(StartOfRound.Instance.explosionPrefab, transform.position + offset, Quaternion.Euler(-90f, 0, 0));
                effect.transform.localScale = Vector3.one * size;
                foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; // faz a escala valer pras partículas
                }
                // Só a do centro faz barulho (o estrondo grande vem do nosso som).
                if (i > 0) foreach (AudioSource a in effect.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
                effect.SetActive(true);
                Destroy(effect, 6f);
            }
        }

        // ------------------------------------------------------------------ animação

        private void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector3 c = transform.position;

            // Bola de fogo: cresce rápido, escurece e some.
            float grow = Mathf.Clamp01(t / .18f);
            float fade = Mathf.Clamp01(1f - (t - .25f) / .9f);
            if (fireball != null)
            {
                fireball.localScale = Vector3.one * (2.2f * scale * Mathf.Lerp(.2f, 1f, Mathf.Sqrt(grow)) * (.6f + .4f * fade));
                SetColor(fireMat, Color.Lerp(new Color(.5f, .08f, .02f), new Color(1f, .7f, .25f), fade) * (1f + 5f * fade));
                fireball.gameObject.SetActive(fade > .01f);
            }

            // Fumaça: sobe e cresce, depois some devagar.
            if (smoke != null)
            {
                float k = Mathf.Clamp01((t - .15f) / 1.5f);
                smoke.localScale = Vector3.one * (2.6f * scale * (.4f + k));
                smoke.localPosition = Vector3.up * (1.5f * scale * k);
                float alpha = Mathf.Clamp01(t / .4f) * Mathf.Clamp01((Life - t) / 2f) * .75f;
                SetColor(smokeMat, new Color(.08f, .07f, .06f, alpha));
            }

            // Onda de choque no chão.
            float rk = Mathf.Clamp01(t / .6f);
            float radius = Mathf.Lerp(.5f, 6f * scale, 1f - (1f - rk) * (1f - rk));
            ring.widthMultiplier = .35f * scale * (1f - rk);
            ring.enabled = rk < 1f;
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, .15f, Mathf.Sin(a) * radius));
            }

            // Estilhaços em brasa.
            float sparkFade = Mathf.Clamp01(1f - t / 1.6f);
            for (int i = 0; i < sparks.Count; i++)
            {
                Vector3 v = sparkVel[i] + Physics.gravity * dt;
                sparkVel[i] = v;
                Vector3 p = sparkPos[i] + v * dt;
                sparkPos[i] = p;
                sparks[i].SetPosition(0, p);
                sparks[i].SetPosition(1, p - v * .05f);
                sparks[i].widthMultiplier = .06f * sparkFade;
                sparks[i].enabled = sparkFade > .01f;
            }

            if (flashLight != null)
                flashLight.SetIntensity(80000f * scale * Mathf.Pow(Mathf.Clamp01(1f - t / .8f), 2f));

            screenFlash = Mathf.MoveTowards(screenFlash, 0f, dt * 2.2f);
            if (t >= Life) Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (screenFlash <= .01f) return;
            GUI.color = new Color(1f, .95f, .85f, screenFlash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            foreach (Material m in new[] { fireMat, smokeMat, ringMat, sparkMat }) if (m != null) Destroy(m);
        }

        // ------------------------------------------------------------------ som

        private void PlaySound(Vector3 c)
        {
            if (bigBoom == null) bigBoom = MakeBigBoom();
            // 3D: ouvido de longe.
            AudioSource far = gameObject.AddComponent<AudioSource>();
            far.spatialBlend = 1f; far.rolloffMode = AudioRolloffMode.Linear;
            far.minDistance = 15f; far.maxDistance = 220f; far.dopplerLevel = 0f;
            far.PlayOneShot(bigBoom, volume);
            // 2D: pancada no ouvido de quem está perto.
            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (local != null)
            {
                float near = Mathf.Clamp01(1f - Vector3.Distance(local.transform.position, c) / 120f);
                if (near > 0f)
                {
                    AudioSource close = gameObject.AddComponent<AudioSource>();
                    close.spatialBlend = 0f;
                    close.PlayOneShot(bigBoom, volume * near);
                }
            }
        }

        /// <summary>Estrondo sintetizado: estalo + grave + ronco longo + ecos, com saturação suave.</summary>
        private static AudioClip MakeBigBoom()
        {
            const int rate = 44100;
            const float duration = 3.2f;
            int n = (int)(rate * duration);
            float[] dry = new float[n];
            System.Random rng = new System.Random(4242);
            float lp1 = 0, lp2 = 0, lp3 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp1 += (noise - lp1) * .25f;  // corpo
                lp2 += (noise - lp2) * .02f;  // grave
                lp3 += (noise - lp3) * .006f; // ronco
                float crack = noise * Mathf.Exp(-t * 45f) * 1.1f;
                float body = lp1 * Mathf.Exp(-t * 6f) * 1.4f;
                float low = lp2 * Mathf.Exp(-t * 1.7f) * 6f;
                float sub = Mathf.Sin(2f * Mathf.PI * (48f - 18f * Mathf.Min(t, 1f)) * t) * Mathf.Exp(-t * 2.2f) * 1.1f;
                float rumble = lp3 * Mathf.Exp(-t * .9f) * 14f;
                dry[i] = crack + body + low + sub + rumble;
            }
            float[] data = new float[n];
            int e1 = (int)(.19f * rate), e2 = (int)(.43f * rate), e3 = (int)(.8f * rate);
            for (int i = 0; i < n; i++)
            {
                float s = dry[i];
                if (i >= e1) s += dry[i - e1] * .38f;
                if (i >= e2) s += dry[i - e2] * .22f;
                if (i >= e3) s += dry[i - e3] * .12f;
                data[i] = (float)Math.Tanh(s * 1.6f) * .98f;
            }
            AudioClip clip = AudioClip.Create("ChineloBigBoom", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ util

        private Transform Sphere(string name, Material material)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = name;
            Collider col = s.GetComponent<Collider>();
            if (col != null) Destroy(col);
            MeshRenderer mr = s.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            s.transform.SetParent(transform, false);
            s.transform.localScale = Vector3.zero;
            return s.transform;
        }

        private LineRenderer Line(string name, Material material, int points)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points;
            lr.sharedMaterial = material;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static Shader Unlit()
        {
            if (unlit != null) return unlit;
            unlit = Shader.Find("HDRP/Unlit");
            if (unlit == null)
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                    if (m != null && m.shader != null && m.shader.name == "HDRP/Unlit") { unlit = m.shader; break; }
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            return unlit;
        }

        private static Material Glow(Color color, float intensity)
        {
            Shader shader = Unlit();
            if (shader == null) return null;
            Material m = new Material(shader);
            SetColor(m, color * intensity);
            Validate(m);
            return m;
        }

        private static Material Transparent(Color color)
        {
            Shader shader = Unlit();
            if (shader == null) return null;
            Material m = new Material(shader);
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);
            if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColor(m, color);
            Validate(m);
            return m;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void Validate(Material m)
        {
            if (!m.shader.name.StartsWith("HDRP")) return;
            try { HDMaterial.ValidateMaterial(m); } catch (Exception) { }
        }
    }
}
