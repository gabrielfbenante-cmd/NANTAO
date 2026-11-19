using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Âncora: X coloca a âncora no chão. X de novo (toque) teleporta de volta pra ela.
    /// Segurar X com a âncora no chão recolhe sem teleportar.
    /// </summary>
    internal sealed class AnchorAbility : Ability
    {
        private const byte EvPlace = 1, EvRemove = 2, EvTeleport = 3, EvWindup = 4;

        private bool _placed;
        private Vector3 _anchorPos;
        private int _anchorFlags;
        private bool _placedInOrbit;
        private AnchorMarker _marker;

        private float _cooldownUntil;
        private float _windup = -1f;        // >=0 carregando teleporte
        private bool _recalledThisPress;
        private bool _pressStartedWithAnchor;
        private float _gesture;              // tempo restante do gesto de colocar
        private float _windupVisual;         // 0..1 (todos os PCs)
        private string _msg;
        private float _msgUntil;
        private float _arriveFlash;

        public AnchorAbility(PlayerAgent agent) : base(agent) { }

        public override void Dispose()
        {
            RemoveAnchorLocal();
            Agent.SetLoop(null, 0f);
        }

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            _recalledThisPress = false;
            _pressStartedWithAnchor = _placed;
            if (_placed || _windup >= 0f) return; // decide no soltar/segurar
            if (!CanAct()) return;

            float cd = _cooldownUntil - Time.time;
            if (cd > 0f) { Fail($"Recarregando ({cd:0}s)"); return; }

            Vector3 pos = FindGround();
            int flags = Flags(P);
            PlaceLocal(pos, flags);
            Send(EvPlace, new AbilityPayload { A = pos, I = flags });
        }

        public override void OnHold(float held)
        {
            if (!_pressStartedWithAnchor || !_placed || _recalledThisPress || _windup >= 0f) return;
            if (held < ModConfig.AnchorRecallHold.Value) return;
            _recalledThisPress = true;
            RemoveAnchorLocal();
            Send(EvRemove);
            Agent.PlaySfx(Sounds.RunOff, 0.6f);
            Message("Âncora recolhida");
        }

        public override void OnRelease(float held)
        {
            if (!_pressStartedWithAnchor || _recalledThisPress || !_placed || _windup >= 0f) return;
            if (!CanAct()) return;
            if (!InRange(out string why)) { Fail(why); return; }

            _windup = 0f;
            Agent.PlaySfx(Sounds.TpWindup);
            Send(EvWindup, new AbilityPayload { A = P.transform.position });
        }

        public override void OwnerTick(float dt)
        {
            if (_windup < 0f) return;
            if (!CanAct() || !_placed)
            {
                _windup = -1f;
                return;
            }
            _windup += dt;
            if (_windup < Mathf.Max(0f, ModConfig.AnchorWindup.Value)) return;

            _windup = -1f;
            if (!InRange(out string why)) { Fail(why); return; }

            Vector3 from = P.transform.position;
            Vector3 to = _anchorPos;
            int flags = _anchorFlags;
            DoTeleport(from, to, flags);
            Send(EvTeleport, new AbilityPayload { A = from, B = to, I = flags });
            _cooldownUntil = Time.time + ModConfig.AnchorCooldown.Value;
        }

        private bool CanAct() =>
            !P.isPlayerDead && P.isPlayerControlled && !P.inSpecialInteractAnimation && !P.isClimbingLadder
            && P.inAnimationWithEnemy == null;

        private bool InRange(out string why)
        {
            why = null;
            if ((Flags(P) & 1) != (_anchorFlags & 1))
            {
                why = (_anchorFlags & 1) != 0 ? "A âncora está dentro do prédio" : "A âncora está do lado de fora";
                return false;
            }
            float d = Vector3.Distance(P.transform.position, _anchorPos);
            if (d > ModConfig.AnchorRange.Value)
            {
                why = $"Fora do alcance ({d:0} m / {ModConfig.AnchorRange.Value:0} m)";
                return false;
            }
            return true;
        }

        private Vector3 FindGround()
        {
            Vector3 origin = P.transform.position + P.transform.forward * 0.9f + Vector3.up * 1.0f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 3.5f, LayerMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return P.transform.position;
        }

        private void Fail(string msg)
        {
            Agent.PlaySfx(Sounds.Fail, 0.6f);
            Message(msg);
        }

        private void Message(string msg)
        {
            _msg = msg;
            _msgUntil = Time.time + 1.8f;
        }

        public override void HudTick(AgentHud hud)
        {
            Color gold = ModConfig.Anchor;
            if (_windupVisual > 0f) hud.Vignette(gold, 0.6f * _windupVisual);
            if (_arriveFlash > 0f)
            {
                hud.Vignette(gold, .85f * _arriveFlash);
                hud.Streaks(Color.Lerp(gold, Color.white, .5f), .6f * _arriveFlash);
            }

            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }

            float cd = _cooldownUntil - Time.time;
            if (_placed)
            {
                float d = Vector3.Distance(P.transform.position, _anchorPos);
                bool ok = InRange(out _);
                hud.Status(ok ? $"[X] voltar pra âncora · {d:0} m   (segure X: recolher)"
                              : $"Âncora longe demais ({d:0} m)   (segure X: recolher)",
                           ok ? gold : new Color(1f, 0.45f, 0.35f));
                hud.Bar(1f - Mathf.Clamp01(d / Mathf.Max(1f, ModConfig.AnchorRange.Value)), ok ? gold : new Color(0.8f, 0.3f, 0.25f));
            }
            else if (cd > 0f)
            {
                hud.Status($"Âncora recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.AnchorCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else
            {
                hud.Status("[X] colocar âncora", gold);
            }
        }

        // ---------------------------------------------------------------- rede

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf) return;
            switch (type)
            {
                case EvPlace: PlaceLocal(p.A, p.I); break;
                case EvRemove: RemoveAnchorLocal(); break;
                case EvWindup:
                    Agent.PlaySfx(Sounds.TpWindup);
                    _windupVisual = 1f;
                    break;
                case EvTeleport: DoTeleport(p.A, p.B, p.I); break;
            }
        }

        // ---------------------------------------------------------------- todos

        private void PlaceLocal(Vector3 pos, int flags)
        {
            RemoveAnchorLocal();
            _placed = true;
            _anchorPos = pos;
            _anchorFlags = flags;
            _placedInOrbit = StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase;
            _marker = AnchorMarker.Spawn(pos, ModConfig.Anchor);
            _gesture = 0.4f;
            RingWave.Spawn(pos, ModConfig.Anchor, .1f, 1.3f, .35f, .07f, 5f);
            LineParticles.Burst(pos + Vector3.up * .1f, ModConfig.Anchor, 10, 1.5f, Vector3.up * 1.5f, 6f, .45f, .018f, .05f);
            Sounds.PlayAt(pos, Sounds.AnchorPlace);
        }

        private void RemoveAnchorLocal()
        {
            _placed = false;
            if (_marker != null) Object.Destroy(_marker.gameObject);
            _marker = null;
        }

        private void DoTeleport(Vector3 from, Vector3 to, int flags)
        {
            BurstFx.Spawn(from + Vector3.up * 0.1f, ModConfig.Anchor, 1f);
            BurstFx.Spawn(to + Vector3.up * 0.1f, ModConfig.Anchor, 1.2f, 0.6f);
            // Saída: anel se fechando. Chegada: anel abrindo + partículas subindo.
            RingWave.Spawn(from, ModConfig.Anchor, 2.2f, .1f, .35f, .1f, 5f);
            RingWave.Spawn(to, ModConfig.Anchor, .2f, 3f, .55f, .12f, 5f);
            LineParticles.Burst(to + Vector3.up * .2f, ModConfig.Anchor, 20, 1.2f, Vector3.up * 3f, 1f, .9f, .02f, .08f, .5f);
            if (IsLocal) _arriveFlash = 1f;
            Sounds.PlayAt(from, Sounds.TpArrive, 0.7f);
            Sounds.PlayAt(to, Sounds.TpArrive);
            try
            {
                P.TeleportPlayer(to, false, 0f, false, true);
                ApplyFlags(P, flags);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Falha no teleporte: " + e.Message);
            }
            _windupVisual = 0f;
            RemoveAnchorLocal();
        }

        public override void LateTick(float dt)
        {
            // Âncora some se o dono morrer ou a fase da partida mudar (pousou / voltou pra órbita).
            if (_placed)
            {
                bool orbit = StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase;
                if (P.isPlayerDead || orbit != _placedInOrbit) RemoveAnchorLocal();
            }

            _arriveFlash = Mathf.MoveTowards(_arriveFlash, 0f, dt / .6f);
            if (IsLocal) _windupVisual = _windup >= 0f ? Mathf.Clamp01(_windup / Mathf.Max(0.05f, ModConfig.AnchorWindup.Value)) : 0f;
            else _windupVisual = Mathf.MoveTowards(_windupVisual, 0f, dt / Mathf.Max(0.2f, ModConfig.AnchorWindup.Value + 0.3f));

            // Gesto: abaixar e "cravar" a âncora.
            if (_gesture > 0f)
            {
                _gesture -= dt;
                float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.4f) * Mathf.PI);
                Agent.PoseArm(true,
                    new Vector3(0.15f, -0.7f, 0.7f), new Vector3(0.05f, -0.8f, 0.6f),
                    new Vector3(0.3f, -0.8f, 0.6f), new Vector3(0f, -0.55f, 1f), w);
            }

            // Carregando o teleporte: mão sobe na frente do rosto.
            if (_windupVisual > 0f)
            {
                float w = Mathf.SmoothStep(0f, 1f, _windupVisual);
                Agent.PoseArm(true,
                    new Vector3(0.3f, 0.2f, 0.3f), new Vector3(-0.3f, 1f, 0.2f),
                    new Vector3(0.3f, -0.4f, 0.7f), new Vector3(-0.25f, 0.6f, 1f), w);
            }
        }
    }

    /// <summary>Marcador da âncora no chão: dois anéis girando, feixe de luz e um diamante flutuando.</summary>
    internal sealed class AnchorMarker : MonoBehaviour
    {
        private Transform _ringA, _ringB, _diamond;
        private LineRenderer _beam;
        private Material _mat;
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;
        private float _t, _moteTimer;
        private Color _color;

        public static AnchorMarker Spawn(Vector3 pos, Color color)
        {
            var go = new GameObject("NantaoAnchor");
            go.transform.position = pos + Vector3.up * 0.03f;
            var m = go.AddComponent<AnchorMarker>();
            m.Build(color);
            return m;
        }

        private void Build(Color color)
        {
            _color = color;
            _mat = Mats.Glow(color);
            _ringA = FxUtil.Ring(transform, "RingA", _mat, 0.45f, 0.03f).transform;
            _ringB = FxUtil.Ring(transform, "RingB", _mat, 0.28f, 0.02f, 6).transform; // hexágono

            _beam = FxUtil.Line(transform, "Beam", _mat, 2, false);
            _beam.SetPosition(0, Vector3.zero);
            _beam.SetPosition(1, Vector3.up * 1.6f);
            _beam.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

            // Diamante (octaedro em linhas)
            var d = FxUtil.Line(transform, "Diamond", _mat, 8, false);
            d.widthMultiplier = 0.015f;
            float r = 0.09f, h = 0.15f;
            d.SetPositions(new[]
            {
                new Vector3(0, h, 0), new Vector3(r, 0, 0), new Vector3(0, -h, 0), new Vector3(-r, 0, 0),
                new Vector3(0, h, 0), new Vector3(0, 0, r), new Vector3(0, -h, 0), new Vector3(0, 0, -r),
            });
            d.loop = true;
            _diamond = d.transform;

            _light = FxUtil.PointLight(transform, color, 3f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.5f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4f);
            _ringA.localRotation = Quaternion.Euler(0f, _t * 40f, 0f);
            _ringB.localRotation = Quaternion.Euler(0f, -_t * 70f, 0f);
            _diamond.localPosition = Vector3.up * (0.7f + 0.06f * Mathf.Sin(_t * 2.2f));
            _diamond.localRotation = Quaternion.Euler(0f, _t * 90f, 0f);
            _beam.widthMultiplier = 0.05f + 0.03f * pulse;
            if (_light != null) _light.SetIntensity(120f + 80f * pulse);
            // Partículas douradas subindo da âncora.
            _moteTimer -= Time.deltaTime;
            if (_moteTimer <= 0f)
            {
                _moteTimer = .16f;
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * .4f;
                LineParticles.Burst(p, _color, 1, .05f, Vector3.up * .8f, 0f, 1.3f, .012f, .12f);
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
