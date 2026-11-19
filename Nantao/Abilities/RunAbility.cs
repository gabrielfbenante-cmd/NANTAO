using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Faísca: corrida elétrica. X liga/desliga. Braços balançam com mãos retas e raios,
    /// velocidade extra enquanto houver energia.
    /// </summary>
    internal sealed class RunAbility : Ability
    {
        private const byte EvState = 1;

        private bool _active;
        private float _lastNetHeard;
        private float _energy = 1f;
        private float _rechargeTimer;
        private float _resendTimer;
        private float _weight;
        private float _phase;
        private float _sparkTimer;
        private readonly HandsFx _fx;

        public RunAbility(PlayerAgent agent) : base(agent)
        {
            _fx = new HandsFx(agent, () => ModConfig.Primary, () => ModConfig.Secondary,
                () => ModConfig.BoltsPerHand.Value, ModConfig.HandTrails.Value);
        }

        public override void Dispose()
        {
            _fx.Dispose();
            Agent.SetLoop(null, 0f);
        }

        private void SetActive(bool on, bool fromLocal)
        {
            if (_active == on) return;
            _active = on;
            Agent.PlaySfx(on ? Sounds.RunOn : Sounds.RunOff);
            if (on && P != null)
            {
                // Descarga elétrica no chão ao ligar.
                Vector3 feet = P.transform.position;
                RingWave.Spawn(feet, ModConfig.Primary, .2f, 2.6f, .45f, .12f, 5f);
                LineParticles.Burst(feet + Vector3.up * .3f, ModConfig.Primary, 16, 3.5f, Vector3.up * 2f, 9.8f, .5f, .025f, .05f, .3f);
                LineParticles.Burst(feet + Vector3.up * .3f, ModConfig.Secondary, 6, 3f, Vector3.up * 1.5f, 9.8f, .45f, .02f, .05f, .3f);
            }
            if (fromLocal)
            {
                Send(EvState, new AbilityPayload { Flag = on });
                _resendTimer = 1f;
            }
        }

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (fromSelf || type != EvState) return;
            _lastNetHeard = Time.time;
            SetActive(p.Flag, false);
        }

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (P.isPlayerDead || P.inSpecialInteractAnimation || P.isClimbingLadder) return;
            if (_active) SetActive(false, true);
            else if (_energy >= ModConfig.MinEnergyToActivate.Value) SetActive(true, true);
            else Agent.PlaySfx(Sounds.Fail, 0.5f);
        }

        public override void OwnerTick(float dt)
        {
            if (_active && (P.isPlayerDead || !P.isPlayerControlled)) SetActive(false, true);

            if (_active)
            {
                _energy -= dt / Mathf.Max(0.1f, ModConfig.EnergySeconds.Value);
                _rechargeTimer = ModConfig.RechargeDelay.Value;
                if (_energy <= 0f) { _energy = 0f; SetActive(false, true); }
                _resendTimer -= dt;
                if (_active && _resendTimer <= 0f)
                {
                    Send(EvState, new AbilityPayload { Flag = true });
                    _resendTimer = 1f;
                }
            }
            else if (_rechargeTimer > 0f) _rechargeTimer -= dt;
            else _energy = Mathf.Min(1f, _energy + dt / Mathf.Max(0.1f, ModConfig.RechargeSeconds.Value));

            if (!_active || P.inSpecialInteractAnimation || P.isClimbingLadder || P.inTerminalMenu) return;

            Vector2 mv = P.moveInputVector;
            if (mv.sqrMagnitude < 0.01f) return;

            var cc = P.thisController;
            if (cc != null && cc.enabled)
            {
                Vector3 dir = P.transform.right * mv.x + P.transform.forward * mv.y;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                float f = cc.isGrounded ? 1f : ModConfig.AirSpeedFactor.Value;
                if (P.isMovementHindered > 0) f *= 0.3f;
                if (P.isCrouching) f *= 0.6f;
                cc.Move(dir * ModConfig.ExtraSpeed.Value * f * dt);
            }
            if (ModConfig.ForceLegSprintAnimation.Value) Agent.SetAnimBool("Sprinting", true);
        }

        public override float FovOffset => ModConfig.FovBoost.Value * _weight * Mathf.Lerp(0.4f, 1f, Agent.MoveFactor);

        public override void HudTick(AgentHud hud)
        {
            float move = Agent.MoveFactor;
            if (_weight > 0.001f)
            {
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 15f, 3f);
                hud.Vignette(ModConfig.Primary, 0.55f * _weight * flicker);
                hud.Streaks(Color.Lerp(ModConfig.Primary, Color.white, 0.5f), 0.35f * _weight * move);
            }
            Color barColor = _active ? ModConfig.Primary
                : _energy < ModConfig.MinEnergyToActivate.Value ? new Color(0.8f, 0.25f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            hud.Bar(_energy, barColor);
            hud.Status(_active ? "[X] desligar corrida" : "[X] correr", Color.white);
        }

        // ---------------------------------------------------------------- todos

        public override void LateTick(float dt)
        {
            if (!IsLocal && _active && Time.time - _lastNetHeard > 3.5f) SetActive(false, false);

            bool showPose = _active && !P.isPlayerDead && !P.isClimbingLadder && !P.inSpecialInteractAnimation;
            _weight = Mathf.MoveTowards(_weight, showPose ? 1f : 0f, dt / Mathf.Max(0.01f, ModConfig.BlendTime.Value));

            float move = Agent.MoveFactor;
            float freq = Mathf.Max(0f, ModConfig.SwingFrequency.Value) * Mathf.Lerp(0.35f, 1f, move);
            _phase = Mathf.Repeat(_phase + dt * freq * Mathf.PI * 2f, Mathf.PI * 2f);

            if (_weight > 0.001f) ApplyPose(move);

            // Faíscas saltando do chão a cada passo correndo.
            if (_weight > .5f && move > .35f)
            {
                _sparkTimer -= dt;
                if (_sparkTimer <= 0f)
                {
                    _sparkTimer = .2f + UnityEngine.Random.value * .08f;
                    Color c = UnityEngine.Random.value < .7f ? ModConfig.Primary : ModConfig.Secondary;
                    Vector3 back = -P.transform.forward * 1.5f;
                    LineParticles.Burst(P.transform.position + Vector3.up * .05f, c, 5, 2f, Vector3.up * 1.4f + back, 12f, .35f, .016f, .045f, .2f);
                }
            }

            float intensity = _weight * Mathf.Lerp(0.6f, 1f, move);
            _fx.Tick(intensity, intensity, dt);
            Agent.SetLoop(Sounds.RunHum, _weight * Mathf.Lerp(0.45f, 1f, move), Mathf.Lerp(0.9f, 1.15f, move));
        }

        private void ApplyPose(float move)
        {
            float w = _weight * Mathf.Clamp01(ModConfig.PoseStrength.Value);
            float a = ModConfig.SwingAmount.Value * move;
            float s = Mathf.Sin(_phase);
            float fingers = Mathf.Clamp01(ModConfig.FingerStraighten.Value);
            float wRight = w * (P.isHoldingObject ? 0.25f : 1f);
            float wLeft = w * (P.twoHanded ? 0.25f : 1f);

            PoseArm(false, -s * a, wLeft, fingers);
            PoseArm(true, s * a, wRight, fingers);
        }

        /// <param name="p">balanço: + = braço indo pra frente.</param>
        private void PoseArm(bool right, float p, float w, float fingers)
        {
            float side = right ? 1f : -1f;
            // 3ª pessoa (corpo)
            var bu = new Vector3(0.12f * side, -1f, 0.75f * p);
            var bl = new Vector3(-0.12f * side, 0.3f + 0.55f * p, 0.9f);
            // 1ª pessoa (câmera)
            float fwd = ModConfig.FirstPersonArmsForward.Value;
            float lift = ModConfig.FirstPersonArmsLift.Value;
            float spread = ModConfig.FirstPersonArmsSpread.Value;
            var fu = new Vector3(spread * side, -0.9f, fwd + 0.55f * p);
            var fl = new Vector3(-0.6f * spread * side, lift + 0.55f * p, 1f);
            Agent.PoseArm(right, bu, bl, fu, fl, w, fingers);
        }
    }
}
