using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Nantao
{
    /// <summary>
    /// Névoa: X manda uma fumaça pro ponto onde você mira. Tem cargas que recarregam.
    /// A fumaça bloqueia a visão dos jogadores e (opcional) dos monstros.
    /// </summary>
    internal sealed class SmokeAbility : Ability
    {
        private const byte EvThrow = 1;
        private const float Travel = 0.55f;

        private int _charges;
        private float _rechargeTimer;
        private float _gesture;
        private string _msg;
        private float _msgUntil;

        public SmokeAbility(PlayerAgent agent) : base(agent)
        {
            _charges = Mathf.Max(1, ModConfig.SmokeCharges.Value);
        }

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled || P.inSpecialInteractAnimation) return;
            if (_charges <= 0)
            {
                Agent.PlaySfx(Sounds.Fail, 0.6f);
                _msg = $"Sem fumaça ({_rechargeTimer:0}s)";
                _msgUntil = Time.time + 1.5f;
                return;
            }

            Vector3 target = AimPoint(ModConfig.SmokeRange.Value, 1.2f);
            Vector3 from = HandPosition();
            _charges--;
            Throw(from, target);
            Send(EvThrow, new AbilityPayload { A = from, B = target });
        }

        public override void OwnerTick(float dt)
        {
            int max = Mathf.Max(1, ModConfig.SmokeCharges.Value);
            if (_charges >= max) { _rechargeTimer = ModConfig.SmokeRecharge.Value; return; }
            _rechargeTimer -= dt;
            if (_rechargeTimer <= 0f)
            {
                _charges++;
                _rechargeTimer = ModConfig.SmokeRecharge.Value;
            }
        }

        public override void HudTick(AgentHud hud)
        {
            var c = Color.Lerp(ModConfig.Smoke, Color.white, 0.55f);
            int max = Mathf.Max(1, ModConfig.SmokeCharges.Value);
            if (_msg != null && Time.time < _msgUntil) hud.Status(_msg, new Color(1f, 0.55f, 0.35f));
            else hud.Status($"[X] fumaça onde você mira · cargas {_charges}/{max}", c);
            if (_charges < max) hud.Bar(1f - _rechargeTimer / Mathf.Max(0.1f, ModConfig.SmokeRecharge.Value), c);
        }

        // ---------------------------------------------------------------- rede / todos

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf || type != EvThrow) return;
            Throw(p.A, p.B);
        }

        private void Throw(Vector3 from, Vector3 to)
        {
            _gesture = 0.45f;
            Agent.PlaySfx(Sounds.SmokeThrow);
            ArcProjectile.Spawn(from, to, Travel, Color.Lerp(ModConfig.Smoke, Color.white, 0.6f), SmokeCloud.Spawn);
        }

        public override void LateTick(float dt)
        {
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.45f) * Mathf.PI);
            // Braço direito aponta pra frente (lançando).
            Agent.PoseArm(true,
                new Vector3(0.15f, 0.1f, 1f), new Vector3(0.05f, 0.15f, 1f),
                new Vector3(0.3f, -0.4f, 1f), new Vector3(0f, -0.05f, 1f), w, 0.8f);
        }
    }

    /// <summary>
    /// Nuvem de fumaça: neblina volumétrica do HDRP (a mesma tecnologia da neblina do jogo)
    /// e/ou camadas de esferas translúcidas. Cresce, dura um tempo e some.
    /// </summary>
    internal sealed class SmokeCloud : MonoBehaviour
    {
        internal static readonly List<SmokeCloud> Active = new List<SmokeCloud>();

        private float _radius;
        private float _duration;
        private float _t;
        private LocalVolumetricFog _fog;
        private float _fogBaseFreePath;
        private readonly List<Transform> _puffs = new List<Transform>();
        private readonly List<Vector3> _puffOffsets = new List<Vector3>();
        private readonly List<float> _puffSizes = new List<float>();
        private Material _puffMat;
        private Color _puffColor;

        private static Texture3D _mask;

        /// <summary>Raio atual (cresce ao nascer, diminui ao sumir).</summary>
        public float CurrentRadius { get; private set; }

        public static void Spawn(Vector3 pos)
        {
            var go = new GameObject("NantaoSmoke");
            go.transform.position = pos;
            go.AddComponent<SmokeCloud>().Build();
            Sounds.PlayAt(pos, Sounds.SmokeBloom);
        }

        private void Build()
        {
            _radius = Mathf.Max(0.5f, ModConfig.SmokeRadius.Value);
            _duration = Mathf.Max(1f, ModConfig.SmokeDuration.Value);
            string mode = (ModConfig.SmokeMode.Value ?? "ambos").Trim().ToLowerInvariant();
            bool volumetric = mode != "esferas";
            bool spheres = mode != "volumetrica" && mode != "volumétrica";

            if (volumetric) BuildFog();
            if (spheres || _fog == null) BuildPuffs();

            Active.Add(this);
        }

        private void BuildFog()
        {
            try
            {
                _fog = gameObject.AddComponent<LocalVolumetricFog>();
                var prm = _fog.parameters;
                Color c = ModConfig.Smoke;
                prm.albedo = c;
                _fogBaseFreePath = 0.6f;
                prm.meanFreePath = 50f; // começa rala e engrossa
                prm.size = Vector3.one * (_radius * 2f);
                prm.volumeMask = Mask();
                prm.textureScrollingSpeed = new Vector3(0.02f, 0.05f, 0.03f);
                prm.textureTiling = Vector3.one;
                prm.distanceFadeStart = 10000f;
                prm.distanceFadeEnd = 10000f;
                _fog.parameters = prm;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Neblina volumétrica indisponível, usando esferas: " + e.Message);
                if (_fog != null) Destroy(_fog);
                _fog = null;
            }
        }

        /// <summary>Textura 3D com densidade esférica + ruído (borda macia e irregular).</summary>
        private static Texture3D Mask()
        {
            if (_mask != null) return _mask;
            const int n = 32;
            var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "NantaoSmokeMask" };
            var px = new Color32[n * n * n];
            var rng = new System.Random(99);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector3 p = new Vector3(x, y, z) / (n - 1) * 2f - Vector3.one;
                float r = p.magnitude;
                float noise = Mathf.PerlinNoise(x * 0.18f + ox + z * 0.07f, y * 0.18f + oy + z * 0.11f);
                float d = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, r + (noise - 0.5f) * 0.35f));
                byte b = (byte)(d * 255);
                px[(z * n + y) * n + x] = new Color32(b, b, b, b);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _mask = tex;
        }

        private void BuildPuffs()
        {
            _puffColor = ModConfig.Smoke;
            _puffMat = Mats.Transparent(new Color(_puffColor.r, _puffColor.g, _puffColor.b, 0.0f));
            if (_puffMat == null) return;
            var rng = new System.Random(GetInstanceID());
            for (int i = 0; i < 9; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var col = s.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var mr = s.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _puffMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                s.transform.SetParent(transform, false);
                Vector3 off = i == 0 ? Vector3.zero
                    : new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.4f) * 0.6f, (float)rng.NextDouble() - 0.5f) * _radius * 0.8f;
                _puffs.Add(s.transform);
                _puffOffsets.Add(off);
                _puffSizes.Add(i == 0 ? 1.8f : 1.0f + (float)rng.NextDouble() * 0.6f);
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            const float grow = 0.9f, fade = 1.6f;
            float k;
            if (_t < grow) k = Mathf.SmoothStep(0f, 1f, _t / grow);
            else if (_t > _duration - fade) k = Mathf.Clamp01((_duration - _t) / fade);
            else k = 1f;

            CurrentRadius = _radius * Mathf.Lerp(0.3f, 1f, Mathf.Min(1f, _t / grow)) * (k > 0.05f ? 1f : 0f);

            if (_fog != null)
            {
                var prm = _fog.parameters;
                // meanFreePath menor = mais denso.
                prm.meanFreePath = Mathf.Lerp(40f, _fogBaseFreePath, k);
                prm.size = Vector3.one * (2f * _radius * Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, _t / grow)));
                _fog.parameters = prm;
            }

            if (_puffMat != null)
            {
                Mats.SetColor(_puffMat, new Color(_puffColor.r, _puffColor.g, _puffColor.b, 0.42f * k));
                for (int i = 0; i < _puffs.Count; i++)
                {
                    float wob = 1f + 0.06f * Mathf.Sin(_t * 0.9f + i * 1.7f);
                    float size = _radius * _puffSizes[i] * Mathf.Lerp(0.35f, 1f, Mathf.Min(1f, _t / grow)) * wob;
                    _puffs[i].localScale = Vector3.one * size;
                    _puffs[i].localPosition = _puffOffsets[i] + Vector3.up * (0.15f * Mathf.Sin(_t * 0.5f + i));
                }
            }

            if (_t >= _duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            if (_puffMat != null) Destroy(_puffMat);
        }

        /// <summary>O segmento a→b atravessa alguma fumaça ativa?</summary>
        public static bool Blocks(Vector3 a, Vector3 b)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var s = Active[i];
                if (s == null) continue;
                float r = s.CurrentRadius * 0.85f;
                if (r <= 0.1f) continue;
                if (SegmentHitsSphere(a, b, s.transform.position, r)) return true;
            }
            return false;
        }

        /// <summary>Quão dentro de uma fumaça o ponto está (0 = fora, 1 = no centro).</summary>
        public static float Inside(Vector3 p)
        {
            float best = 0f;
            for (int i = 0; i < Active.Count; i++)
            {
                var s = Active[i];
                if (s == null || s.CurrentRadius <= 0.1f) continue;
                float d = Vector3.Distance(p, s.transform.position) / (s.CurrentRadius * 0.9f);
                best = Mathf.Max(best, Mathf.Clamp01(1.25f - d));
            }
            return Mathf.Clamp01(best);
        }

        private static bool SegmentHitsSphere(Vector3 a, Vector3 b, Vector3 c, float r)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(c - a, ab) / len2) : 0f;
            return (a + ab * t - c).sqrMagnitude <= r * r;
        }
    }
}
