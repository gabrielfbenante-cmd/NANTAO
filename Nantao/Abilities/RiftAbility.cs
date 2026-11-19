using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Fenda: X lança uma fenda que desliza pelo chão pra frente e para em paredes.
    /// X de novo teleporta você até onde ela estiver. Some sozinha depois de um tempo.
    /// </summary>
    internal sealed class RiftAbility : Ability
    {
        private const byte EvSpawn = 1, EvTeleport = 2, EvRemove = 3;

        private RiftWalker _rift;
        private float _cooldownUntil;
        private string _msg;
        private float _msgUntil;
        private float _gesture;

        public RiftAbility(PlayerAgent agent) : base(agent) { }

        public override void Dispose() => Remove();

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (!CanAct()) return;
            if (_rift != null)
            {
                if ((Flags(P) & 1) != (_rift.Flags & 1)) { Fail("A fenda está em outro ambiente"); return; }
                Vector3 from = P.transform.position;
                Vector3 to = _rift.Ground;
                int flags = _rift.Flags;
                Teleport(from, to, flags);
                Send(EvTeleport, new AbilityPayload { A = from, B = to, I = flags });
                _cooldownUntil = Time.time + ModConfig.RiftCooldown.Value;
                return;
            }

            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f) { Fail($"Recarregando ({cd:0}s)"); return; }

            Vector3 start = P.transform.position + FlatForward() * 0.8f;
            Vector3 dir = FlatForward();
            int f = Flags(P);
            Spawn(start, dir, f);
            Send(EvSpawn, new AbilityPayload { A = start, B = dir, I = f });
        }

        public override void OwnerTick(float dt)
        {
            if (_rift != null && _rift.Expired)
            {
                Remove();
                Send(EvRemove);
                _cooldownUntil = Time.time + ModConfig.RiftCooldown.Value * 0.5f;
            }
        }

        private bool CanAct() =>
            !P.isPlayerDead && P.isPlayerControlled && !P.inSpecialInteractAnimation && !P.isClimbingLadder
            && P.inAnimationWithEnemy == null;

        private void Fail(string msg)
        {
            Agent.PlaySfx(Sounds.Fail, 0.6f);
            _msg = msg;
            _msgUntil = Time.time + 1.6f;
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = Color.Lerp(ModConfig.Rift, Color.white, 0.3f);
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            if (_rift != null)
            {
                float d = Vector3.Distance(P.transform.position, _rift.Ground);
                hud.Status($"[X] teleportar pra fenda · {d:0} m", c);
                hud.Bar(1f - _rift.Age / Mathf.Max(0.1f, ModConfig.RiftLifetime.Value), c);
                return;
            }
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                hud.Status($"Fenda recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.RiftCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else hud.Status("[X] lançar fenda", c);
        }

        // ---------------------------------------------------------------- rede / todos

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf) return;
            switch (type)
            {
                case EvSpawn: Spawn(p.A, p.B, p.I); break;
                case EvTeleport: Teleport(p.A, p.B, p.I); break;
                case EvRemove: Remove(); break;
            }
        }

        private void Spawn(Vector3 start, Vector3 dir, int flags)
        {
            Remove();
            _rift = RiftWalker.Spawn(start, dir, flags, LayerMask);
            _gesture = 0.4f;
            Agent.PlaySfx(Sounds.RiftOpen);
        }

        private void Teleport(Vector3 from, Vector3 to, int flags)
        {
            BurstFx.Spawn(from + Vector3.up * 0.3f, ModConfig.Rift, 0.9f, 0.45f);
            BurstFx.Spawn(to + Vector3.up * 0.3f, ModConfig.Rift, 1.1f, 0.55f);
            Sounds.PlayAt(from, Sounds.TpArrive, 0.6f);
            Sounds.PlayAt(to, Sounds.TpArrive);
            try
            {
                P.TeleportPlayer(to, false, 0f, false, true);
                ApplyFlags(P, flags);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Falha no teleporte: " + e.Message); }
            Remove();
        }

        private void Remove()
        {
            if (_rift != null) Object.Destroy(_rift.gameObject);
            _rift = null;
        }

        public override void LateTick(float dt)
        {
            if (_rift != null && (P.isPlayerDead || _rift.Age > ModConfig.RiftLifetime.Value + 2f)) Remove();
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.4f) * Mathf.PI);
            // Arremesso rasteiro (como jogar algo deslizando no chão).
            Agent.PoseArm(true,
                new Vector3(0.2f, -0.6f, 0.8f), new Vector3(0.05f, -0.4f, 1f),
                new Vector3(0.35f, -0.8f, 0.6f), new Vector3(0f, -0.4f, 1f), w, 0.8f);
        }
    }

    /// <summary>A fenda: rasgo vertical azul que desliza pelo chão.</summary>
    internal sealed class RiftWalker : MonoBehaviour
    {
        public int Flags { get; private set; }
        public float Age { get; private set; }
        public bool Expired => Age >= ModConfig.RiftLifetime.Value;
        /// <summary>Ponto do chão sob a fenda (pra onde o jogador vai).</summary>
        public Vector3 Ground { get; private set; }

        private Vector3 _dir;
        private bool _moving = true;
        private int _mask;
        private Material _mat, _core;
        private LineRenderer _edge;
        private LineRenderer[] _cracks;
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;

        public static RiftWalker Spawn(Vector3 start, Vector3 dir, int flags, int mask)
        {
            var go = new GameObject("NantaoRift");
            go.transform.position = start;
            var r = go.AddComponent<RiftWalker>();
            r._dir = dir.normalized;
            r.Flags = flags;
            r._mask = mask;
            r.Ground = start;
            r.Build();
            return r;
        }

        private void Build()
        {
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
            _mat = Mats.Glow(ModConfig.Rift, 3f);
            _core = Mats.Glow(Color.Lerp(ModConfig.Rift, Color.white, 0.7f), 4f);
            _edge = FxUtil.Line(transform, "Edge", _mat, 20, false);
            _edge.loop = true;
            _edge.widthMultiplier = 0.04f;
            _cracks = new LineRenderer[4];
            for (int i = 0; i < _cracks.Length; i++)
            {
                _cracks[i] = FxUtil.Line(transform, "Crack", _core, 6, false);
                _cracks[i].widthMultiplier = 0.02f;
            }
            _light = FxUtil.PointLight(transform, ModConfig.Rift, 4f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.9f;
            SnapToGround(transform.position);
        }

        private void SnapToGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var hit, 4f, _mask, QueryTriggerInteraction.Ignore))
            {
                Ground = hit.point;
                transform.position = hit.point;
            }
            else _moving = false; // borda/buraco: para
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;
            if (_moving && Age < ModConfig.RiftTravelTime.Value)
            {
                Vector3 step = _dir * ModConfig.RiftSpeed.Value * dt;
                Vector3 eye = transform.position + Vector3.up * 0.6f;
                if (Physics.Raycast(eye, _dir, step.magnitude + 0.4f, _mask, QueryTriggerInteraction.Ignore)) _moving = false;
                else SnapToGround(transform.position + step);
            }

            // Forma: elipse vertical irregular que "respira".
            float h = 1.7f, w = 0.35f + 0.05f * Mathf.Sin(Age * 6f);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                float jag = 1f + 0.12f * Mathf.PerlinNoise(i * 0.7f, Age * 4f);
                _edge.SetPosition(i, new Vector3(Mathf.Sin(a) * w * jag, h * 0.5f + Mathf.Cos(a) * h * 0.5f, 0f));
            }
            for (int c = 0; c < _cracks.Length; c++)
            {
                for (int i = 0; i < 6; i++)
                {
                    float k = i / 5f;
                    float x = (Mathf.PerlinNoise(c * 3.1f, Age * 8f + k * 3f) - 0.5f) * w * 1.2f;
                    _cracks[c].SetPosition(i, new Vector3(x, 0.15f + k * (h - 0.3f), 0f));
                }
            }
            float fade = Mathf.Clamp01((ModConfig.RiftLifetime.Value - Age) / 1f);
            if (_light != null) _light.SetIntensity(200f * fade * (0.8f + 0.2f * Mathf.Sin(Age * 10f)));
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_core != null) Destroy(_core);
        }
    }
}
