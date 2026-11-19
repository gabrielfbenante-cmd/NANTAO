using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Uivo: X solta um lobo de energia que corre pra frente pelo chão.
    /// Se chegar perto de um monstro (ou se você apertar X de novo), ele dá o bote
    /// e atordoa os monstros em volta.
    /// </summary>
    internal sealed class WolfAbility : Ability
    {
        private const byte EvSpawn = 1, EvPounce = 2, EvStun = 3, EvEnd = 4;

        private WolfSpirit _wolf;
        private float _cooldownUntil;
        private float _gesture;
        private string _msg;
        private float _msgUntil;

        public WolfAbility(PlayerAgent agent) : base(agent) { }

        public override void Dispose() => EndWolf();

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled || P.inSpecialInteractAnimation) return;
            if (_wolf != null && !_wolf.Pouncing) { Pounce(); return; }
            if (_wolf != null) return;

            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f) { Fail($"Recarregando ({cd:0}s)"); return; }

            Vector3 start = P.transform.position + FlatForward() * 1.0f;
            Vector3 dir = FlatForward();
            SpawnWolf(start, dir);
            Send(EvSpawn, new AbilityPayload { A = start, B = dir });
            _cooldownUntil = Time.time + ModConfig.WolfCooldown.Value;
        }

        public override void OwnerTick(float dt)
        {
            if (_wolf == null) return;
            if (!_wolf.Pouncing)
            {
                // Monstro perto → bote automático.
                foreach (var _ in EnemiesNear(_wolf.transform.position, 2.2f)) { Pounce(); return; }
                if (_wolf.Age >= ModConfig.WolfMaxTime.Value || _wolf.Stopped)
                {
                    EndWolf();
                    Send(EvEnd);
                }
            }
        }

        private void Pounce()
        {
            Vector3 pos = _wolf.transform.position;
            _wolf.Pounce();
            Send(EvPounce, new AbilityPayload { A = pos });

            // Atordoa monstros no raio: manda um evento por monstro (todos aplicam, como a granada do jogo).
            foreach (var e in new List<EnemyAI>(EnemiesNear(pos, ModConfig.WolfStunRadius.Value)))
            {
                if (e.thisNetworkObject == null) continue;
                Send(EvStun, new AbilityPayload { I = (int)e.thisNetworkObject.NetworkObjectId });
            }
        }

        private void Fail(string msg)
        {
            Agent.PlaySfx(Sounds.Fail, 0.6f);
            _msg = msg;
            _msgUntil = Time.time + 1.6f;
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = Color.Lerp(ModConfig.Wolf, Color.white, 0.25f);
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            if (_wolf != null && !_wolf.Pouncing) { hud.Status("[X] bote agora!", c); return; }
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                hud.Status($"Lobo recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.WolfCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else hud.Status("[X] soltar o lobo", c);
        }

        // ---------------------------------------------------------------- rede / todos

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (type == EvStun)
            {
                // Vem pra todos (inclusive eu): aplica o atordoamento localmente.
                var e = EnemyById(p.I);
                if (e != null && !e.isEnemyDead && e.enemyType != null && e.enemyType.canBeStunned)
                {
                    try { e.SetEnemyStunned(true, ModConfig.WolfStunTime.Value, P); }
                    catch (System.Exception ex) { Plugin.Log.LogWarning("Atordoar: " + ex.Message); }
                }
                return;
            }
            if (fromSelf) return;
            switch (type)
            {
                case EvSpawn: SpawnWolf(p.A, p.B); break;
                case EvPounce: if (_wolf != null) { _wolf.transform.position = p.A; _wolf.Pounce(); } break;
                case EvEnd: EndWolf(); break;
            }
        }

        private void SpawnWolf(Vector3 start, Vector3 dir)
        {
            EndWolf();
            _wolf = WolfSpirit.Spawn(start, dir, LayerMask);
            _gesture = 0.5f;
            Agent.PlaySfx(Sounds.Howl);
        }

        private void EndWolf()
        {
            if (_wolf != null) _wolf.Vanish();
            _wolf = null;
        }

        public override void LateTick(float dt)
        {
            if (_wolf != null && _wolf.Done) _wolf = null; // terminou o bote
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.5f) * Mathf.PI);
            // Braço esticado pra frente, mão aberta: "vai!"
            Agent.PoseArm(true,
                new Vector3(0.15f, 0f, 1f), new Vector3(0.05f, 0.05f, 1f),
                new Vector3(0.3f, -0.45f, 1f), new Vector3(0f, -0.1f, 1f), w, 1f);
        }
    }

    /// <summary>
    /// Lobo feito de linhas de energia (visual próprio do mod): corre rente ao chão,
    /// pernas em galope, cauda e rastro. Ao dar o bote, salta e explode em luz.
    /// </summary>
    internal sealed class WolfSpirit : MonoBehaviour
    {
        public float Age { get; private set; }
        public bool Pouncing { get; private set; }
        public bool Stopped { get; private set; }
        public bool Done { get; private set; }

        private Vector3 _dir;
        private int _mask;
        private float _pounceT;
        private Material _mat, _bright;
        private LineRenderer _spine, _head, _tail;
        private LineRenderer[] _legs;
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;
        private Transform _body;

        public static WolfSpirit Spawn(Vector3 start, Vector3 dir, int mask)
        {
            var go = new GameObject("NantaoWolf");
            go.transform.position = start;
            var w = go.AddComponent<WolfSpirit>();
            w._dir = dir.normalized;
            w._mask = mask;
            w.Build();
            return w;
        }

        private void Build()
        {
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
            _mat = Mats.Glow(ModConfig.Wolf, 3f);
            _bright = Mats.Glow(Color.Lerp(ModConfig.Wolf, Color.white, 0.6f), 4f);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            _spine = FxUtil.Line(_body, "Spine", _mat, 5, false);
            _spine.widthMultiplier = 0.09f;
            _head = FxUtil.Line(_body, "Head", _bright, 6, false);
            _head.widthMultiplier = 0.04f;
            _tail = FxUtil.Line(_body, "Tail", _mat, 5, false);
            _tail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            _tail.widthMultiplier = 0.06f;
            _legs = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                _legs[i] = FxUtil.Line(_body, "Leg", _mat, 3, false);
                _legs[i].widthMultiplier = 0.035f;
            }
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _mat;
            trail.time = 0.35f;
            trail.widthMultiplier = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _light = FxUtil.PointLight(transform, ModConfig.Wolf, 4f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.5f;
            Snap(transform.position);
        }

        private void Snap(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var hit, 4f, _mask, QueryTriggerInteraction.Ignore))
                transform.position = hit.point;
            else Stopped = true; // borda
        }

        public void Pounce()
        {
            if (Pouncing) return;
            Pouncing = true;
            _pounceT = 0f;
            Sounds.PlayAt(transform.position, Sounds.RunOn);
        }

        public void Vanish()
        {
            BurstFx.Spawn(transform.position + Vector3.up * 0.4f, ModConfig.Wolf, 0.6f, 0.35f);
            Destroy(gameObject);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;
            float lift = 0f;

            if (Pouncing)
            {
                _pounceT += dt;
                float k = Mathf.Clamp01(_pounceT / 0.35f);
                lift = Mathf.Sin(k * Mathf.PI) * 1.1f;
                transform.position += _dir * 4f * dt;
                if (k >= 1f)
                {
                    BurstFx.Spawn(transform.position + Vector3.up * 0.4f, ModConfig.Wolf, ModConfig.WolfStunRadius.Value / 2.5f, 0.5f);
                    Done = true;
                    Destroy(gameObject);
                    return;
                }
            }
            else if (!Stopped)
            {
                Vector3 step = _dir * ModConfig.WolfSpeed.Value * dt;
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, _dir, step.magnitude + 0.5f, _mask, QueryTriggerInteraction.Ignore))
                    Stopped = true;
                else Snap(transform.position + step);
            }

            // Galope: corpo sobe/desce, pernas alternam.
            float g = Age * 14f;
            float bob = Mathf.Abs(Mathf.Sin(g)) * 0.08f;
            _body.localPosition = Vector3.up * (lift + bob);
            _body.localRotation = Quaternion.Euler(Pouncing ? -25f : Mathf.Sin(g) * 4f, 0f, 0f);

            const float hip = 0.55f, len = 0.9f;
            _spine.SetPositions(new[]
            {
                new Vector3(0f, hip, -len * 0.5f), new Vector3(0f, hip + 0.05f, -len * 0.2f),
                new Vector3(0f, hip + 0.06f, len * 0.1f), new Vector3(0f, hip + 0.1f, len * 0.4f),
                new Vector3(0f, hip + 0.2f, len * 0.55f),
            });
            // Cabeça: focinho + duas orelhas pontudas.
            Vector3 hd = new Vector3(0f, hip + 0.25f, len * 0.6f);
            _head.SetPositions(new[]
            {
                hd + new Vector3(-0.08f, 0.15f, -0.02f), hd + new Vector3(-0.05f, 0f, 0f), hd + new Vector3(0f, -0.04f, 0.28f),
                hd + new Vector3(0.05f, 0f, 0f), hd + new Vector3(0.08f, 0.15f, -0.02f), hd + new Vector3(0f, 0.02f, 0.05f),
            });
            for (int i = 0; i < 5; i++)
            {
                float k = i / 4f;
                float wave = Mathf.Sin(g * 0.5f - k * 2f) * 0.12f * k;
                _tail.SetPosition(i, new Vector3(wave, hip + 0.05f + k * 0.15f, -len * 0.5f - k * 0.45f));
            }
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float side = i % 2 == 0 ? -0.1f : 0.1f;
                float phase = g + (front ? 0f : Mathf.PI) + (i % 2) * 0.6f;
                float z = front ? len * 0.35f : -len * 0.4f;
                float swing = Pouncing ? (front ? 0.35f : -0.35f) : Mathf.Sin(phase) * 0.25f;
                _legs[i].SetPosition(0, new Vector3(side, hip, z));
                _legs[i].SetPosition(1, new Vector3(side, hip * 0.45f, z + swing * 0.5f));
                _legs[i].SetPosition(2, new Vector3(side, Pouncing ? hip * 0.2f : Mathf.Max(0f, Mathf.Cos(phase)) * 0.1f, z + swing));
            }
            if (_light != null) _light.SetIntensity(250f * (0.8f + 0.2f * Mathf.Sin(Age * 12f)));
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_bright != null) Destroy(_bright);
        }
    }
}
