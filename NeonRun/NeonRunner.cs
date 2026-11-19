using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonRun
{
    /// <summary>
    /// Um por jogador (local e remotos). Cuida do estado ligado/desligado, energia, velocidade,
    /// pose dos braços, efeitos, som e (no jogador local) câmera e HUD.
    /// Roda depois dos scripts do jogo (ordem alta) pra pose ficar por cima da animação original.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    internal sealed class NeonRunner : MonoBehaviour
    {
        private static readonly Dictionary<PlayerControllerB, NeonRunner> All = new Dictionary<PlayerControllerB, NeonRunner>();

        public static NeonRunner For(PlayerControllerB p)
        {
            if (p == null) return null;
            return All.TryGetValue(p, out var r) ? r : null;
        }

        private PlayerControllerB _p;

        // Estado
        private bool _active;
        private float _lastNetHeard;
        private float _energy = 1f;
        private float _rechargeTimer;
        private float _netResendTimer;

        // Animação
        private float _weight;
        private float _phase;
        private float _moveFactor;
        private Vector3 _lastPos;
        private bool _hasLastPos;

        // Esqueletos
        private ArmRig _armsRig; // braços em 1ª pessoa (só jogador local)
        private ArmRig _bodyRig; // corpo inteiro (o que os outros veem)
        private float _nextRigTry;
        private int _rigFailures;

        // Efeitos
        private HandFx _fxLeft, _fxRight;
        private ArmRig _fxRig;
        private AudioSource _hum, _sfx;
        private NeonHud _hud;
        private float _fovApplied;

        // Input
        private InputAction _toggle, _reload;
        private string _toggleBinding, _reloadBinding;

        // Animator
        private Animator _paramsFor;
        private HashSet<string> _params;

        public void Init(PlayerControllerB p)
        {
            _p = p;
            All[p] = this;
            NeonConfig.Reloaded += OnConfigReloaded;
        }

        private void OnDestroy()
        {
            if (_p != null) All.Remove(_p);
            NeonConfig.Reloaded -= OnConfigReloaded;
            _toggle?.Dispose();
            _reload?.Dispose();
            DisposeFx();
            _hud?.Dispose();
            _hud = null;
            RemoveFov();
        }

        private bool IsLocal =>
            _p != null && GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController == _p;

        private int PlayerIndex => (int)_p.playerClientId;

        // ---------------------------------------------------------------- estado

        public void SetNetworkState(bool active)
        {
            if (IsLocal) return; // o meu estado quem manda sou eu
            _lastNetHeard = Time.time;
            SetActive(active, false);
        }

        private void SetActive(bool on, bool fromLocal)
        {
            if (_active == on) return;
            _active = on;
            PlaySfx(on ? NeonAudio.On : NeonAudio.Off);
            if (fromLocal)
            {
                NeonNetwork.SendState(PlayerIndex, on);
                _netResendTimer = 1f;
            }
        }

        // ---------------------------------------------------------------- Update do jogo

        /// <summary>Antes do Update do jogo: tira o FOV extra pra o jogo calcular o dele normalmente.</summary>
        public void BeforeGameUpdate() => RemoveFov();

        /// <summary>Depois do Update do jogo: input, energia, velocidade extra e pernas correndo.</summary>
        public void AfterGameUpdate()
        {
            if (_p == null) return;

            if (IsLocal)
            {
                LocalTick();
            }
            else if (_active && Time.time - _lastNetHeard > 3.5f)
            {
                // Parou de receber atualização (ex: o jogador saiu) -> desliga.
                SetActive(false, false);
            }
        }

        private void LocalTick()
        {
            EnsureInput();
            float dt = Time.deltaTime;

            if (_reload.WasPressedThisFrame()) Plugin.ReloadConfig();

            bool inMenu = _p.inTerminalMenu || _p.isTypingChat ||
                          (_p.quickMenuManager != null && _p.quickMenuManager.isMenuOpen);
            bool cantRun = _p.isPlayerDead || !_p.isPlayerControlled || _p.inSpecialInteractAnimation || _p.isClimbingLadder;

            if (!inMenu && !cantRun && _toggle.WasPressedThisFrame())
            {
                if (_active) SetActive(false, true);
                else if (_energy >= NeonConfig.MinEnergyToActivate.Value) SetActive(true, true);
                else PlaySfx(NeonAudio.Off, 0.4f);
            }

            if (_active && (_p.isPlayerDead || !_p.isPlayerControlled)) SetActive(false, true);

            // Energia
            if (_active)
            {
                _energy -= dt / Mathf.Max(0.1f, NeonConfig.EnergySeconds.Value);
                _rechargeTimer = NeonConfig.RechargeDelay.Value;
                if (_energy <= 0f)
                {
                    _energy = 0f;
                    SetActive(false, true);
                }
            }
            else if (_rechargeTimer > 0f)
            {
                _rechargeTimer -= dt;
            }
            else
            {
                _energy = Mathf.Min(1f, _energy + dt / Mathf.Max(0.1f, NeonConfig.RechargeSeconds.Value));
            }

            // Reenvia o estado de vez em quando (pra quem entrou depois na lobby).
            if (_active)
            {
                _netResendTimer -= dt;
                if (_netResendTimer <= 0f)
                {
                    NeonNetwork.SendState(PlayerIndex, true);
                    _netResendTimer = 1f;
                }
            }

            if (!_active || cantRun || inMenu) return;

            Vector2 mv = _p.moveInputVector;
            if (mv.sqrMagnitude < 0.01f) return;

            // Velocidade extra (o movimento do dono é sincronizado pelo próprio jogo).
            var cc = _p.thisController;
            if (cc != null && cc.enabled)
            {
                Vector3 dir = _p.transform.right * mv.x + _p.transform.forward * mv.y;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                float f = cc.isGrounded ? 1f : NeonConfig.AirSpeedFactor.Value;
                if (_p.isMovementHindered > 0) f *= 0.3f;
                if (_p.isCrouching) f *= 0.6f;
                cc.Move(dir * NeonConfig.ExtraSpeed.Value * f * dt);
            }

            // Pernas na animação de corrida do jogo (sincroniza sozinho com os outros).
            if (NeonConfig.ForceLegSprintAnimation.Value) SetAnimBool("Sprinting", true);
        }

        private void EnsureInput()
        {
            string tb = NeonConfig.KeyBinding.Value;
            if (_toggle == null || _toggleBinding != tb)
            {
                _toggle?.Dispose();
                _toggle = new InputAction("NeonRunToggle", InputActionType.Button, tb);
                _toggle.Enable();
                _toggleBinding = tb;
            }
            string rb = NeonConfig.ReloadKeyBinding.Value;
            if (_reload == null || _reloadBinding != rb)
            {
                _reload?.Dispose();
                _reload = new InputAction("NeonRunReload", InputActionType.Button, rb);
                _reload.Enable();
                _reloadBinding = rb;
            }
        }

        private void SetAnimBool(string name, bool value)
        {
            var an = _p.playerBodyAnimator;
            if (an == null) return;
            if (_paramsFor != an)
            {
                _paramsFor = an;
                _params = new HashSet<string>(an.parameters.Select(x => x.name));
                if (NeonConfig.DebugLog.Value)
                    Plugin.Log.LogInfo("Parâmetros do animator: " + string.Join(", ", _params));
            }
            if (_params.Contains(name)) an.SetBool(name, value);
        }

        // ---------------------------------------------------------------- LateUpdate: pose + efeitos

        private void LateUpdate()
        {
            if (_p == null) return;
            float dt = Time.deltaTime;
            bool local = IsLocal;

            // Quão rápido o jogador está se movendo (vale pra remotos também).
            Vector3 pos = _p.transform.position;
            if (_hasLastPos && dt > 0f)
            {
                Vector3 d = pos - _lastPos;
                d.y = 0f;
                float spd = d.magnitude / dt;
                if (spd > 40f) spd = 0f; // teleporte
                _moveFactor = Mathf.MoveTowards(_moveFactor, Mathf.Clamp01(spd / 4.5f), dt * 4f);
            }
            _lastPos = pos;
            _hasLastPos = true;

            bool showPose = _active && !_p.isPlayerDead && !_p.isClimbingLadder && !_p.inSpecialInteractAnimation;
            _weight = Mathf.MoveTowards(_weight, showPose ? 1f : 0f, dt / Mathf.Max(0.01f, NeonConfig.BlendTime.Value));

            float freq = Mathf.Max(0f, NeonConfig.SwingFrequency.Value) * Mathf.Lerp(0.35f, 1f, _moveFactor);
            _phase = Mathf.Repeat(_phase + dt * freq * Mathf.PI * 2f, Mathf.PI * 2f);

            EnsureRigs(local);
            if (_weight > 0.001f) ApplyPoses(local);

            UpdateFx(local, dt);
            UpdateAudio();

            if (local)
            {
                UpdateFov();
                if (_hud == null) _hud = new NeonHud();
                bool hudVisible = !_p.isPlayerDead && !_p.inTerminalMenu &&
                                  !(_p.quickMenuManager != null && _p.quickMenuManager.isMenuOpen);
                _hud.Tick(hudVisible, _weight, _moveFactor, _energy, dt);
            }
        }

        private void EnsureRigs(bool local)
        {
            bool bodyOk = _bodyRig != null && _bodyRig.Valid;
            bool armsOk = !local || (_armsRig != null && _armsRig.Valid);
            if ((bodyOk && armsOk) || Time.time < _nextRigTry) return;
            _nextRigTry = Time.time + 2f;

            if (!bodyOk && _p.thisPlayerModel != null)
                _bodyRig = ArmRig.Build("corpo " + PlayerIndex, _p.thisPlayerModel.bones);

            if (local && !armsOk)
            {
                var bones = new List<Transform>();
                if (_p.thisPlayerModelArms != null) bones.AddRange(_p.thisPlayerModelArms.bones);
                if (_p.playerModelArmsMetarig != null) bones.AddRange(_p.playerModelArmsMetarig.GetComponentsInChildren<Transform>(true));
                _armsRig = ArmRig.Build("braços 1ª pessoa", bones);
            }

            bool ok = _bodyRig != null && _bodyRig.Valid && (!local || (_armsRig != null && _armsRig.Valid));
            if (!ok && ++_rigFailures == 3)
            {
                Plugin.Log.LogWarning("Não achei os ossos dos braços. Ligue Debug/LogDetalhado=true na config e me mande o log.");
            }
        }

        private void ApplyPoses(bool local)
        {
            float w = _weight * Mathf.Clamp01(NeonConfig.PoseStrength.Value);
            float a = NeonConfig.SwingAmount.Value * _moveFactor;
            float swing = Mathf.Sin(_phase);
            float fingers = Mathf.Clamp01(NeonConfig.FingerStraighten.Value);

            // Segurando item: o braço do item quase não mexe.
            float wRight = w * (_p.isHoldingObject ? 0.25f : 1f);
            float wLeft = w * (_p.twoHanded ? 0.25f : 1f);

            // 3ª pessoa (o que os amigos veem). Espaço do corpo: x=direita, y=cima, z=frente.
            if (_bodyRig != null && _bodyRig.Valid)
            {
                Transform frame = _p.transform;
                PoseBodyArm(_bodyRig.Left, frame, -1f, -swing * a, wLeft, fingers);
                PoseBodyArm(_bodyRig.Right, frame, 1f, swing * a, wRight, fingers);
            }

            // 1ª pessoa (o que você vê). Espaço da câmera.
            if (local && !_p.isPlayerDead && _armsRig != null && _armsRig.Valid && _p.gameplayCamera != null)
            {
                Transform frame = _p.gameplayCamera.transform;
                PoseFirstPersonArm(_armsRig.Left, frame, -1f, -swing * a, wLeft, fingers);
                PoseFirstPersonArm(_armsRig.Right, frame, 1f, swing * a, wRight, fingers);
            }
        }

        /// <param name="side">-1 esquerdo, +1 direito.</param>
        /// <param name="p">balanço: + = braço indo pra frente.</param>
        private static void PoseBodyArm(Arm arm, Transform frame, float side, float p, float w, float fingers)
        {
            var upper = new Vector3(0.12f * side, -1f, 0.75f * p);
            var lower = new Vector3(-0.12f * side, 0.3f + 0.55f * p, 0.9f);
            var hand = lower + new Vector3(0f, 0.12f, 0.15f);
            ArmRig.Pose(arm, frame, upper, lower, hand, w, fingers);
        }

        private static void PoseFirstPersonArm(Arm arm, Transform frame, float side, float p, float w, float fingers)
        {
            float fwd = NeonConfig.FirstPersonArmsForward.Value;
            float lift = NeonConfig.FirstPersonArmsLift.Value;
            float spread = NeonConfig.FirstPersonArmsSpread.Value;
            var upper = new Vector3(spread * side, -0.9f, fwd + 0.55f * p);
            var lower = new Vector3(-0.6f * spread * side, lift + 0.55f * p, 1f);
            var hand = lower + new Vector3(0f, 0.1f, 0f);
            ArmRig.Pose(arm, frame, upper, lower, hand, w, fingers);
        }

        // ---------------------------------------------------------------- efeitos

        private void UpdateFx(bool local, float dt)
        {
            // Local vivo: efeitos nos braços de 1ª pessoa. Outros: no corpo.
            ArmRig target = local && !_p.isPlayerDead ? _armsRig : _bodyRig;
            if (target == null || !target.Valid)
            {
                DisposeFx();
                return;
            }

            if (_fxRig != target)
            {
                if (_weight <= 0.001f && _fxLeft == null) return; // cria só quando precisar
                DisposeFx();
                var renderer = target == _armsRig ? _p.thisPlayerModelArms : _p.thisPlayerModel;
                int layer = renderer != null ? renderer.gameObject.layer : _p.gameObject.layer;
                _fxLeft = new HandFx(target.Left, layer, transform);
                _fxRight = new HandFx(target.Right, layer, transform);
                _fxRig = target;
            }

            float intensity = _weight * Mathf.Lerp(0.6f, 1f, _moveFactor);
            _fxLeft.Tick(intensity, dt);
            _fxRight.Tick(intensity, dt);
        }

        private void DisposeFx()
        {
            _fxLeft?.Dispose();
            _fxRight?.Dispose();
            _fxLeft = _fxRight = null;
            _fxRig = null;
        }

        private void OnConfigReloaded()
        {
            // Recria tudo com as novas cores/quantidades.
            DisposeFx();
            _armsRig = null;
            _bodyRig = null;
            _nextRigTry = 0f;
            _rigFailures = 0;
        }

        private void EnsureAudio()
        {
            if (_hum != null) return;
            var go = new GameObject("NeonRunAudio");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var group = _p.movementAudio != null ? _p.movementAudio.outputAudioMixerGroup : null;

            _hum = go.AddComponent<AudioSource>();
            _sfx = go.AddComponent<AudioSource>();
            foreach (var s in new[] { _hum, _sfx })
            {
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 1.5f;
                s.maxDistance = 30f;
                s.dopplerLevel = 0f;
                s.outputAudioMixerGroup = group;
            }
            _hum.loop = true;
            _hum.clip = NeonAudio.Hum;
        }

        private void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            EnsureAudio();
            _sfx.PlayOneShot(clip, Mathf.Clamp01(NeonConfig.Volume.Value) * volumeScale);
        }

        private void UpdateAudio()
        {
            if (_weight <= 0.001f)
            {
                if (_hum != null && _hum.isPlaying) _hum.Stop();
                return;
            }
            EnsureAudio();
            if (!_hum.isPlaying) _hum.Play();
            _hum.volume = Mathf.Clamp01(NeonConfig.Volume.Value) * _weight * Mathf.Lerp(0.45f, 1f, _moveFactor);
            _hum.pitch = Mathf.Lerp(0.9f, 1.15f, _moveFactor);
        }

        // ---------------------------------------------------------------- câmera

        private void UpdateFov()
        {
            var cam = _p.gameplayCamera;
            if (cam == null) return;
            RemoveFov();
            if (_p.isPlayerDead) return;
            float off = NeonConfig.FovBoost.Value * _weight * Mathf.Lerp(0.4f, 1f, _moveFactor);
            if (Mathf.Abs(off) < 0.01f) return;
            cam.fieldOfView += off;
            _fovApplied = off;
        }

        private void RemoveFov()
        {
            if (_fovApplied == 0f) return;
            if (_p != null && _p.gameplayCamera != null) _p.gameplayCamera.fieldOfView -= _fovApplied;
            _fovApplied = 0f;
        }
    }
}
