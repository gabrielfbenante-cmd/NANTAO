using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace ChineloArremessavel
{
    [BepInPlugin(Guid, "Chinelo Arremessavel", "1.2.0")]
    public sealed partial class ChineloPlugin : BaseUnityPlugin
    {
        public const string Guid = "gabri.chineloarremessavel";
        public static ChineloPlugin Instance;
        public static GameObject Prefab;
        public static Item ChineloItem;
        public static readonly List<ChineloBehaviour> Instances = new List<ChineloBehaviour>();
        private NetworkManager registeredManager;
        private Harmony harmony;
        private float nextAttempt;
        internal ConfigEntry<float> range;
        internal ConfigEntry<int> damage;
        internal ConfigEntry<float> flightSpeed;
        internal ConfigEntry<float> blastRadius;
        internal ConfigEntry<float> explodeChance;
        internal ConfigEntry<float> explodeMultiplier;
        internal ConfigEntry<int> explodePlayerDamage;
        internal ConfigEntry<float> knockForce;
        internal ConfigEntry<float> knockSeconds;
        internal ConfigEntry<bool> dropItem;
        internal ConfigEntry<float> explodeVisualScale;
        internal ConfigEntry<float> explodeVolume;

        private void Awake()
        {
            Instance = this;
            range = Config.Bind("Chinelo", "Alcance", 12f, new ConfigDescription("Distancia do voo reto antes de explodir, em metros. Colisoes antecipam a explosao.", new AcceptableValueRange<float>(3f, 25f)));
            damage = Config.Bind("Chinelo", "ForcaContraMonstros", 1, new ConfigDescription("Forca da explosao contra monstros vulneraveis, por alvo. Alguns monstros sao imunes.", new AcceptableValueRange<int>(0, 3)));
            flightSpeed = Config.Bind("Chinelo", "Velocidade", 14f, new ConfigDescription("Velocidade do voo reto em metros por segundo.", new AcceptableValueRange<float>(6f, 30f)));
            // Porrada: acertou alguem -> empurrao forte + desmaio. Explosao so em uma parte das vezes.
            knockForce = Config.Bind("Porrada", "ForcaDoEmpurrao", 22f, new ConfigDescription("Forca do empurrao em quem leva a chinelada (m/s).", new AcceptableValueRange<float>(0f, 60f)));
            knockSeconds = Config.Bind("Porrada", "TempoDesmaiado", 4f, new ConfigDescription("Segundos caido no chao, com o corpo mole, antes de levantar.", new AcceptableValueRange<float>(1f, 15f)));
            dropItem = Config.Bind("Porrada", "LargarItemDaMao", true, "Quem desmaia larga o item que estava segurando.");
            explodeChance = Config.Bind("Explosao", "ChanceDeExplodir", 10f, new ConfigDescription("Chance (%) do chinelo explodir quando bate ou chega no alcance. No resto ele cai no chao e pode ser pego de novo.", new AcceptableValueRange<float>(0f, 100f)));
            explodeMultiplier = Config.Bind("Explosao", "Multiplicador", 5f, new ConfigDescription("Quantas vezes a explosao e mais forte que a original (forca contra monstros e empurrao).", new AcceptableValueRange<float>(1f, 10f)));
            blastRadius = Config.Bind("Explosao", "Raio", 6f, new ConfigDescription("Raio da explosao (m): monstros tomam dano e jogadores sao arremessados e desmaiam.", new AcceptableValueRange<float>(1f, 15f)));
            explodeVisualScale = Config.Bind("Explosao", "TamanhoVisual", 3f, new ConfigDescription("Tamanho do efeito visual (1 = explosao normal do jogo, 3 = tres vezes maior).", new AcceptableValueRange<float>(1f, 6f)));
            explodeVolume = Config.Bind("Explosao", "Volume", 1f, new ConfigDescription("Volume do estrondo (0 a 1).", new AcceptableValueRange<float>(0f, 1f)));
            explodePlayerDamage = Config.Bind("Explosao", "DanoEmJogadores", 0, new ConfigDescription("Dano da explosao em jogadores (0 = so arremessa e desmaia, sem tirar vida).", new AcceptableValueRange<int>(0, 100)));
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(ChineloPlugin).Assembly);
            Logger.LogInfo("Chinelo Arremessavel 1.2.0 loaded. Hit = knockback + knockout; " + explodeChance.Value + "% chance of a x" + explodeMultiplier.Value + " explosion.");
        }

        private void Update()
        {
            UpdateFlightNetwork();
            if (Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + 1f;
            try { EnsurePrefab(); RegisterItem(); }
            catch (Exception exception) { Logger.LogError("Chinelo initialization failed: " + exception); nextAttempt += 15f; }
        }

        public void EnsurePrefab()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager == registeredManager) return;
            if (manager.IsListening && manager.NetworkConfig.ForceSamePrefabs) return;
            if (Prefab == null)
            {
                GameObject source = null;
                foreach (NetworkPrefab entry in manager.NetworkConfig.Prefabs.Prefabs)
                {
                    if (entry.Prefab == null) continue;
                    PhysicsProp prop = entry.Prefab.GetComponent<PhysicsProp>();
                    if (prop == null || prop.itemProperties == null) continue;
                    if (source == null) source = entry.Prefab;
                    if (prop.itemProperties.itemName.ToLowerInvariant().Contains("metal sheet")) { source = entry.Prefab; break; }
                }
                if (source == null)
                {
                    foreach (Item candidate in Resources.FindObjectsOfTypeAll<Item>())
                    {
                        if (candidate.spawnPrefab != null && candidate.spawnPrefab.GetComponent<PhysicsProp>() != null)
                        { source = candidate.spawnPrefab; break; }
                    }
                }
                if (source == null) return;

                GameObject holder = new GameObject("ChineloRuntimeTemplates");
                holder.SetActive(false);
                DontDestroyOnLoad(holder);
                Prefab = UnityEngine.Object.Instantiate(source, holder.transform);
                Prefab.name = "ChineloArremessavel";
                Prefab.SetActive(false);
                Prefab.transform.localPosition = Vector3.zero;
                Prefab.transform.localRotation = Quaternion.identity;
                Prefab.transform.localScale = Vector3.one;
                PhysicsProp native = Prefab.GetComponent<PhysicsProp>();
                ChineloItem = UnityEngine.Object.Instantiate(native.itemProperties);
                ChineloItem.name = "ChineloItemRuntime";
                ChineloItem.itemName = "Chinelo arremessavel";
                ChineloItem.itemId = 684201;
                ChineloItem.spawnPrefab = Prefab;
                ChineloItem.isScrap = false;
                ChineloItem.requiresBattery = false;
                ChineloItem.twoHanded = false;
                ChineloItem.twoHandedAnimation = false;
                ChineloItem.weight = 1f;
                ChineloItem.canBeGrabbedBeforeGameStart = true;
                ChineloItem.syncUseFunction = false;
                ChineloItem.saveItemVariable = false;
                ChineloItem.itemIsTrigger = false;
                ChineloItem.grabAnimationTime = 0.2f;
                ChineloItem.restingRotation = Vector3.zero;
                ChineloItem.rotationOffset = new Vector3(-90f, 0f, 0f);
                ChineloItem.positionOffset = new Vector3(0f, 0.03f, 0f);
                ChineloItem.verticalOffset = 0.02f;
                ChineloItem.floorYOffset = 0;
                ChineloItem.toolTips = new string[] { "Arremessar: [LMB]", "Largar: [G]", "Outro chinelo: [F6]" };
                native.itemProperties = ChineloItem;
                native.scrapValue = 0;
                native.grabbable = true;
                native.grabbableToEnemies = false;

                Material template = native.mainObjectRenderer != null ? native.mainObjectRenderer.sharedMaterial : null;
                foreach (Renderer renderer in Prefab.GetComponentsInChildren<Renderer>(true)) UnityEngine.Object.DestroyImmediate(renderer);
                foreach (Collider collider in Prefab.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Animator animator in Prefab.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                BoxCollider box = Prefab.AddComponent<BoxCollider>();
                box.size = new Vector3(0.15f, 0.09f, 0.32f);
                box.center = new Vector3(0, 0.025f, 0);
                native.propColliders = new Collider[] { box };
                GameObject visual = ChineloModel.Build(Prefab.transform, template);
                native.mainObjectRenderer = visual.GetComponentInChildren<MeshRenderer>();
                ChineloItem.itemIcon = ChineloModel.MakeIcon();
                foreach (ScanNodeProperties scan in Prefab.GetComponentsInChildren<ScanNodeProperties>(true))
                {
                    scan.headerText = "Chinelo arremessavel";
                    scan.subText = "Chinelada explosiva";
                    scan.scrapValue = 0;
                }
                ChineloBehaviour behaviour = Prefab.AddComponent<ChineloBehaviour>();
                behaviour.Native = native;
                behaviour.Visual = visual.transform;
                NetworkObject networkObject = Prefab.GetComponent<NetworkObject>();
                AccessTools.Field(typeof(NetworkObject), "GlobalObjectIdHash").SetValue(networkObject, 0xC41E10A1u);
                AccessTools.Field(typeof(NetworkObject), "PrefabGlobalObjectIdHash").SetValue(networkObject, 0xC41E10A1u);
                Logger.LogInfo("Chinelo runtime prefab created from " + source.name);
            }
            manager.AddNetworkPrefab(Prefab);
            registeredManager = manager;
            Logger.LogInfo("Chinelo network prefab registered. Listening=" + manager.IsListening);
        }

        public static void RegisterItem()
        {
            if (ChineloItem != null && StartOfRound.Instance != null && StartOfRound.Instance.allItemsList != null &&
                !StartOfRound.Instance.allItemsList.itemsList.Contains(ChineloItem))
                StartOfRound.Instance.allItemsList.itemsList.Add(ChineloItem);
        }

        public static void Tip(string message)
        {
            if (HUDManager.Instance != null) HUDManager.Instance.DisplayTip("Teste do chinelo", message, false, false, "");
        }

        internal void Throw(ChineloBehaviour chinelo)
        {
            GrabbableObject item = chinelo.Native;
            PlayerControllerB player = item.playerHeldBy;
            if (player == null || !player.IsOwner || player.isGrabbingObjectAnimation || player.inSpecialInteractAnimation) return;
            if (chinelo.IsFlying || chinelo.Pending || player.isTypingChat || player.inTerminalMenu) return;
            RequestFlight(chinelo, player);
        }

        private void OnDestroy()
        {
            CleanupFlightNetwork();
            if (harmony != null) harmony.UnpatchSelf();
            Instance = null;
        }
    }

    [HarmonyPatch(typeof(GrabbableObject), "ItemActivate")]
    internal static class ActivatePatch
    {
        private static bool Prefix(GrabbableObject __instance, bool buttonDown)
        {
            ChineloBehaviour chinelo = __instance.GetComponent<ChineloBehaviour>();
            if (chinelo == null) return true;
            if (buttonDown && ChineloPlugin.Instance != null) ChineloPlugin.Instance.Throw(chinelo);
            return false;
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "Awake")]
    internal static class ItemsPatch
    {
        private static void Postfix() { ChineloPlugin.RegisterItem(); }
    }

    [HarmonyPatch(typeof(NetworkManager), "StartHost")]
    internal static class BeforeHostPatch
    {
        private static void Prefix() { if (ChineloPlugin.Instance != null) ChineloPlugin.Instance.EnsurePrefab(); }
    }

    [HarmonyPatch(typeof(NetworkManager), "StartClient")]
    internal static class BeforeClientPatch
    {
        private static void Prefix() { if (ChineloPlugin.Instance != null) ChineloPlugin.Instance.EnsurePrefab(); }
    }

    // Creative props are temporary. Mark only our props as excluded while the
    // game's save routine runs, then restore every original flag even on error.
    [HarmonyPatch(typeof(GameNetworkManager), "SaveItemsInShip")]
    internal static class DoNotSaveCreativeItems
    {
        private static void Prefix(out Dictionary<GrabbableObject, bool> __state)
        {
            __state = new Dictionary<GrabbableObject, bool>();
            foreach (ChineloBehaviour chinelo in ChineloPlugin.Instances)
                if (chinelo != null && chinelo.Native != null)
                { __state[chinelo.Native] = chinelo.Native.itemUsedUp; chinelo.Native.itemUsedUp = true; }
        }
        private static void Finalizer(Dictionary<GrabbableObject, bool> __state)
        {
            if (__state == null) return;
            foreach (KeyValuePair<GrabbableObject, bool> pair in __state)
                if (pair.Key != null) pair.Key.itemUsedUp = pair.Value;
        }
    }
}
