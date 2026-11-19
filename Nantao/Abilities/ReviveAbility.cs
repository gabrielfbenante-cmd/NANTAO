using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Aurora: segure X perto do corpo de um amigo morto pra reviver.
    /// Limite de revives por rodada (recarrega com a nave em órbita).
    /// </summary>
    internal sealed class ReviveAbility : Ability
    {
        internal const byte EvRevive = 1, EvChannel = 2;

        private int _revivesLeft;
        private bool _lastOrbit;
        private PlayerControllerB _target;
        private float _channel = -1f;      // segundos canalizando (dono)
        private Vector3 _channelStart;
        private bool _channelVisual;        // todos os PCs
        private Vector3 _channelBodyPos;
        private float _channelWeight;
        private string _msg;
        private float _msgUntil;
        private readonly HandsFx _fx;
        private ReviveGlow _glow;

        public ReviveAbility(PlayerAgent agent) : base(agent)
        {
            _revivesLeft = Mathf.Max(0, ModConfig.RevivesPerRound.Value);
            _lastOrbit = StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase;
            _fx = new HandsFx(agent, () => ModConfig.Revive, () => Color.white, () => 3, false, 0.25f);
        }

        public override void Dispose()
        {
            _fx.Dispose();
            SetGlow(false, default);
            Agent.SetLoop(null, 0f);
        }

        // ---------------------------------------------------------------- dono

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled) return;
            if (_revivesLeft <= 0) { Fail("Sem revives nesta rodada (recarrega em órbita)"); return; }
            _target = FindBody(out Vector3 bodyPos);
            if (_target == null) { Fail("Nenhum corpo de amigo por perto"); return; }

            _channel = 0f;
            _channelStart = P.transform.position;
            StartChannelVisual(bodyPos);
            Send(EvChannel, new AbilityPayload { Flag = true, A = bodyPos });
        }

        public override void OnRelease(float held)
        {
            if (_channel >= 0f) CancelChannel("Cancelado");
        }

        public override void OwnerTick(float dt)
        {
            bool orbit = StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase;
            if (orbit && !_lastOrbit) _revivesLeft = Mathf.Max(0, ModConfig.RevivesPerRound.Value);
            _lastOrbit = orbit;

            if (_channel < 0f) return;
            if (P.isPlayerDead || P.inAnimationWithEnemy != null) { CancelChannel(null); return; }
            if (Vector3.Distance(P.transform.position, _channelStart) > 1.2f) { CancelChannel("Fique parado pra reviver"); return; }
            if (!BodyStillValid(_target, out Vector3 bodyPos)) { CancelChannel("O corpo não está mais aqui"); return; }
            _channelBodyPos = bodyPos;

            _channel += dt;
            if (_channel < Mathf.Max(0.2f, ModConfig.ReviveTime.Value)) return;

            // Pede pro host reviver (ele confere e manda pra todos, inclusive pra mim).
            _channel = -1f;
            _revivesLeft--;
            Send(EvRevive, new AbilityPayload { I = (int)_target.playerClientId, A = bodyPos, B = new Vector3(Flags(P), 0f, 0f) });
            StopChannelVisual();
            Send(EvChannel, new AbilityPayload { Flag = false });
        }

        private void CancelChannel(string why)
        {
            _channel = -1f;
            StopChannelVisual();
            Send(EvChannel, new AbilityPayload { Flag = false });
            if (why != null) Fail(why);
        }

        private PlayerControllerB FindBody(out Vector3 pos)
        {
            pos = default;
            PlayerControllerB best = null;
            float bestD = ModConfig.ReviveRange.Value;
            var sor = StartOfRound.Instance;
            if (sor == null) return null;
            foreach (var other in sor.allPlayerScripts)
            {
                if (other == null || other == P) continue;
                if (!BodyStillValid(other, out Vector3 bp)) continue;
                float d = Vector3.Distance(P.transform.position, bp);
                if (d <= bestD) { bestD = d; best = other; pos = bp; }
            }
            return best;
        }

        internal static bool BodyStillValid(PlayerControllerB p, out Vector3 pos)
        {
            pos = default;
            if (p == null || !p.isPlayerDead || p.deadBody == null || p.deadBody.deactivated) return false;
            var grab = p.deadBody.grabBodyObject;
            if (grab != null && grab.isHeld) return false; // alguém está carregando
            pos = BodyPosition(p.deadBody);
            return true;
        }

        private static Vector3 BodyPosition(DeadBodyInfo body)
        {
            if (body.bodyParts != null && body.bodyParts.Length > 0 && body.bodyParts[0] != null)
                return body.bodyParts[0].transform.position;
            return body.transform.position;
        }

        private void Fail(string msg)
        {
            Agent.PlaySfx(Sounds.Fail, 0.6f);
            _msg = msg;
            _msgUntil = Time.time + 1.8f;
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = ModConfig.Revive;
            if (_channel >= 0f)
            {
                float k = _channel / Mathf.Max(0.2f, ModConfig.ReviveTime.Value);
                hud.Vignette(c, 0.35f + 0.25f * k);
                hud.Bar(k, c);
                hud.Status($"Revivendo {_target?.playerUsername}…", c);
                return;
            }
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }

            if (_revivesLeft <= 0) { hud.Status("Revive usado (recarrega em órbita)", new Color(0.7f, 0.7f, 0.7f)); return; }
            var near = FindBody(out _);
            hud.Status(near != null ? $"[segure X] reviver {near.playerUsername}" : $"Revives: {_revivesLeft} · chegue perto de um corpo",
                near != null ? c : new Color(0.75f, 0.9f, 0.85f));
        }

        // ---------------------------------------------------------------- rede

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (type == EvChannel && !fromSelf)
            {
                if (p.Flag) StartChannelVisual(p.A);
                else StopChannelVisual();
            }
            else if (type == EvRevive)
            {
                // Vem do host pra todos (inclusive eu): agora sim revive.
                var sor = StartOfRound.Instance;
                if (sor == null || p.I < 0 || p.I >= sor.allPlayerScripts.Length) return;
                ReviveLogic.Revive(sor.allPlayerScripts[p.I], p.A, (int)p.B.x);
            }
        }

        /// <summary>Host confere antes de repassar o pedido de reviver.</summary>
        internal static bool HostAllows(AbilityPayload p)
        {
            var sor = StartOfRound.Instance;
            if (sor == null || p.I < 0 || p.I >= sor.allPlayerScripts.Length) return false;
            return BodyStillValid(sor.allPlayerScripts[p.I], out _);
        }

        // ---------------------------------------------------------------- todos

        private void StartChannelVisual(Vector3 bodyPos)
        {
            _channelVisual = true;
            _channelBodyPos = bodyPos;
            SetGlow(true, bodyPos);
        }

        private void StopChannelVisual()
        {
            _channelVisual = false;
            SetGlow(false, default);
        }

        private void SetGlow(bool on, Vector3 pos)
        {
            if (_glow != null) UnityEngine.Object.Destroy(_glow.gameObject);
            _glow = on ? ReviveGlow.Spawn(pos, ModConfig.Revive) : null;
        }

        public override void LateTick(float dt)
        {
            if (P.isPlayerDead && _channelVisual) StopChannelVisual();
            _channelWeight = Mathf.MoveTowards(_channelWeight, _channelVisual ? 1f : 0f, dt / 0.25f);

            if (_channelWeight > 0.001f)
            {
                // Duas mãos estendidas pra baixo, na direção do corpo.
                var bu = new Vector3(0.2f, -0.6f, 0.8f);
                var bl = new Vector3(-0.1f, -0.5f, 1f);
                var fu = new Vector3(0.3f, -0.8f, 0.5f);
                var fl = new Vector3(-0.2f, -0.35f, 1f);
                Agent.PoseArm(true, bu, bl, fu, fl, _channelWeight, 0.9f);
                Agent.PoseArm(false, PlayerAgent.Mirror(bu), PlayerAgent.Mirror(bl), PlayerAgent.Mirror(fu), PlayerAgent.Mirror(fl),
                    _channelWeight, 0.9f);
            }

            _fx.Tick(_channelWeight, _channelWeight, dt);
            Agent.SetLoop(Sounds.ReviveLoop, _channelWeight * 0.8f);
        }
    }

    /// <summary>Coluna de luz suave subindo do corpo enquanto alguém revive.</summary>
    internal sealed class ReviveGlow : MonoBehaviour
    {
        private Material _mat;
        private LineRenderer[] _strands;
        private float[] _angles;
        private Transform _ring;
        private UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _light;
        private float _t;

        public static ReviveGlow Spawn(Vector3 pos, Color c)
        {
            var go = new GameObject("NantaoReviveGlow");
            go.transform.position = pos;
            var g = go.AddComponent<ReviveGlow>();
            g.Build(c);
            return g;
        }

        private void Build(Color c)
        {
            _mat = Mats.Glow(c, 2.5f);
            _ring = FxUtil.Ring(transform, "Ring", _mat, 0.8f, 0.025f).transform;
            _strands = new LineRenderer[8];
            _angles = new float[8];
            for (int i = 0; i < _strands.Length; i++)
            {
                _strands[i] = FxUtil.Line(transform, "Strand", _mat, 10, false);
                _strands[i].widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                _angles[i] = i / (float)_strands.Length * Mathf.PI * 2f;
            }
            _light = FxUtil.PointLight(transform, c, 5f);
            if (_light != null) _light.transform.localPosition = Vector3.up * 0.6f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            _ring.localRotation = Quaternion.Euler(0f, _t * 30f, 0f);
            for (int s = 0; s < _strands.Length; s++)
            {
                float a = _angles[s] + _t * 1.2f;
                for (int i = 0; i < 10; i++)
                {
                    float k = i / 9f;
                    float r = 0.8f * (1f - k * 0.7f);
                    float aa = a + k * 2.2f;
                    float y = k * (1.2f + 0.3f * Mathf.Sin(_t * 3f + s));
                    _strands[s].SetPosition(i, new Vector3(Mathf.Cos(aa) * r, y, Mathf.Sin(aa) * r));
                }
                _strands[s].widthMultiplier = 0.03f;
            }
            if (_light != null) _light.SetIntensity(180f + 60f * Mathf.Sin(_t * 5f));
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }

    /// <summary>
    /// Desfaz a morte de um jogador no meio da rodada. Roda em TODOS os computadores.
    /// Baseado no que o próprio jogo faz ao reviver todo mundo no fim da rodada, mas pra um jogador só.
    /// Cada passo é protegido: se uma parte falhar (ex: atualização do jogo), o resto continua.
    /// </summary>
    internal static class ReviveLogic
    {
        private static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Reviver/{what}: {e.Message}"); }
        }

        public static void Revive(PlayerControllerB t, Vector3 pos, int flags)
        {
            if (t == null || !t.isPlayerDead) return;
            var sor = StartOfRound.Instance;
            int i = (int)t.playerClientId;
            bool owner = t.IsOwner;

            Try("sangue", () => t.ResetPlayerBloodObjects(true));
            Try("estado", () =>
            {
                t.isClimbingLadder = false;
                t.clampLooking = false;
                t.inVehicleAnimation = false;
                t.disableMoveInput = false;
                t.disableLookInput = false;
                t.disableInteract = false;
                t.health = 100;
                t.hasBeenCriticallyInjured = false;
                t.criticallyInjured = false;
                t.bleedingHeavily = false;
                t.activatingItem = false;
                t.twoHanded = false;
                t.inShockingMinigame = false;
                t.inSpecialInteractAnimation = false;
                t.freeRotationInInteractAnimation = false;
                t.disableSyncInAnimation = false;
                t.inAnimationWithEnemy = null;
                t.holdingWalkieTalkie = false;
                t.speakingToWalkieTalkie = false;
                t.isSinking = false;
                t.isUnderwater = false;
                t.sinkingValue = 0f;
                t.externalForceAutoFade = Vector3.zero;
                t.voiceMuffledByEnemy = false;
                t.parentedToElevatorLastFrame = false;
                t.overrideGameOverSpectatePivot = null;
                t.setPositionOfDeadPlayer = false;
            });
            Try("rotação", t.ResetZAndXRotation);
            Try("controle", () => t.thisController.enabled = true);

            t.isPlayerDead = false;
            t.isPlayerControlled = true;
            Try("flags", () =>
            {
                t.isInsideFactory = (flags & 1) != 0;
                t.isInElevator = (flags & 2) != 0;
                t.isInHangarShipRoom = (flags & 4) != 0;
            });

            Try("extrapolação", () => sor.SetPlayerObjectExtrapolate(false));
            Try("teleporte", () => t.TeleportPlayer(pos + Vector3.up * 0.2f, false, 0f, false, true));
            Try("modelo", () => t.DisablePlayerModel(sor.allPlayerObjects[i], true, true));
            Try("renderers", () => PlayerAgent.For(t)?.RestoreRenderers());
            Try("lanterna", () => t.helmetLight.enabled = false);
            Try("agachar", () => t.Crouch(false));
            Try("mancar", () => t.playerBodyAnimator.SetBool("Limp", false));
            Try("audio", () => t.statusEffectAudio.Stop());
            Try("jetpack", t.DisableJetpackControlsLocally);
            Try("radar", () => t.mapRadarDotAnimator.SetBool("dead", false));

            if (owner)
            {
                Try("hud gás", () => HUDManager.Instance.gasHelmetAnimator.SetBool("gasEmitting", false));
                Try("espectador", () =>
                {
                    t.hasBegunSpectating = false;
                    t.spectatedPlayerScript = null;
                    t.hinderedMultiplier = 1f;
                    t.isMovementHindered = 0;
                    t.sourcesCausingSinking = 0;
                });
                Try("ui espectador", HUDManager.Instance.RemoveSpectateUI);
                Try("tela game over", () => HUDManager.Instance.gameOverAnimator.SetTrigger("revive"));
                Try("filtro audio", () => HUDManager.Instance.audioListenerLowPass.enabled = false);
                Try("câmera espectador", () => sor.SetSpectateCameraToGameOverMode(false, t));
                Try("câmera", () => sor.SwitchCamera(t.gameplayCamera));
                Try("vida", () => HUDManager.Instance.UpdateHealthUI(100, false));
                Try("hud", () => HUDManager.Instance.HideHUD(false));
                Try("braços", () => { if (t.thisPlayerModelArms != null) t.thisPlayerModelArms.enabled = true; });
            }

            Try("voz", () =>
            {
                SoundManager.Instance.playerVoicePitchTargets[i] = 1f;
                SoundManager.Instance.SetPlayerPitch(1f, i);
            });

            // Corpo: o host remove o objeto de rede (carregável); todos apagam o ragdoll local.
            Try("corpo", () =>
            {
                var body = t.deadBody;
                if (body == null) return;
                var grab = body.grabBodyObject;
                if (grab != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    && grab.NetworkObject != null && grab.NetworkObject.IsSpawned)
                    grab.NetworkObject.Despawn(true);
                UnityEngine.Object.Destroy(body.gameObject);
                t.deadBody = null;
            });

            Try("contagem", () =>
            {
                sor.livingPlayers = Mathf.Min(sor.livingPlayers + 1, sor.allPlayerScripts.Length);
                sor.allPlayersDead = false;
            });
            Try("efeitos de voz", sor.UpdatePlayerVoiceEffects);

            BurstFx.Spawn(pos, ModConfig.Revive, 1.3f, 0.8f);
            Sounds.PlayAt(pos, Sounds.ReviveDone);
            Plugin.Log.LogInfo($"{t.playerUsername} foi revivido.");
        }
    }
}
