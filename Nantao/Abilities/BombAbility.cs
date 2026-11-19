using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Salva: X joga várias bombinhas que caem espalhadas no ponto onde você mira,
    /// piscam por um instante e explodem (explosão de verdade do jogo). Usos limitados por rodada.
    /// </summary>
    internal sealed class BombAbility : Ability
    {
        private const byte EvThrow = 1;

        private int _usesLeft;
        private float _gesture;
        private string _msg;
        private float _msgUntil;

        public BombAbility(PlayerAgent agent) : base(agent)
        {
            _usesLeft = Mathf.Max(0, ModConfig.BombUsesPerRound.Value);
        }

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled || P.inSpecialInteractAnimation) return;
            if (_usesLeft <= 0)
            {
                Agent.PlaySfx(Sounds.Fail, 0.6f);
                _msg = "Sem bombinhas nesta rodada (recarrega em órbita)";
                _msgUntil = Time.time + 1.8f;
                return;
            }
            _usesLeft--;
            Vector3 from = HandPosition();
            Vector3 target = AimPoint(ModConfig.BombRange.Value);
            int seed = Random.Range(1, int.MaxValue);
            Throw(from, target, seed);
            Send(EvThrow, new AbilityPayload { A = from, B = target, I = seed });
        }

        public override void OwnerTick(float dt)
        {
            if (EnteredOrbit()) _usesLeft = Mathf.Max(0, ModConfig.BombUsesPerRound.Value);
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = Color.Lerp(ModConfig.Bomb, Color.white, 0.3f);
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            int max = Mathf.Max(1, ModConfig.BombUsesPerRound.Value);
            hud.Status(_usesLeft > 0 ? $"[X] jogar bombinhas · {_usesLeft}/{max} nesta rodada" : "Bombinhas usadas (recarrega em órbita)",
                _usesLeft > 0 ? c : new Color(0.7f, 0.7f, 0.7f));
        }

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf || type != EvThrow) return;
            Throw(p.A, p.B, p.I);
        }

        /// <summary>Mesma semente em todos os PCs → bombinhas caem nos mesmos lugares.</summary>
        private void Throw(Vector3 from, Vector3 target, int seed)
        {
            _gesture = 0.45f;
            Agent.PlaySfx(Sounds.SmokeThrow);
            var rng = new System.Random(seed);
            int n = Mathf.Clamp(ModConfig.BombCount.Value, 1, 8);
            float spread = Mathf.Max(0f, ModConfig.BombSpread.Value);
            int mask = LayerMask;
            for (int i = 0; i < n; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = i == 0 ? 0f : spread * (0.5f + 0.5f * (float)rng.NextDouble());
                Vector3 p = target + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, mask, QueryTriggerInteraction.Ignore)) p = hit.point;
                float travel = 0.55f + i * 0.08f;
                float fuse = ModConfig.BombFuse.Value + i * 0.15f;
                ArcProjectile.Spawn(from, p, travel, ModConfig.Bomb, pos => LittleBomb.Spawn(pos, fuse), 0.8f);
            }
        }

        public override void LateTick(float dt)
        {
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.45f) * Mathf.PI);
            Agent.PoseArm(true,
                new Vector3(0.2f, 0.5f, 0.7f), new Vector3(0.05f, 0.7f, 0.9f),
                new Vector3(0.3f, -0.1f, 1f), new Vector3(0f, 0.35f, 1f), w, 0.5f);
        }
    }

    /// <summary>Bombinha no chão: pisca cada vez mais rápido e explode.</summary>
    internal sealed class LittleBomb : MonoBehaviour
    {
        private float _fuse, _t, _nextBeep;
        private Material _mat;
        private LineRenderer _ring, _core;
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;

        public static void Spawn(Vector3 pos, float fuse)
        {
            var go = new GameObject("NantaoBomb");
            go.transform.position = pos + Vector3.up * 0.05f;
            var b = go.AddComponent<LittleBomb>();
            b._fuse = Mathf.Max(0.1f, fuse);
            b.Build();
        }

        private void Build()
        {
            _mat = Mats.Glow(ModConfig.Bomb, 3f);
            _ring = FxUtil.Ring(transform, "Ring", _mat, 0.25f, 0.03f, 20);
            _core = FxUtil.Line(transform, "Core", _mat, 2, false);
            _core.widthMultiplier = 0.16f;
            _core.SetPosition(0, Vector3.up * 0.02f);
            _core.SetPosition(1, Vector3.up * 0.16f);
            _light = FxUtil.PointLight(transform, ModConfig.Bomb, 3f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.3f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _fuse);
            float rate = Mathf.Lerp(3f, 14f, k);
            bool on = Mathf.Sin(_t * rate * Mathf.PI * 2f) > 0f;
            _core.enabled = on;
            _ring.widthMultiplier = 0.03f + 0.04f * k;
            if (_light != null) _light.SetIntensity(on ? 300f : 40f);
            if (Time.time >= _nextBeep)
            {
                _nextBeep = Time.time + 1f / rate * 2f;
                Sounds.PlayAt(transform.position, Sounds.Beep, 0.5f);
            }
            if (_t < _fuse) return;

            Explode();
            Destroy(gameObject);
        }

        private void Explode()
        {
            bool ff = ModConfig.BombFriendlyFire.Value;
            try
            {
                // Explosão do próprio jogo (efeito, som, empurrão e dano em monstros/jogadores).
                Landmine.SpawnExplosion(transform.position + Vector3.up * 0.3f, true,
                    0f, ModConfig.BombRadius.Value, ff ? ModConfig.BombDamage.Value : 0, ff ? 6f : 0f, null, false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Explosão do jogo falhou, usando efeito próprio: " + e.Message);
                BurstFx.Spawn(transform.position, ModConfig.Bomb, 1.5f, 0.5f);
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
