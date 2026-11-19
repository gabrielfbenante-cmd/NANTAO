using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SustoNantao
{
    [BepInPlugin(Guid, "SustoNantao", "1.1.0")]
    public sealed class SustoPlugin : BaseUnityPlugin
    {
        public const string Guid = "nantao.susto";

        internal static SustoPlugin Instance;
        internal ConfigEntry<bool> scareEnabled, onlyInside, screamForAll, useDefaultFace, useDefaultScream;
        internal ConfigEntry<float> chancePerMinute, minInterval, firstDelay, duration, volume;
        internal ConfigEntry<string> testKey, types;
        internal ConfigEntry<bool> suspenseEnabled, debugLog;
        internal ConfigEntry<float> buildMinutes, maxEventsPerMinute, suspenseVolume;

        private void Awake()
        {
            Instance = this;
            scareEnabled = Config.Bind("Geral", "Ativado", true, "Liga/desliga os sustos.");
            chancePerMinute = Config.Bind("Geral", "ChancePorMinuto", 6f, new ConfigDescription(
                "Chance (%) de levar um susto a cada minuto dentro do predio.", new AcceptableValueRange<float>(0f, 100f)));
            minInterval = Config.Bind("Geral", "IntervaloMinimo", 240f, new ConfigDescription(
                "Segundos minimos entre um susto e outro.", new AcceptableValueRange<float>(10f, 3600f)));
            firstDelay = Config.Bind("Geral", "PrimeiroSustoDepois", 90f, new ConfigDescription(
                "Segundos dentro do predio antes do primeiro susto poder acontecer.", new AcceptableValueRange<float>(0f, 1800f)));
            onlyInside = Config.Bind("Geral", "SoDentroDoPredio", true, "Sustos so acontecem dentro do predio (fora da nave).");
            duration = Config.Bind("Geral", "Duracao", .9f, new ConfigDescription("Duracao do susto (s).", new AcceptableValueRange<float>(.3f, 3f)));
            volume = Config.Bind("Geral", "Volume", 1f, new ConfigDescription("Volume do grito.", new AcceptableValueRange<float>(0f, 1f)));
            screamForAll = Config.Bind("Geral", "AmigosOuvemOGrito", true, "Os amigos perto ouvem o grito vindo de voce.");
            testKey = Config.Bind("Geral", "TeclaTeste", "<Keyboard>/f10", "Dispara um susto na hora (pra testar). Vazio = desliga.");
            types = Config.Bind("Geral", "Tipos", "rosto,investida,glitch,espreita",
                "Tipos de susto sorteados (separados por virgula): rosto, investida (vem correndo na sua cara), glitch (chiado de TV), espreita (observa do canto e pula).");
            suspenseEnabled = Config.Bind("Suspense", "Ativado", true,
                "Passos te seguindo, batidas na parede, porta rangendo, luzes piscando, risadinhas e sussurros (so voce ouve).");
            buildMinutes = Config.Bind("Suspense", "MinutosAteTensaoMaxima", 6f, new ConfigDescription(
                "Minutos sozinho e no escuro dentro do predio ate a tensao chegar no maximo.", new AcceptableValueRange<float>(1f, 30f)));
            maxEventsPerMinute = Config.Bind("Suspense", "EventosPorMinutoNoMaximo", 4f, new ConfigDescription(
                "Quantos eventos por minuto com a tensao no maximo (com tensao baixa e bem menos).", new AcceptableValueRange<float>(0f, 12f)));
            suspenseVolume = Config.Bind("Suspense", "Volume", .9f, new ConfigDescription("Volume dos sons de suspense.", new AcceptableValueRange<float>(0f, 1f)));
            debugLog = Config.Bind("Debug", "LogDetalhado", false, "Escreve no log cada evento de suspense e a tensao.");
            useDefaultFace = Config.Bind("Midia", "UsarRostoPadrao", true, "Usa os rostos gerados pelo mod (alem das suas imagens da pasta Sustos).");
            useDefaultScream = Config.Bind("Midia", "UsarGritoPadrao", true, "Usa o grito gerado pelo mod (alem dos seus .wav da pasta Sustos).");

            // Objeto próprio que sobrevive a troca de cena (não depende do objeto do BepInEx).
            var go = new GameObject("SustoNantaoRunner");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ScareRunner>();
            go.AddComponent<SuspenseDirector>();
            Logger.LogInfo("SustoNantao 1.1.0 carregado. Tecla de teste: " + testKey.Value);
        }

        internal static void Log(string message) { if (Instance != null) Instance.Logger.LogInfo(message); }
        internal static void Warn(string message) { if (Instance != null) Instance.Logger.LogWarning(message); }
    }

    /// <summary>Decide quando assustar, mostra o susto e sincroniza o grito com os amigos.</summary>
    internal sealed class ScareRunner : MonoBehaviour
    {
        private const string ScreamMessage = SustoPlugin.Guid + ".grito.v1";

        private sealed class FaceSet { public Texture2D Open, Closed, Negative; }
        private enum Mode { Rosto, Investida, Glitch, Espreita }

        private readonly List<FaceSet> faces = new List<FaceSet>();
        private readonly List<Texture2D> statics = new List<Texture2D>();
        private readonly List<AudioClip> screams = new List<AudioClip>();
        private bool mediaLoaded;

        private float lastScare = -9999f, eligibleTime, checkTimer;
        private InputAction testAction;
        private string testBinding;

        private NetworkManager network;

        // Tela do susto
        private GameObject canvasGo;
        private RawImage faceImage;
        private Image blackBack, flash;
        private AspectRatioFitter fitter;
        private AudioSource audioSource;
        private float scareT = -1f, scareDuration, flickerTimer, intro, sideX;
        private int currentFace;
        private Mode mode;
        private bool screamed;
        private AudioClip pendingScream;
        private Vector3 scareOrigin;

        private SustoPlugin P => SustoPlugin.Instance;

        // ------------------------------------------------------------------ ciclo

        private void Update()
        {
            if (P == null) return;
            UpdateNetwork();
            EnsureTestKey();

            float dt = Time.unscaledDeltaTime;
            if (scareT >= 0f) { AnimateScare(dt); return; }

            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (local == null) { eligibleTime = 0f; return; }

            if (testAction != null && testAction.WasPressedThisFrame() && !local.isTypingChat && !local.inTerminalMenu)
            {
                StartScare(local);
                return;
            }
            if (!P.scareEnabled.Value) return;

            bool can = ScareRules.CanScare(ReadState(local), P.onlyInside.Value);
            if (!can) { if (local.isInHangarShipRoom || !Landed()) eligibleTime = 0f; return; }
            eligibleTime += dt;

            checkTimer += dt;
            if (checkTimer < 1f) return; // sorteia 1x por segundo
            float interval = checkTimer;
            checkTimer = 0f;
            if (!ScareRules.Ready(Time.unscaledTime, lastScare, P.minInterval.Value, eligibleTime, P.firstDelay.Value)) return;
            // Tensão alta (suspense) deixa o susto bem mais provável.
            float chance = P.chancePerMinute.Value * SuspenseRules.ScareMultiplier(SuspenseDirector.Tension);
            if (ScareRules.Roll(UnityEngine.Random.value, chance, interval)) StartScare(local);
        }

        private static bool Landed() => StartOfRound.Instance != null && StartOfRound.Instance.shipHasLanded;

        private static PlayerState ReadState(PlayerControllerB p) => new PlayerState
        {
            Alive = !p.isPlayerDead,
            Controlled = p.isPlayerControlled,
            InTerminal = p.inTerminalMenu,
            Typing = p.isTypingChat,
            MenuOpen = p.quickMenuManager != null && p.quickMenuManager.isMenuOpen,
            InShip = p.isInHangarShipRoom,
            InsideFacility = p.isInsideFactory,
            Landed = Landed(),
        };

        private void EnsureTestKey()
        {
            string binding = P.testKey.Value ?? "";
            if (testAction != null && testBinding == binding) return;
            testAction?.Dispose();
            testAction = null;
            testBinding = binding;
            if (string.IsNullOrWhiteSpace(binding)) return;
            try { testAction = new InputAction("SustoTeste", InputActionType.Button, binding); testAction.Enable(); }
            catch (Exception e) { SustoPlugin.Warn("Tecla de teste invalida: " + e.Message); }
        }

        // ------------------------------------------------------------------ susto

        private void StartScare(PlayerControllerB local)
        {
            LoadMedia();
            if (faces.Count == 0 && screams.Count == 0) return;
            BuildCanvas();
            lastScare = Time.unscaledTime;
            mode = PickMode();
            intro = mode == Mode.Investida ? .38f : mode == Mode.Espreita ? .9f : 0f;
            scareT = 0f;
            scareDuration = P.duration.Value;
            flickerTimer = 0f;
            sideX = UnityEngine.Random.value < .5f ? -1f : 1f;
            currentFace = faces.Count > 0 ? UnityEngine.Random.Range(0, faces.Count) : -1;
            screamed = false;
            pendingScream = screams.Count > 0 ? screams[UnityEngine.Random.Range(0, screams.Count)] : null;
            scareOrigin = local.transform.position + Vector3.up * 1.5f;
            faceImage.color = Color.white;
            ShowFace(mode == Mode.Investida ? FaceFrame.Closed : FaceFrame.Open);
            canvasGo.SetActive(true);
            if (intro <= 0f) Scream();
            SuspenseDirector.Release();
            SustoPlugin.Log("Susto! (" + mode + ")");
        }

        private Mode PickMode()
        {
            var list = new List<Mode>();
            foreach (string part in (P.types.Value ?? "").Split(','))
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "rosto": list.Add(Mode.Rosto); break;
                    case "investida": list.Add(Mode.Investida); break;
                    case "glitch": list.Add(Mode.Glitch); break;
                    case "espreita": list.Add(Mode.Espreita); break;
                }
            }
            if (list.Count == 0) list.Add(Mode.Rosto);
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>O "BUM": grito, tremor de câmera e aviso pros amigos.</summary>
        private void Scream()
        {
            if (screamed) return;
            screamed = true;
            if (pendingScream != null) audioSource.PlayOneShot(pendingScream, P.volume.Value);
            try { if (HUDManager.Instance != null) HUDManager.Instance.ShakeCamera(ScreenShakeType.Big); } catch { }
            if (P.screamForAll.Value) SendScream(scareOrigin);
        }

        private enum FaceFrame { Open, Closed, Negative, Static }

        private void ShowFace(FaceFrame frame)
        {
            Texture2D tex = null;
            if (frame == FaceFrame.Static && statics.Count > 0) tex = statics[UnityEngine.Random.Range(0, statics.Count)];
            else if (currentFace >= 0)
            {
                FaceSet f = faces[currentFace];
                tex = frame == FaceFrame.Closed ? f.Closed : frame == FaceFrame.Negative && f.Negative != null ? f.Negative : f.Open;
            }
            faceImage.enabled = tex != null;
            if (tex == null) return;
            faceImage.texture = tex;
            fitter.aspectRatio = tex.width / (float)Mathf.Max(1, tex.height);
        }

        private void AnimateScare(float dt)
        {
            scareT += dt;
            float total = intro + Mathf.Max(.1f, scareDuration);
            if (scareT >= total)
            {
                scareT = -1f;
                canvasGo.SetActive(false);
                return;
            }
            RectTransform rt = faceImage.rectTransform;
            float t = scareT;
            Color tint = Color.white;
            float alpha = 1f, back = 1f, flashA = 0f;

            if (t < intro)
            {
                float k = t / intro;
                if (mode == Mode.Investida)
                {
                    // Vem do fundo escuro acelerando até a cara; boca abre no final.
                    float e = k * k * k;
                    rt.localScale = Vector3.one * Mathf.Lerp(.12f, 1.05f, e);
                    rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-4f, 4f), Mathf.Lerp(-60f, 0f, e));
                    rt.localRotation = Quaternion.identity;
                    ShowFace(k > .65f ? FaceFrame.Open : FaceFrame.Closed);
                    alpha = Mathf.Lerp(.25f, 1f, e);
                    if (k > .85f) Scream();
                }
                else // Espreita: meio apagada no canto, observando.
                {
                    rt.localScale = Vector3.one * .55f;
                    rt.anchoredPosition = new Vector2(sideX * (720f - 40f * k), -120f + 10f * Mathf.Sin(t * 3f));
                    rt.localRotation = Quaternion.Euler(0, 0, sideX * -8f);
                    ShowFace(FaceFrame.Open);
                    alpha = .16f + .14f * k;
                    back = .55f * k;
                }
            }
            else
            {
                Scream();
                float s = t - intro, d = Mathf.Max(.1f, scareDuration);
                float pop = 1.22f - .22f * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, s / .08f));
                float shake = 38f * (1f - s / d) + 6f;
                rt.localScale = Vector3.one * (pop + UnityEngine.Random.Range(-.02f, .02f));
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake));
                rt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-2.5f, 2.5f));

                flickerTimer -= dt;
                if (flickerTimer <= 0f)
                {
                    flickerTimer = UnityEngine.Random.Range(.03f, .07f);
                    if (mode == Mode.Glitch)
                    {
                        // Alterna chiado e rosto, com cortes laterais e cores trocadas.
                        float r = UnityEngine.Random.value;
                        ShowFace(r < .45f ? FaceFrame.Static : r < .6f ? FaceFrame.Negative : FaceFrame.Open);
                        rt.anchoredPosition += new Vector2(UnityEngine.Random.Range(-160f, 160f), 0f);
                        tint = UnityEngine.Random.value < .5f ? new Color(.5f, 1f, 1f) : new Color(1f, .4f, .4f);
                    }
                    else
                    {
                        ShowFace(UnityEngine.Random.value < .18f ? FaceFrame.Negative : FaceFrame.Open);
                        tint = UnityEngine.Random.value < .22f ? new Color(1f, .35f, .35f) : Color.white;
                    }
                    faceImage.color = tint;
                }
                alpha = Mathf.Clamp01((d - s) / .15f);
                back = alpha;
                flashA = Mathf.Clamp01(1f - s / .05f) * .9f;
            }
            var c = faceImage.color; c.a = alpha; faceImage.color = c;
            blackBack.color = new Color(0, 0, 0, back);
            flash.color = new Color(1, 1, 1, flashA);
        }

        // ------------------------------------------------------------------ mídia

        private void LoadMedia()
        {
            if (mediaLoaded) return;
            mediaLoaded = true;
            string folder = Path.Combine(Paths.ConfigPath, "Sustos");
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "LEIA-ME.txt"),
                        "Coloque aqui imagens (.png / .jpg) e sons (.wav) para os sustos.\r\n" +
                        "O mod sorteia entre eles (e os padroes, se ligados na config).\r\n" +
                        "Depois de adicionar, reinicie o jogo.\r\n");
                }
                foreach (string file in Directory.GetFiles(folder))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    try
                    {
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(file) };
                            if (ImageConversion.LoadImage(tex, File.ReadAllBytes(file))) faces.Add(new FaceSet { Open = tex, Closed = tex });
                        }
                        else if (ext == ".wav")
                        {
                            var samples = ScareRules.ParseWav(File.ReadAllBytes(file), out int ch, out int rate);
                            if (samples == null) { SustoPlugin.Warn("WAV nao suportado: " + file); continue; }
                            var clip = AudioClip.Create(Path.GetFileName(file), samples.Length / ch, ch, rate, false);
                            clip.SetData(samples, 0);
                            screams.Add(clip);
                        }
                    }
                    catch (Exception e) { SustoPlugin.Warn("Nao consegui ler " + file + ": " + e.Message); }
                }
            }
            catch (Exception e) { SustoPlugin.Warn("Pasta Sustos: " + e.Message); }

            if (P.useDefaultFace.Value || faces.Count == 0)
            {
                for (int s = 1; s <= 4; s++)
                {
                    int seed = s * 97;
                    faces.Add(new FaceSet
                    {
                        Open = MakeTexture(ScareArt.Generate(512, seed), "SustoRosto" + s),
                        Closed = MakeTexture(ScareArt.Generate(512, seed, false, .1f), "SustoRostoFechado" + s),
                        Negative = MakeTexture(ScareArt.Generate(512, seed, true), "SustoRostoNeg" + s),
                    });
                }
            }
            for (int i = 0; i < 4; i++) statics.Add(MakeTexture(ScareArt.Static(256, 1000 + i), "SustoChiado" + i));
            if (P.useDefaultScream.Value || screams.Count == 0)
            {
                const int rate = 44100;
                var samples = ScareSound.Scream(rate, 666);
                var clip = AudioClip.Create("SustoGrito", samples.Length, 1, rate, false);
                clip.SetData(samples, 0);
                screams.Add(clip);
            }
            SustoPlugin.Log("Sustos: " + faces.Count + " imagens, " + screams.Count + " sons. Pasta: " + folder);
        }

        private static Texture2D MakeTexture(byte[] rgba, string name)
        {
            int size = (int)Math.Sqrt(rgba.Length / 4);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            return tex;
        }

        private void BuildCanvas()
        {
            if (canvasGo != null) return;
            canvasGo = new GameObject("SustoNantaoTela");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // por cima de tudo
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            blackBack = FullScreen<Image>("Fundo");
            blackBack.color = Color.black;

            var faceGo = new GameObject("Rosto", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            faceGo.transform.SetParent(canvasGo.transform, false);
            faceImage = faceGo.GetComponent<RawImage>();
            faceImage.raycastTarget = false;
            fitter = faceGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // cobre a tela toda

            flash = FullScreen<Image>("Clarao");
            flash.color = Color.clear;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            canvasGo.SetActive(false);
        }

        private T FullScreen<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.GetComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        // ------------------------------------------------------------------ rede: amigos ouvem o grito

        private void UpdateNetwork()
        {
            var current = NetworkManager.Singleton;
            if (current == null || !current.IsListening || current.CustomMessagingManager == null)
            {
                network = null;
                return;
            }
            if (current == network) return;
            network = current;
            network.CustomMessagingManager.UnregisterNamedMessageHandler(ScreamMessage);
            network.CustomMessagingManager.RegisterNamedMessageHandler(ScreamMessage, ReceiveScream);
        }

        private void SendScream(Vector3 position)
        {
            if (network == null) return;
            try
            {
                if (network.IsServer) Relay(network.LocalClientId, position);
                else Send(NetworkManager.ServerClientId, position);
            }
            catch (Exception e) { SustoPlugin.Warn("Grito (rede): " + e.Message); }
        }

        private void Send(ulong target, Vector3 position)
        {
            using (var w = new FastBufferWriter(32, Allocator.Temp))
            {
                w.WriteValueSafe(position);
                network.CustomMessagingManager.SendNamedMessage(ScreamMessage, target, w, NetworkDelivery.Reliable);
            }
        }

        /// <summary>Host repassa pra todos menos quem gritou (e toca no próprio host se não foi ele).</summary>
        private void Relay(ulong from, Vector3 position)
        {
            if (from != network.LocalClientId) PlayDistant(position);
            foreach (ulong client in network.ConnectedClientsIds)
                if (client != from && client != network.LocalClientId) Send(client, position);
        }

        private void ReceiveScream(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out Vector3 position);
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || Mathf.Abs(position.x) > 100000f) return;
            if (network != null && network.IsServer) Relay(sender, position);
            else if (sender == NetworkManager.ServerClientId) PlayDistant(position);
        }

        private void PlayDistant(Vector3 position)
        {
            LoadMedia();
            if (screams.Count == 0) return;
            var go = new GameObject("SustoGritoDistante");
            go.transform.position = position;
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 3f;
            a.maxDistance = 40f;
            a.dopplerLevel = 0f;
            var clip = screams[UnityEngine.Random.Range(0, screams.Count)];
            a.PlayOneShot(clip, P.volume.Value * .8f);
            Destroy(go, clip.length + .2f);
        }

        private void OnDestroy()
        {
            testAction?.Dispose();
            if (network != null && network.CustomMessagingManager != null)
                network.CustomMessagingManager.UnregisterNamedMessageHandler(ScreamMessage);
        }
    }
}
