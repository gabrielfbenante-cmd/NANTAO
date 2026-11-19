using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using ChineloArremessavel;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModoCriativoTeste
{
    [BepInPlugin("gabri.modocriativoteste", "Modo Criativo - Teste do Chinelo", "1.0.0")]
    [BepInDependency(ChineloPlugin.Guid, "1.0.0")]
    public sealed class CreativePlugin : BaseUnityPlugin
    {
        public static CreativePlugin Instance;
        private ConfigEntry<bool> creative;
        private ConfigEntry<bool> automatic;
        internal ConfigEntry<bool> separateSave;
        private PlayerControllerB seenPlayer;
        private bool gaveItem;
        private bool pending;
        private float readySince;
        private float nextKey;
        private float lastToggle = -10f;
        private QuickMenuManager quickMenu;

        private void Awake()
        {
            Instance = this;
            creative = Config.Bind("Criativo", "Ativado", true, "Entrega chinelo gratuitamente para testar. F7 liga/desliga.");
            automatic = Config.Bind("Criativo", "ChineloAoEntrar", true, "Entrega um chinelo na mao quando o anfitriao fica pronto.");
            separateSave = Config.Bind("Criativo", "UsarSaveDeTeste", true, "Ao hospedar com o criativo ativo, usa o arquivo ChineloCriativoTeste em vez dos saves normais. Mude antes de hospedar.");
            new Harmony("gabri.modocriativoteste").PatchAll(typeof(CreativePlugin).Assembly);
            Logger.LogInfo("Modo Criativo Teste 1.0.0 loaded. F6: chinelo; F7: toggle creative. Host-only spawning.");
        }

        [HarmonyPatch(typeof(NetworkManager), "StartHost")]
        private static class TestSavePatch
        {
            private static void Prefix()
            {
                if (Instance != null && Instance.creative.Value && Instance.separateSave.Value && GameNetworkManager.Instance != null)
                {
                    GameNetworkManager.Instance.currentSaveFileName = "ChineloCriativoTeste";
                    Instance.Logger.LogInfo("Using separate creative test save: ChineloCriativoTeste");
                }
            }
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            PlayerControllerB player = GameNetworkManager.Instance == null ? null : GameNetworkManager.Instance.localPlayerController;
            if (player == null || manager == null || !manager.IsHost)
            {
                seenPlayer = null; gaveItem = false; readySince = 0;
                return;
            }
            if (player != seenPlayer)
            { seenPlayer = player; gaveItem = false; readySince = Time.unscaledTime; }
            if (player.isPlayerDead) { gaveItem = false; readySince = Time.unscaledTime; return; }
            if (!player.isPlayerControlled || player.inSpecialInteractAnimation || player.isGrabbingObjectAnimation) return;
            if (quickMenu == null) quickMenu = UnityEngine.Object.FindObjectOfType<QuickMenuManager>();
            if (!Application.isFocused) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame)
            {
                ToggleCreative();
            }
            if (!creative.Value || pending) return;
            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame && Time.unscaledTime > nextKey)
            { RequestChinelo(); }
            else if (automatic.Value && !gaveItem && Time.unscaledTime - readySince > 4f)
            { gaveItem = true; StartCoroutine(Give(player)); }
        }

        private void ToggleCreative()
        {
            if (Time.unscaledTime - lastToggle < 0.3f) return;
            lastToggle = Time.unscaledTime;
            creative.Value = !creative.Value;
            ChineloPlugin.Tip(creative.Value ? "Criativo ligado. F6 entrega um chinelo." : "Criativo desligado. O chinelo continua utilizavel.");
        }

        private void RequestChinelo()
        {
            if (!creative.Value || pending || Time.unscaledTime <= nextKey) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || GameNetworkManager.Instance == null) return;
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            if (player == null || player.isPlayerDead || !player.isPlayerControlled || player.isGrabbingObjectAnimation) return;
            nextKey = Time.unscaledTime + 2f;
            if (quickMenu != null && quickMenu.isMenuOpen) quickMenu.CloseQuickMenu();
            StartCoroutine(Give(player));
        }

        private void OnGUI()
        {
            // IMGUI fallback also accepts synthetic/accessibility keyboard input
            // that can be absent from the new Input System's raw keyboard state.
            if (Event.current.type == EventType.KeyDown && !Event.current.isMouse)
            {
                if (Event.current.keyCode == KeyCode.F6) { RequestChinelo(); Event.current.Use(); }
                else if (Event.current.keyCode == KeyCode.F7) { ToggleCreative(); Event.current.Use(); }
            }
            if (quickMenu == null || !quickMenu.isMenuOpen || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
            GUI.Box(new Rect(12, 58, 245, 98), "CRIATIVO - TESTE DO CHINELO");
            if (GUI.Button(new Rect(22, 88, 225, 26), "Entregar / recuperar chinelo [F6]")) RequestChinelo();
            if (GUI.Button(new Rect(22, 120, 225, 26), creative.Value ? "Criativo: ligado [F7]" : "Criativo: desligado [F7]")) ToggleCreative();
        }

        private IEnumerator Give(PlayerControllerB player)
        {
            pending = true;
            try
            {
                if (ChineloPlugin.Prefab == null)
                { ChineloPlugin.Tip("O chinelo nao carregou. Volte ao menu principal e confira o log."); yield break; }
                if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.GetComponent<ChineloBehaviour>() != null)
                { ChineloPlugin.Tip("Voce ja esta com o chinelo! Clique esquerdo arremessa; G larga."); yield break; }
                ChineloPlugin.RegisterItem();
                ChineloBehaviour existing = null;
                foreach (ChineloBehaviour candidate in ChineloPlugin.Instances)
                    if (candidate != null && candidate.Native != null && !candidate.IsFlying && !candidate.Pending &&
                        !candidate.Native.itemUsedUp && !candidate.Native.isHeld && !candidate.Native.isPocketed)
                    { existing = candidate; break; }
                GameObject spawned;
                Vector3 position = player.gameplayCamera.transform.position + player.gameplayCamera.transform.forward * 0.8f;
                if (existing != null)
                {
                    spawned = existing.gameObject;
                    existing.Native.fallTime = 0;
                    existing.Native.reachedFloorTarget = false;
                    spawned.transform.position = position;
                }
                else
                {
                    if (ChineloPlugin.Instances.Count >= 8)
                    { ChineloPlugin.Tip("Limite de 8 chinelos. Recupere um dos existentes."); yield break; }
                    spawned = UnityEngine.Object.Instantiate(ChineloPlugin.Prefab, position, Quaternion.identity, StartOfRound.Instance.propsContainer);
                    spawned.SetActive(true);
                    spawned.GetComponent<NetworkObject>().Spawn(false);
                }
                GrabbableObject item = spawned.GetComponent<GrabbableObject>();
                item.fallTime = 1f;
                item.reachedFloorTarget = true;
                item.targetFloorPosition = item.transform.localPosition;
                item.startFallingPosition = item.transform.localPosition;
                yield return null;
                yield return new WaitForFixedUpdate();
                if (player == null || item == null) yield break;
                item.transform.position = player.gameplayCamera.transform.position + player.gameplayCamera.transform.forward * 0.8f;
                item.targetFloorPosition = item.transform.localPosition;
                item.startFallingPosition = item.transform.localPosition;
                Physics.SyncTransforms();
                if (player.currentlyHeldObjectServer == null)
                {
                    // Use the game's complete grab path, including server validation,
                    // inventory slots, hand pose and the native pickup coroutine.
                    AccessTools.Method(typeof(PlayerControllerB), "BeginGrabObject").Invoke(player, null);
                    yield return new WaitForSeconds(0.8f);
                    if (item.isHeld)
                    { ChineloPlugin.Tip("Chinelo na mao! Clique esquerdo arremessa. F6 recupera; F7 liga/desliga o criativo."); Logger.LogInfo("Chinelo auto-equipped successfully."); }
                    else
                    { item.FallToGround(false, false, Vector3.zero); ChineloPlugin.Tip("Chinelo entregue a sua frente. Olhe para ele e aperte E para pegar."); Logger.LogWarning("Automatic grab did not complete; item remains available for normal pickup."); }
                }
                else
                { item.FallToGround(false, false, Vector3.zero); ChineloPlugin.Tip("Mao ocupada: chinelo entregue a sua frente. Use E para pegar."); }
            }
            finally { pending = false; }
        }
    }
}
