using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Nantao
{
    /// <summary>
    /// Um por jogador (local e remotos). Guarda o personagem escolhido, cria a habilidade,
    /// lê a tecla (no dono) e oferece utilidades comuns: esqueleto dos braços, som, FOV, HUD.
    /// Roda depois dos scripts do jogo pra pose ficar por cima da animação original.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    internal sealed class PlayerAgent : MonoBehaviour
    {
        private static readonly Dictionary<PlayerControllerB, PlayerAgent> All = new Dictionary<PlayerControllerB, PlayerAgent>();

        public static PlayerAgent For(PlayerControllerB p)
        {
            if (p == null) return null;
            return All.TryGetValue(p, out var r) ? r : null;
        }

        public static PlayerAgent Local
        {
            get
            {
                var gnm = GameNetworkManager.Instance;
                return gnm != null ? For(gnm.localPlayerController) : null;
            }
        }

        public PlayerControllerB Player { get; private set; }
        public int CharacterId { get; private set; } = Characters.None;
        public Ability Ability { get; private set; }
        public float MoveFactor { get; private set; }

        public bool IsLocal =>
            Player != null && GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController == Player;

        public int PlayerIndex => (int)Player.playerClientId;

        // Esqueletos
        public ArmRig ArmsRig { get; private set; } // 1ª pessoa (só local)
        public ArmRig BodyRig { get; private set; } // corpo (o que os outros veem)
        private float _nextRigTry;
        private int _rigFailures;

        private Vector3 _lastPos;
        private bool _hasLastPos;

        // Som
        public AudioSource Loop { get; private set; }
        private AudioSource _sfx;

        // Câmera / HUD / menu (só local)
        private float _fovApplied;
        private AgentHud _hud;
        private CharacterMenu _menu;
        private bool _autoMenuShown;

        // Input
        private InputAction _ability, _menuKey, _reload;
        private string _abilityBind, _menuBind, _reloadBind;
        private float _pressTime = -1f;

        // Animator
        private Animator _paramsFor;
        private HashSet<string> _params;

        // Estado dos renderers enquanto vivo (pra restaurar ao reviver)
        private readonly Dictionary<Renderer, (bool enabled, ShadowCastingMode shadow)> _rendererSnapshot =
            new Dictionary<Renderer, (bool, ShadowCastingMode)>();
        private float _nextSnapshot;

        public void Init(PlayerControllerB p)
        {
            Player = p;
            All[p] = this;
            ModConfig.Reloaded += OnConfigReloaded;
        }

        private void OnDestroy()
        {
            if (Player != null) All.Remove(Player);
            ModConfig.Reloaded -= OnConfigReloaded;
            Ability?.Dispose();
            Ability = null;
            _ability?.Dispose();
            _menuKey?.Dispose();
            _reload?.Dispose();
            _hud?.Dispose();
            _menu?.Dispose();
            RemoveFov();
        }

        // ---------------------------------------------------------------- personagem

        public void ApplyCharacter(int id)
        {
            if (id == CharacterId) return;
            Ability?.Dispose();
            Ability = null;
            CharacterId = id;
            var def = Characters.Get(id);
            if (def != null && def.Implemented) Ability = def.Create(this);
            if (ModConfig.DebugLog.Value)
                Plugin.Log.LogInfo($"Jogador {PlayerIndex} agora é: {(def != null ? def.Name : "sem personagem")}");
        }

        public bool CanSwapNow =>
            ModConfig.AllowSwapMidRound.Value || (StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase);

        // ---------------------------------------------------------------- Update do jogo

        public void BeforeGameUpdate() => RemoveFov();

        public void AfterGameUpdate()
        {
            if (Player == null || !IsLocal) return;
            EnsureInput();
            InputCompat.Tick();

            if (_reload.WasPressedThisFrame()) Plugin.ReloadConfig();

            bool pauseOpen = Player.quickMenuManager != null && Player.quickMenuManager.isMenuOpen;
            bool busy = Player.inTerminalMenu || Player.isTypingChat || pauseOpen;

            // Menu de personagem
            if (_menu == null) _menu = new CharacterMenu();
            if (pauseOpen && _menu.IsOpen) _menu.Close(false);
            if (!busy && _menuKey.WasPressedThisFrame())
            {
                if (_menu.IsOpen) _menu.Close();
                else if (CanSwapNow) _menu.Open();
                else Hud.Toast("Só dá pra trocar de personagem com a nave em órbita.", new Color(1f, 0.5f, 0.3f));
            }
            if (!_autoMenuShown && ModConfig.OpenMenuOnJoin.Value && Player.isPlayerControlled && CanSwapNow
                && CharacterId == Characters.None && !busy)
            {
                _autoMenuShown = true;
                _menu.Open();
            }
            _menu.Tick(this);

            // Tecla da habilidade
            bool blocked = busy || _menu.IsOpen;
            if (Ability != null && !blocked)
            {
                if (_ability.WasPressedThisFrame())
                {
                    _pressTime = Time.time;
                    Ability.OnPress();
                }
                if (_pressTime >= 0f && _ability.IsPressed()) Ability.OnHold(Time.time - _pressTime);
                if (_pressTime >= 0f && _ability.WasReleasedThisFrame())
                {
                    Ability.OnRelease(Time.time - _pressTime);
                    _pressTime = -1f;
                }
            }
            else if (_pressTime >= 0f)
            {
                Ability?.OnRelease(Time.time - _pressTime);
                _pressTime = -1f;
            }

            Ability?.OwnerTick(Time.deltaTime);
        }

        private void EnsureInput()
        {
            Rebind(ref _ability, ref _abilityBind, "NantaoAbility", ModConfig.AbilityKey.Value);
            Rebind(ref _menuKey, ref _menuBind, "NantaoMenu", ModConfig.MenuKey.Value);
            Rebind(ref _reload, ref _reloadBind, "NantaoReload", ModConfig.ReloadKeyBinding.Value);
        }

        private static void Rebind(ref InputAction action, ref string current, string name, string binding)
        {
            if (action != null && current == binding) return;
            action?.Dispose();
            action = new InputAction(name, InputActionType.Button, binding);
            action.Enable();
            current = binding;
        }

        public void SetAnimBool(string name, bool value)
        {
            var an = Player.playerBodyAnimator;
            if (an == null) return;
            if (_paramsFor != an)
            {
                _paramsFor = an;
                _params = new HashSet<string>(an.parameters.Select(x => x.name));
                if (ModConfig.DebugLog.Value) Plugin.Log.LogInfo("Parâmetros do animator: " + string.Join(", ", _params));
            }
            if (_params.Contains(name)) an.SetBool(name, value);
        }

        // ---------------------------------------------------------------- LateUpdate

        private void LateUpdate()
        {
            if (Player == null) return;
            float dt = Time.deltaTime;

            Vector3 pos = Player.transform.position;
            if (_hasLastPos && dt > 0f)
            {
                Vector3 d = pos - _lastPos;
                d.y = 0f;
                float spd = d.magnitude / dt;
                if (spd > 40f) spd = 0f; // teleporte
                MoveFactor = Mathf.MoveTowards(MoveFactor, Mathf.Clamp01(spd / 4.5f), dt * 4f);
            }
            _lastPos = pos;
            _hasLastPos = true;

            if (!Player.isPlayerDead && Player.isPlayerControlled && Time.time >= _nextSnapshot)
            {
                _nextSnapshot = Time.time + 1f;
                SnapshotRenderers();
            }

            EnsureRigs();
            Ability?.LateTick(dt);

            if (IsLocal)
            {
                UpdateFov();
                Hud.BeginFrame();
                Ability?.HudTick(Hud);
                bool visible = !Player.inTerminalMenu &&
                               !(Player.quickMenuManager != null && Player.quickMenuManager.isMenuOpen);
                Hud.EndFrame(visible, this, dt);
            }
        }

        public AgentHud Hud => _hud ?? (_hud = new AgentHud());

        // ---------------------------------------------------------------- esqueleto

        private void EnsureRigs()
        {
            bool local = IsLocal;
            bool bodyOk = BodyRig != null && BodyRig.Valid;
            bool armsOk = !local || (ArmsRig != null && ArmsRig.Valid);
            if ((bodyOk && armsOk) || Time.time < _nextRigTry) return;
            _nextRigTry = Time.time + 2f;

            if (!bodyOk && Player.thisPlayerModel != null)
                BodyRig = ArmRig.Build("corpo " + PlayerIndex, Player.thisPlayerModel.bones);

            if (local && !armsOk)
            {
                var bones = new List<Transform>();
                if (Player.thisPlayerModelArms != null) bones.AddRange(Player.thisPlayerModelArms.bones);
                if (Player.playerModelArmsMetarig != null)
                    bones.AddRange(Player.playerModelArmsMetarig.GetComponentsInChildren<Transform>(true));
                ArmsRig = ArmRig.Build("braços 1ª pessoa", bones);
            }

            bool ok = BodyRig != null && BodyRig.Valid && (!local || (ArmsRig != null && ArmsRig.Valid));
            if (!ok && ++_rigFailures == 3)
                Plugin.Log.LogWarning("Não achei os ossos dos braços. Ligue Debug/LogDetalhado=true e mande o log.");
        }

        /// <summary>Esqueleto onde os efeitos das mãos devem aparecer neste computador.</summary>
        public ArmRig FxRig => IsLocal && !Player.isPlayerDead ? ArmsRig : BodyRig;

        public int FxLayer
        {
            get
            {
                var r = FxRig == ArmsRig ? Player.thisPlayerModelArms : Player.thisPlayerModel;
                return r != null ? r.gameObject.layer : Player.gameObject.layer;
            }
        }

        /// <summary>
        /// Pose de um braço. Direções em (x=direita, y=cima, z=frente) do corpo (3ª pessoa)
        /// e da câmera (1ª pessoa).
        /// </summary>
        public void PoseArm(bool right, Vector3 bodyUpper, Vector3 bodyLower, Vector3 fpUpper, Vector3 fpLower,
                            float weight, float fingers = 0.6f)
        {
            if (weight <= 0.001f) return;
            if (BodyRig != null && BodyRig.Valid)
            {
                var arm = right ? BodyRig.Right : BodyRig.Left;
                ArmRig.Pose(arm, Player.transform, bodyUpper, bodyLower, bodyLower, weight, fingers);
            }
            if (IsLocal && !Player.isPlayerDead && ArmsRig != null && ArmsRig.Valid && Player.gameplayCamera != null)
            {
                var arm = right ? ArmsRig.Right : ArmsRig.Left;
                ArmRig.Pose(arm, Player.gameplayCamera.transform, fpUpper, fpLower, fpLower, weight, fingers);
            }
        }

        /// <summary>Espelha uma direção pro braço esquerdo.</summary>
        public static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        // ---------------------------------------------------------------- som

        private void EnsureAudio()
        {
            if (Loop != null) return;
            var go = new GameObject("NantaoAudio");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Loop = go.AddComponent<AudioSource>();
            _sfx = go.AddComponent<AudioSource>();
            Sounds.Setup3D(Loop);
            Sounds.Setup3D(_sfx);
            Loop.loop = true;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            EnsureAudio();
            _sfx.PlayOneShot(clip, Mathf.Clamp01(ModConfig.Volume.Value) * volumeScale);
        }

        /// <summary>Toca um loop no jogador (volume 0 = para).</summary>
        public void SetLoop(AudioClip clip, float volume, float pitch = 1f)
        {
            if (volume <= 0.001f || clip == null)
            {
                if (Loop != null && Loop.isPlaying) Loop.Stop();
                return;
            }
            EnsureAudio();
            if (Loop.clip != clip) { Loop.clip = clip; Loop.Stop(); }
            if (!Loop.isPlaying) Loop.Play();
            Loop.volume = Mathf.Clamp01(ModConfig.Volume.Value) * volume;
            Loop.pitch = pitch;
        }

        // ---------------------------------------------------------------- câmera

        private void UpdateFov()
        {
            var cam = Player.gameplayCamera;
            if (cam == null) return;
            RemoveFov();
            if (Player.isPlayerDead || Ability == null) return;
            float off = Ability.FovOffset;
            if (Mathf.Abs(off) < 0.01f) return;
            cam.fieldOfView += off;
            _fovApplied = off;
        }

        private void RemoveFov()
        {
            if (_fovApplied == 0f) return;
            if (Player != null && Player.gameplayCamera != null) Player.gameplayCamera.fieldOfView -= _fovApplied;
            _fovApplied = 0f;
        }

        // ---------------------------------------------------------------- reviver

        private void SnapshotRenderers()
        {
            _rendererSnapshot.Clear();
            foreach (var r in new Renderer[] { Player.thisPlayerModel, Player.thisPlayerModelLOD1, Player.thisPlayerModelLOD2, Player.thisPlayerModelArms })
            {
                if (r != null) _rendererSnapshot[r] = (r.enabled, r.shadowCastingMode);
            }
        }

        public void RestoreRenderers()
        {
            foreach (var kv in _rendererSnapshot)
            {
                if (kv.Key == null) continue;
                kv.Key.enabled = kv.Value.enabled;
                kv.Key.shadowCastingMode = kv.Value.shadow;
            }
        }

        private void OnConfigReloaded()
        {
            ArmsRig = null;
            BodyRig = null;
            _nextRigTry = 0f;
            _rigFailures = 0;
            // Recria a minha habilidade pra pegar cores/quantidades novas.
            if (!IsLocal) return;
            int id = CharacterId;
            ApplyCharacter(Characters.None);
            ApplyCharacter(id);
        }
    }
}
