using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Brasa: X arremessa um coquetel que vira uma área de fogo no chão.
    /// O fogo acerta monstros periodicamente, cura quem jogou e (opcional) machuca outros jogadores.
    /// </summary>
    internal sealed class FireAbility : Ability
    {
        private const byte EvThrow = 1;

        private float _cooldownUntil;
        private float _gesture;
        private string _msg;
        private float _msgUntil;

        public FireAbility(PlayerAgent agent) : base(agent) { }

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled || P.inSpecialInteractAnimation) return;
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                Agent.PlaySfx(Sounds.Fail, 0.6f);
                _msg = $"Recarregando ({cd:0}s)";
                _msgUntil = Time.time + 1.5f;
                return;
            }
            Vector3 from = HandPosition();
            Vector3 to = AimPoint(ModConfig.FireRange.Value);
            Throw(from, to);
            Send(EvThrow, new AbilityPayload { A = from, B = to });
            _cooldownUntil = Time.time + ModConfig.FireCooldown.Value;
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = Color.Lerp(ModConfig.Fire, Color.white, 0.2f);
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                hud.Status($"Coquetel recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.FireCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else hud.Status("[X] arremessar coquetel de fogo", c);
            if (FireZone.LocalHealing) hud.Vignette(ModConfig.Fire, 0.3f);
        }

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf || type != EvThrow) return;
            Throw(p.A, p.B);
        }

        private void Throw(Vector3 from, Vector3 to)
        {
            _gesture = 0.45f;
            Agent.PlaySfx(Sounds.SmokeThrow);
            var owner = P;
            ArcProjectile.Spawn(from, to, 0.6f, ModConfig.Fire, pos => FireZone.Spawn(pos, owner));
        }

        public override void LateTick(float dt)
        {
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.45f) * Mathf.PI);
            Agent.PoseArm(true,
                new Vector3(0.2f, 0.4f, 0.8f), new Vector3(0.05f, 0.6f, 1f),
                new Vector3(0.3f, -0.2f, 1f), new Vector3(0f, 0.25f, 1f), w, 0.5f);
        }
    }

    /// <summary>Área de fogo: chamas de linhas tremulando, luz laranja e dano/cura periódicos.</summary>
    internal sealed class FireZone : MonoBehaviour
    {
        /// <summary>O jogador local está se curando no próprio fogo (pro HUD).</summary>
        public static bool LocalHealing;

        private PlayerControllerB _owner;
        private float _radius, _duration, _t;
        private float _playerTick, _enemyTick;
        private Material _orange, _yellow, _red;
        private readonly List<LineRenderer> _flames = new List<LineRenderer>();
        private readonly List<Vector3> _bases = new List<Vector3>();
        private readonly List<float> _seeds = new List<float>();
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;
        private AudioSource _audio;
        private float _emberTimer, _smokeTimer;
        private Material _scorch;

        public static void Spawn(Vector3 pos, PlayerControllerB owner)
        {
            var go = new GameObject("NantaoFire");
            go.transform.position = pos + Vector3.up * 0.02f;
            go.AddComponent<FireZone>().Build(owner);
        }

        private void Build(PlayerControllerB owner)
        {
            _owner = owner;
            _radius = Mathf.Max(0.5f, ModConfig.FireRadius.Value);
            _duration = Mathf.Max(1f, ModConfig.FireDuration.Value);
            Color c = ModConfig.Fire;
            _orange = Mats.Glow(c, 3f);
            _yellow = Mats.Glow(Color.Lerp(c, new Color(1f, 0.9f, 0.3f), 0.6f), 4f);
            _red = Mats.Glow(Color.Lerp(c, new Color(0.6f, 0.05f, 0f), 0.5f), 2f);

            FxUtil.Ring(transform, "Edge", _red, _radius, 0.06f);
            // Chão queimado (disco escuro rente ao chão).
            _scorch = Mats.Glow(new Color(.05f, .03f, .02f), 1f);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var dc = disc.GetComponent<Collider>();
            if (dc != null) Destroy(dc);
            var dmr = disc.GetComponent<MeshRenderer>();
            dmr.sharedMaterial = _scorch;
            dmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(_radius * 2.1f, .015f, _radius * 2.1f);
            var rng = new System.Random(GetInstanceID());
            int count = Mathf.Clamp((int)(_radius * _radius * 5f), 18, 60);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * _radius;
                var basePos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var mat = i % 3 == 0 ? _yellow : i % 3 == 1 ? _orange : _red;
                var lr = FxUtil.Line(transform, "Flame", mat, 4, false);
                lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                _flames.Add(lr);
                _bases.Add(basePos);
                _seeds.Add((float)rng.NextDouble() * 100f);
            }

            _light = FxUtil.PointLight(transform, c, _radius * 3f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.8f;

            _audio = gameObject.AddComponent<AudioSource>();
            Sounds.Setup3D(_audio);
            _audio.clip = Sounds.FireLoop;
            _audio.loop = true;
            _audio.volume = Mathf.Clamp01(ModConfig.Volume.Value) * 0.9f;
            _audio.Play();
            Sounds.PlayAt(transform.position, Sounds.Ignite);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            float life = Mathf.Clamp01(Mathf.Min(_t / 0.3f, (_duration - _t) / 1f));

            for (int i = 0; i < _flames.Count; i++)
            {
                float s = _seeds[i];
                float h = (0.35f + 0.55f * Mathf.PerlinNoise(s, _t * 3f)) * life;
                Vector3 b = _bases[i];
                float sway = (Mathf.PerlinNoise(s + 10f, _t * 4f) - 0.5f) * 0.25f;
                for (int k = 0; k < 4; k++)
                {
                    float f = k / 3f;
                    _flames[i].SetPosition(k, b + new Vector3(sway * f * f, h * f, sway * 0.6f * f));
                }
                _flames[i].widthMultiplier = 0.12f * life;
            }
            if (_light != null) _light.SetIntensity(900f * life * (0.75f + 0.25f * Mathf.PerlinNoise(_t * 9f, 0f)));
            if (_audio != null) _audio.volume = Mathf.Clamp01(ModConfig.Volume.Value) * 0.9f * life;

            // Brasas subindo e fumaça saindo do fogo.
            if (life > .2f)
            {
                _emberTimer -= dt;
                if (_emberTimer <= 0f)
                {
                    _emberTimer = .07f;
                    float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.value) * _radius;
                    Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * r, .2f, Mathf.Sin(a) * r);
                    Color ember = Color.Lerp(ModConfig.Fire, new Color(1f, .85f, .3f), UnityEngine.Random.value);
                    LineParticles.Burst(p, ember, 2, .5f, Vector3.up * UnityEngine.Random.Range(1.5f, 2.8f), -.3f, 1.4f, .016f, .06f, 0f, 5f, .4f);
                }
                _smokeTimer -= dt;
                if (_smokeTimer <= 0f)
                {
                    _smokeTimer = .45f;
                    float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.value) * _radius * .8f;
                    SmokePuff.Spawn(transform.position + new Vector3(Mathf.Cos(a) * r, .7f, Mathf.Sin(a) * r),
                        new Color(.11f, .1f, .09f, .5f), 1.1f + UnityEngine.Random.value * .6f, 2.4f, .9f);
                }
            }

            ApplyEffects(dt);
            if (_t >= _duration)
            {
                LocalHealing = false;
                Destroy(gameObject);
            }
        }

        private bool Inside(Vector3 p)
        {
            Vector3 d = p - transform.position;
            if (d.y < -1f || d.y > 2.5f) return false;
            d.y = 0f;
            return d.magnitude <= _radius;
        }

        private void ApplyEffects(float dt)
        {
            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;

            // Jogador local: dano (fogo dos outros) ou cura (o seu fogo). Cada um aplica em si mesmo.
            _playerTick -= dt;
            bool healing = false;
            if (local != null && !local.isPlayerDead && Inside(local.transform.position))
            {
                bool mine = local == _owner;
                healing = mine && ModConfig.FireSelfHeal.Value > 0;
                if (_playerTick <= 0f)
                {
                    _playerTick = 1f;
                    if (healing)
                    {
                        local.health = Mathf.Min(100, local.health + ModConfig.FireSelfHeal.Value);
                        if (local.health >= 20) local.criticallyInjured = false;
                        try { HUDManager.Instance.UpdateHealthUI(local.health, false); } catch { /* HUD indisponível */ }
                    }
                    else if (!mine && ModConfig.FireFriendlyFire.Value)
                    {
                        local.DamagePlayer(ModConfig.FirePlayerDamage.Value, true, true, CauseOfDeath.Burning, 0, false, Vector3.zero);
                    }
                }
            }
            if (local == _owner) LocalHealing = healing;

            // Monstros: só o computador de quem jogou manda os acertos (o jogo sincroniza).
            if (local == null || local != _owner) return;
            _enemyTick -= dt;
            if (_enemyTick > 0f) return;
            _enemyTick = Mathf.Max(0.2f, ModConfig.FireEnemyHitInterval.Value);
            var rm = RoundManager.Instance;
            if (rm == null || rm.SpawnedEnemies == null) return;
            foreach (var e in rm.SpawnedEnemies)
            {
                if (e == null || e.isEnemyDead || !Inside(e.transform.position)) continue;
                try { e.HitEnemyOnLocalClient(1, (e.transform.position - transform.position).normalized, _owner, true, -1); }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Fogo/monstro: " + ex.Message); }
            }
        }

        private void OnDestroy()
        {
            if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController == _owner) LocalHealing = false;
            if (_orange != null) Destroy(_orange);
            if (_yellow != null) Destroy(_yellow);
            if (_red != null) Destroy(_red);
            if (_scorch != null) Destroy(_scorch);
        }
    }
}
