using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Breu: X → você se desfaz em sombra e reaparece no ponto do chão onde está mirando
    /// (curta distância, com um pequeno tempo de carga). Todos veem a marca no destino.
    /// </summary>
    internal sealed class BlinkAbility : Ability
    {
        private const byte EvWindup = 1, EvTeleport = 2, EvCancel = 3;

        private float _cooldownUntil;
        private float _windup = -1f;
        private Vector3 _dest;
        private ShadowMark _mark;
        private float _visual;
        private string _msg;
        private float _msgUntil;

        public BlinkAbility(PlayerAgent agent) : base(agent) { }

        public override void Dispose()
        {
            ClearMark();
            Agent.SetLoop(null, 0f);
        }

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (_windup >= 0f || !CanAct()) return;
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f) { Fail($"Recarregando ({cd:0}s)"); return; }

            Vector3 dest = AimPoint(ModConfig.BlinkRange.Value);
            Vector3 flat = dest - P.transform.position;
            flat.y = 0f;
            if (flat.magnitude < 1.5f) { Fail("Mire mais longe"); return; }
            if (flat.magnitude > ModConfig.BlinkRange.Value + 0.5f) { Fail("Longe demais"); return; }

            _dest = dest;
            _windup = 0f;
            StartVisual(P.transform.position, dest);
            Send(EvWindup, new AbilityPayload { A = P.transform.position, B = dest });
        }

        public override void OwnerTick(float dt)
        {
            if (_windup < 0f) return;
            if (!CanAct())
            {
                _windup = -1f;
                ClearMark();
                Send(EvCancel);
                return;
            }
            _windup += dt;
            if (_windup < Mathf.Max(0.05f, ModConfig.BlinkWindup.Value)) return;

            _windup = -1f;
            Vector3 from = P.transform.position;
            Arrive(from, _dest);
            Send(EvTeleport, new AbilityPayload { A = from, B = _dest });
            _cooldownUntil = Time.time + ModConfig.BlinkCooldown.Value;
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
            Color c = Color.Lerp(ModConfig.Blink, Color.white, 0.3f);
            if (_windup >= 0f)
            {
                float k = _windup / Mathf.Max(0.05f, ModConfig.BlinkWindup.Value);
                hud.Vignette(new Color(0.05f, 0f, 0.1f), 0.5f + 0.45f * k);
                hud.Status("Sumindo nas sombras…", c);
                return;
            }
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                hud.Status($"Sombras recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.BlinkCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else hud.Status($"[X] reaparecer onde você mira (até {ModConfig.BlinkRange.Value:0} m)", c);
        }

        // ---------------------------------------------------------------- rede / todos

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf) return;
            switch (type)
            {
                case EvWindup: StartVisual(p.A, p.B); break;
                case EvCancel: ClearMark(); break;
                case EvTeleport: Arrive(p.A, p.B); break;
            }
        }

        private void StartVisual(Vector3 from, Vector3 dest)
        {
            ClearMark();
            _mark = ShadowMark.Spawn(dest, ModConfig.Blink);
            Agent.PlaySfx(Sounds.ShadowWindup);
            _visual = 0.001f;
        }

        private void Arrive(Vector3 from, Vector3 to)
        {
            BurstFx.Spawn(from + Vector3.up * 0.2f, ModConfig.Blink, 0.9f, 0.5f);
            BurstFx.Spawn(to + Vector3.up * 0.2f, ModConfig.Blink, 1.1f, 0.6f);
            Sounds.PlayAt(to, Sounds.ShadowArrive);
            try { P.TeleportPlayer(to, false, 0f, false, true); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Falha no teleporte: " + e.Message); }
            ClearMark();
        }

        private void ClearMark()
        {
            if (_mark != null) Object.Destroy(_mark.gameObject);
            _mark = null;
            _visual = 0f;
        }

        public override void LateTick(float dt)
        {
            if (_mark == null) return;
            _visual = Mathf.Min(1f, _visual + dt / Mathf.Max(0.05f, ModConfig.BlinkWindup.Value));
            // Mão esquerda sobe, palma pra frente, "puxando" as sombras.
            Agent.PoseArm(false,
                new Vector3(-0.3f, 0.1f, 0.6f), new Vector3(0.1f, 0.8f, 0.6f),
                new Vector3(-0.3f, -0.5f, 0.8f), new Vector3(0.15f, 0.45f, 1f), Mathf.SmoothStep(0f, 1f, _visual), 0.9f);
        }
    }

    /// <summary>Marca de sombra no destino: círculo escuro com fios de sombra girando pra dentro.</summary>
    internal sealed class ShadowMark : MonoBehaviour
    {
        private Material _mat, _dark;
        private Transform _ring;
        private LineRenderer[] _wisps;
        private float _t;

        public static ShadowMark Spawn(Vector3 pos, Color c)
        {
            var go = new GameObject("NantaoShadowMark");
            go.transform.position = pos + Vector3.up * 0.04f;
            var m = go.AddComponent<ShadowMark>();
            m.Build(c);
            return m;
        }

        private void Build(Color c)
        {
            _mat = Mats.Glow(c, 2f);
            _dark = Mats.Glow(new Color(0.08f, 0.02f, 0.15f), 1f);
            _ring = FxUtil.Ring(transform, "Ring", _mat, 0.7f, 0.03f).transform;
            FxUtil.Ring(transform, "Core", _dark, 0.35f, 0.5f, 24);
            _wisps = new LineRenderer[10];
            for (int i = 0; i < _wisps.Length; i++)
            {
                _wisps[i] = FxUtil.Line(transform, "Wisp", i % 2 == 0 ? _mat : _dark, 8, false);
                _wisps[i].widthCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);
                _wisps[i].widthMultiplier = 0.05f;
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            _ring.localRotation = Quaternion.Euler(0f, -_t * 120f, 0f);
            for (int w = 0; w < _wisps.Length; w++)
            {
                float a0 = w / (float)_wisps.Length * Mathf.PI * 2f - _t * 2.5f;
                for (int i = 0; i < 8; i++)
                {
                    float k = i / 7f;
                    float r = Mathf.Lerp(1.1f, 0.1f, k);
                    float a = a0 + k * 2.4f;
                    float y = Mathf.Sin(k * Mathf.PI) * (0.6f + 0.3f * Mathf.Sin(_t * 3f + w));
                    _wisps[w].SetPosition(i, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_dark != null) Destroy(_dark);
        }
    }
}
