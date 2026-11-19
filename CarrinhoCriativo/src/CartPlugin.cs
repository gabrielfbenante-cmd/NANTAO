using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace CarrinhoCriativo
{
    [BepInPlugin(Guid, "Carrinho de Supermercado", "1.2.0")]
    [BepInDependency("gabri.modocriativoteste", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class CartPlugin : BaseUnityPlugin
    {
        public const string Guid = "gabri.carrinhocriativo";
        private const string RequestMessage = Guid + ".request.v1";
        private const string StateMessage = Guid + ".state.v1";
        internal static CartPlugin Instance;
        internal static readonly HashSet<GrabbableObject> LoadedItems = new HashSet<GrabbableObject>();
        private readonly List<CargoRecord> cargo = new List<CargoRecord>();
        private readonly Dictionary<ulong, float> lastRequests = new Dictionary<ulong, float>();
        private readonly Dictionary<KeyCode, float> lastKeys = new Dictionary<KeyCode, float>();
        private NetworkManager network;
        private GameObject cart;
        private Rigidbody body;
        private Transform[] wheels;
        private AudioSource rolling;
        private AudioClip rattle;
        private CartSnapshot snapshot = new CartSnapshot();
        private ConfigEntry<bool> autoSpawn;
        private ConfigEntry<int> capacity;
        private ConfigEntry<float> speed;
        private ConfigEntry<float> sizeConfig;
        private ConfigEntry<float> slopeBoost;
        private ConfigEntry<float> rollFriction;
        private ConfigEntry<float> maxRollSpeed;
        private ConfigEntry<string> passengerKey;
        private float size = 1f;
        private Vector3 lastGroundNormal = Vector3.up;
        private bool wasGrounded;
        private const int MaxCapacity = 40;
        private PlayerControllerB localPlayer;
        private PlayerControllerB passengerPlayer;
        private bool passengerPreviousMoveFlag;
        private Vector3 passengerPreviousPosition;
        private bool autoDone;
        private bool pendingLoad;
        private float readyAt, nextBroadcast, nextNoise, lastDistanceCheck;
        private QuickMenuManager quickMenu;
        private string overlay = "";
        private Harmony harmony;
        private Vector3 motorVelocity;
        private Transform groundAnchor;
        private Vector3 groundLocalPoint, previousGroundPoint;
        private UnityEngine.SceneManagement.Scene floorScene;

        private void Awake()
        {
            Instance = this;
            autoSpawn = Config.Bind("Criativo", "CarrinhoAoEntrar", true, "Cria o carrinho ao entrar como anfitriao quando o modo criativo esta ligado. F5 chama o carrinho.");
            capacity = Config.Bind("Carrinho", "CapacidadeCesto", 20, new ConfigDescription("Quantidade maxima de objetos no cesto (empilham em camadas).", new AcceptableValueRange<int>(1, MaxCapacity)));
            speed = Config.Bind("Carrinho", "VelocidadeMaxima", 4f, new ConfigDescription("Velocidade maxima aproximada empurrando no plano, reduzida pela carga.", new AcceptableValueRange<float>(2f, 6f)));
            sizeConfig = Config.Bind("Carrinho", "Tamanho", 1.5f, new ConfigDescription("Tamanho do carrinho (1 = original). Vale o do anfitriao.", new AcceptableValueRange<float>(1f, 2.5f)));
            slopeBoost = Config.Bind("Descida", "Impulso", 1.6f, new ConfigDescription("Quanto o carrinho acelera em descidas (0 = desliga).", new AcceptableValueRange<float>(0f, 4f)));
            rollFriction = Config.Bind("Descida", "AtritoRodas", .35f, new ConfigDescription("Atrito quando ninguem empurra (menor = rola mais longe).", new AcceptableValueRange<float>(.05f, 2f)));
            passengerKey = Config.Bind("Teclas", "Passageiro", "C",
                "Tecla para subir/descer do carrinho como passageiro (nome da tecla: C, B, N...). O V ficou para o radio.");
            maxRollSpeed = Config.Bind("Descida", "VelocidadeMaximaRolando", 12f, new ConfigDescription("Velocidade maxima rolando sozinho ladeira abaixo (m/s).", new AcceptableValueRange<float>(3f, 25f)));
            harmony = new Harmony(Guid); harmony.PatchAll(typeof(CartPlugin).Assembly);
            Logger.LogInfo("Carrinho de Supermercado 1.2.0 loaded. F5: cart; E: push; R: load; T: unload; " + PassengerName + ": passenger. Size x" + sizeConfig.Value + ", capacity " + capacity.Value + ".");
        }

        private bool CreativeEnabled()
        {
            PluginInfo info;
            ConfigEntry<bool> setting;
            return !Chainloader.PluginInfos.TryGetValue("gabri.modocriativoteste", out info) ||
                !info.Instance.Config.TryGetEntry<bool>("Criativo", "Ativado", out setting) || setting.Value;
        }

        private static PlayerControllerB Local()
        { return GameNetworkManager.Instance == null ? null : GameNetworkManager.Instance.localPlayerController; }

        private PlayerControllerB Player(ulong client)
        {
            if (StartOfRound.Instance == null) return null;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                if (player != null && player.actualClientId == client && player.isPlayerControlled && !player.isPlayerDead) return player;
            return null;
        }

        private void Update()
        {
            NetworkManager current = NetworkManager.Singleton;
            if (current == null || !current.IsListening)
            {
                if (network != null || cart != null) Cleanup();
                localPlayer = null; autoDone = false; readyAt = Time.unscaledTime + 6f;
                return;
            }
            if (current != network)
            {
                Cleanup(); network = current;
                network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
                network.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
                Logger.LogInfo("Carrinho message handlers registered.");
            }
            PlayerControllerB player = Local();
            if (player != localPlayer) { localPlayer = player; readyAt = Time.unscaledTime + 6f; autoDone = false; }
            if (player == null || !player.isPlayerControlled) return;
            if (quickMenu == null) quickMenu = UnityEngine.Object.FindObjectOfType<QuickMenuManager>();
            if (network.IsServer)
            {
                if (!autoDone && autoSpawn.Value && CreativeEnabled() && Time.unscaledTime > readyAt)
                { autoDone = true; SpawnOrRecall(player); }
                if (snapshot.driver >= 0 && Player((ulong)snapshot.driver) == null) snapshot.driver = -1;
                if (snapshot.passenger >= 0 && Player((ulong)snapshot.passenger) == null) snapshot.passenger = -1;
                if (cart != null && Time.unscaledTime >= nextBroadcast)
                { nextBroadcast = Time.unscaledTime + .1f; Broadcast(); }
            }
            if (Application.isFocused && (quickMenu == null || !quickMenu.isMenuOpen))
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.f5Key.wasPressedThisFrame) HandleKey(KeyCode.F5);
                    if (keyboard.eKey.wasPressedThisFrame) HandleKey(KeyCode.E);
                    if (keyboard.rKey.wasPressedThisFrame) HandleKey(KeyCode.R);
                    if (keyboard.tKey.wasPressedThisFrame) HandleKey(KeyCode.T);
                    var pk = keyboard[PassengerInputKey];
                    if (pk != null && pk.wasPressedThisFrame) HandleKey(PassengerKeyCode);
                }
            }
            if (Time.unscaledTime >= lastDistanceCheck)
            {
                lastDistanceCheck = Time.unscaledTime + .4f;
                overlay = cart == null ? "F5: chamar carrinho" : "CARRINHO " + cargo.Count + "/" + capacity.Value +
                    " | E: empurrar/soltar | R: guardar | T: descarregar | " + PassengerName + ": passageiro | F5: chamar";
            }
        }

        private void HandleKey(KeyCode key)
        {
            float previous;
            if (lastKeys.TryGetValue(key, out previous) && Time.unscaledTime - previous < .3f) return;
            lastKeys[key] = Time.unscaledTime;
            if (localPlayer == null || localPlayer.isPlayerDead || network == null || localPlayer.isTypingChat || localPlayer.inTerminalMenu) return;
            if (key == KeyCode.F5)
            {
                if (!network.IsHost) { Tip("So o anfitriao pode chamar o carrinho de teste."); return; }
                if (!CreativeEnabled()) { Tip("Ligue o criativo com F7 para chamar o carrinho."); return; }
                SpawnOrRecall(localPlayer); return;
            }
            if (cart == null) return;
            if (key == KeyCode.E)
            {
                if (snapshot.driver != (long)localPlayer.actualClientId && !LookingAtCart()) return;
                Request("push", null);
            }
            else if (key == KeyCode.R) { if (!pendingLoad) StartCoroutine(DropThenLoad()); }
            else if (key == KeyCode.T) Request("unload", null);
            else if (key == PassengerKeyCode) Request("ride", null);
        }

        private IEnumerator DropThenLoad()
        {
            if (localPlayer == null || cart == null || Vector3.Distance(localPlayer.transform.position, cart.transform.position) > Reach)
            { Tip("Chegue perto do carrinho para guardar um item."); yield break; }
            GrabbableObject item = localPlayer.isHoldingObject ? localPlayer.currentlyHeldObjectServer : null;
            if (item == null) { Tip("Segure um item e pressione R para coloca-lo no cesto."); yield break; }
            if (localPlayer.isGrabbingObjectAnimation || localPlayer.inSpecialInteractAnimation) yield break;
            ulong id = item.NetworkObjectId;
            pendingLoad = true;
            try
            {
                localPlayer.DiscardHeldObject(true, null, cart.transform.position + Vector3.up * .65f * size, false);
                yield return new WaitForSeconds(.3f);
                if (item != null) Request("load", id.ToString());
            }
            finally { pendingLoad = false; }
        }

        private void Request(string action, string item)
        {
            if (network == null || localPlayer == null) return;
            CartRequest request = new CartRequest { action = action, itemId = item };
            if (network.IsServer) Execute(network.LocalClientId, request);
            else Send(RequestMessage, JsonUtility.ToJson(request), NetworkManager.ServerClientId, false);
        }

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsServer) return;
            try
            {
                string json; reader.ReadValueSafe(out json, false);
                if (json.Length > 1024) return;
                CartRequest request = JsonUtility.FromJson<CartRequest>(json);
                float previous;
                if (lastRequests.TryGetValue(sender, out previous) && Time.unscaledTime - previous < .15f) return;
                lastRequests[sender] = Time.unscaledTime;
                Execute(sender, request);
            }
            catch (Exception exception) { Logger.LogWarning("Ignored invalid cart request: " + exception.Message); }
        }

        private void Execute(ulong sender, CartRequest request)
        {
            if (request == null || request.version != 1 || cart == null) return;
            PlayerControllerB player = Player(sender);
            if (player == null || Vector3.Distance(player.transform.position, cart.transform.position) > Reach) return;
            if (request.action == "push")
            {
                if (snapshot.driver == (long)sender) snapshot.driver = -1;
                else if (CartRules.CanTakeRole((long)sender, snapshot.driver, snapshot.passenger)) snapshot.driver = (long)sender;
                else { Tip("O carrinho ja esta em uso. Saia do assento antes de empurrar."); return; }
                IgnoreRoleCollisions(); Logger.LogInfo("Cart driver=" + snapshot.driver);
            }
            else if (request.action == "ride")
            {
                if (snapshot.passenger == (long)sender) snapshot.passenger = -1;
                else if (CartRules.CanTakeRole((long)sender, snapshot.passenger, snapshot.driver)) snapshot.passenger = (long)sender;
                else { Tip("Assento ocupado ou voce esta empurrando. Aperte E para soltar primeiro."); return; }
                IgnoreRoleCollisions(); Logger.LogInfo("Cart passenger=" + snapshot.passenger);
            }
            else if (request.action == "load")
            {
                if (cargo.Count >= capacity.Value) { Tip("O cesto esta cheio."); return; }
                ulong id; NetworkObject obj;
                if (!ulong.TryParse(request.itemId, out id) || !network.SpawnManager.SpawnedObjects.TryGetValue(id, out obj)) return;
                GrabbableObject item = obj.GetComponent<GrabbableObject>();
                if (item == null || item.isHeld || item.isHeldByEnemy || item.isPocketed || LoadedItems.Contains(item) ||
                    Vector3.Distance(item.transform.position, cart.transform.position) > Reach) return;
                cargo.Add(new CargoRecord(item)); LoadedItems.Add(item);
                Logger.LogInfo("Cart loaded " + item.itemProperties.itemName + "; cargo=" + cargo.Count);
                Tip("Item guardado no carrinho (" + cargo.Count + "/" + capacity.Value + "). T descarrega.");
            }
            else if (request.action == "unload") UnloadAll();
            else return;
            Broadcast();
        }

        private void Send(string message, string json, ulong client, bool all)
        {
            if (network == null || network.CustomMessagingManager == null) return;
            using (FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp, 4096))
            {
                writer.WriteValueSafe(json, false);
                if (all) network.CustomMessagingManager.SendNamedMessageToAll(message, writer, NetworkDelivery.UnreliableSequenced);
                else network.CustomMessagingManager.SendNamedMessage(message, client, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void Broadcast()
        {
            snapshot.exists = cart != null;
            if (cart != null)
            {
                Vector3 p = cart.transform.position;
                snapshot.x = p.x; snapshot.y = p.y; snapshot.z = p.z; snapshot.yaw = cart.transform.eulerAngles.y;
                snapshot.speed = Mathf.Min(100, motorVelocity.magnitude);
                snapshot.size = size;
            }
            List<string> ids = new List<string>();
            for (int i = cargo.Count - 1; i >= 0; i--)
            {
                if (cargo[i].Item == null) { cargo.RemoveAt(i); continue; }
                ids.Insert(0, cargo[i].Item.NetworkObjectId.ToString());
            }
            snapshot.cargoIds = ids.ToArray();
            Send(StateMessage, JsonUtility.ToJson(snapshot), 0, true);
        }

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                string json; reader.ReadValueSafe(out json, false);
                if (json.Length > 4096) return;
                CartSnapshot state = JsonUtility.FromJson<CartSnapshot>(json);
                if (!CartRules.ValidSnapshot(state, MaxCapacity)) return;
                snapshot = state;
                if (!state.exists) { RemoveCart(); return; }
                if (cart == null) CreateCart(new Vector3(state.x, state.y, state.z), state.yaw, state.size);
                else if (Mathf.Abs(size - state.size) > .01f) { size = state.size; cart.transform.localScale = Vector3.one * size; }
                HashSet<GrabbableObject> wanted = new HashSet<GrabbableObject>();
                foreach (string text in state.cargoIds)
                {
                    ulong id; NetworkObject obj;
                    if (ulong.TryParse(text, out id) && network.SpawnManager.SpawnedObjects.TryGetValue(id, out obj))
                    {
                        GrabbableObject item = obj.GetComponent<GrabbableObject>();
                        if (item == null) continue;
                        wanted.Add(item);
                        if (!LoadedItems.Contains(item)) { cargo.Add(new CargoRecord(item)); LoadedItems.Add(item); }
                    }
                }
                for (int i = cargo.Count - 1; i >= 0; i--)
                    if (cargo[i].Item == null || !wanted.Contains(cargo[i].Item))
                    { CargoRecord old = cargo[i]; cargo.RemoveAt(i); LoadedItems.Remove(old.Item); old.Restore(cart.transform.position + cart.transform.right * .8f); }
                IgnoreRoleCollisions();
            }
            catch (Exception exception) { Logger.LogWarning("Ignored invalid cart state: " + exception.Message); }
        }

        private Material FindMaterial()
        {
            foreach (Item item in Resources.FindObjectsOfTypeAll<Item>())
                if (item.spawnPrefab != null)
                {
                    MeshRenderer renderer = item.spawnPrefab.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null) return renderer.sharedMaterial;
                }
            return null;
        }

        private static Vector3 Forward(PlayerControllerB player)
        {
            Vector3 direction = player.IsOwner && player.gameplayCamera != null ? player.gameplayCamera.transform.forward : player.transform.forward;
            direction.y = 0;
            return direction.sqrMagnitude > .001f ? direction.normalized : player.transform.forward;
        }

        private static float Yaw(PlayerControllerB player)
        { Vector3 forward = Forward(player); return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg; }

        private bool LookingAtCart()
        {
            if (cart == null || localPlayer == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(localPlayer.gameplayCamera.transform.position,
                localPlayer.gameplayCamera.transform.forward, Reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<PlayerControllerB>() != null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance; found = hit.collider.transform.IsChildOf(cart.transform);
            }
            return found;
        }

        private int PhysicsLayer(Vector3 position)
        {
            RaycastHit floor;
            int mask = StartOfRound.Instance != null ? StartOfRound.Instance.collidersAndRoomMaskAndDefault : ~0;
            if (!Physics.Raycast(position + Vector3.up * .3f, Vector3.down, out floor, 2f, mask, QueryTriggerInteraction.Ignore)) return 0;
            int floorLayer = floor.collider.gameObject.layer;
            floorScene = floor.collider.gameObject.scene;
            int selected = -1, best = -1;
            foreach (string name in new string[] { "PhysicsObject", "Props", "Default", "Colliders", "Room", "Player" })
            {
                int candidate = LayerMask.NameToLayer(name);
                if (candidate < 0 || Physics.GetIgnoreLayerCollision(candidate, floorLayer)) continue;
                int score = name == "Player" ? -2 : 0;
                foreach (string world in new string[] { "Room", "Colliders", "Default" })
                {
                    int layer = LayerMask.NameToLayer(world);
                    if (layer >= 0 && !Physics.GetIgnoreLayerCollision(candidate, layer)) score++;
                }
                if (score > best) { selected = candidate; best = score; }
            }
            if (selected < 0) selected = floorLayer;
            Logger.LogInfo("Cart collision layer=" + LayerMask.LayerToName(selected) + " (" + selected + "); floor=" + LayerMask.LayerToName(floorLayer) + " (" + floorLayer + "); scene=" + floorScene.name);
            return selected;
        }

        // Tecla de passageiro configurável (padrão C; o V é do rádio no modpack).
        private KeyCode PassengerKeyCode => Enum.TryParse(passengerKey.Value, true, out KeyCode k) && k != KeyCode.None ? k : KeyCode.C;
        private Key PassengerInputKey => Enum.TryParse(passengerKey.Value, true, out Key k) && k != Key.None ? k : Key.C;
        private string PassengerName => PassengerKeyCode.ToString();

        /// <summary>Alcance para interagir (cresce com o carrinho).</summary>
        private float Reach { get { return 2f + size; } }

        private void CreateCart(Vector3 position, float yaw, float scale = -1f)
        {
            int physicsLayer = PhysicsLayer(position);
            size = scale > 0f ? scale : Mathf.Clamp(sizeConfig.Value, 1f, 2.5f);
            cart = CartModel.Build(network.IsServer, FindMaterial(), out wheels);
            cart.transform.localScale = Vector3.one * size;
            cart.layer = physicsLayer;
            int visibleLayer = LayerMask.NameToLayer("Props");
            if (visibleLayer >= 0)
                foreach (Renderer renderer in cart.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = visibleLayer;
            if (floorScene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cart, floorScene);
            else if (StartOfRound.Instance != null)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cart, StartOfRound.Instance.gameObject.scene);
            cart.transform.position = position; cart.transform.rotation = Quaternion.Euler(0, yaw, 0);
            body = cart.GetComponent<Rigidbody>();
            motorVelocity = Vector3.zero; groundAnchor = null;
            rolling = cart.AddComponent<AudioSource>(); rolling.spatialBlend = 1f; rolling.minDistance = 1.5f; rolling.maxDistance = 18f;
            rolling.loop = true; rolling.volume = 0; rolling.clip = MakeRattle(); rolling.Play();
            snapshot.exists = true;
            snapshot.size = size;
            lastGroundNormal = Vector3.up; wasGrounded = false;
            Logger.LogInfo("Cart model created at " + position + " (size x" + size + "); server=" + network.IsServer);
        }

        private void SpawnOrRecall(PlayerControllerB player)
        {
            if (player == null || player.isPlayerDead || !player.isPlayerControlled) return;
            if (snapshot.passenger >= 0) { Tip("O carrinho tem passageiro. Saia antes de chama-lo."); return; }
            Vector3 position;
            if (!FindSpawn(player, out position)) { Tip("Nao ha espaco para o carrinho aqui. Procure um lugar mais aberto e pressione F5."); return; }
            if (cart != null)
            {
                snapshot.driver = -1; UnloadAll();
                cart.transform.position = position; cart.transform.rotation = Quaternion.Euler(0, Yaw(player), 0);
                motorVelocity = Vector3.zero; groundAnchor = null;
            }
            else CreateCart(position, Yaw(player));
            autoDone = true;
            Broadcast();
            Tip("Carrinho pronto! E empurra; R guarda o item da mao; T descarrega; " + PassengerName + " entra no assento.");
        }

        private bool FindSpawn(PlayerControllerB player, out Vector3 result)
        {
            Vector3 forward = Forward(player);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3[] directions = new Vector3[] { forward, right, -right, -forward };
            float s = cart != null ? size : Mathf.Clamp(sizeConfig.Value, 1f, 2.5f);
            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = player.transform.position + direction * (.75f + .9f * s);
                RaycastHit hit;
                int mask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
                if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out hit, 5f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < .7f || hit.point.y - player.transform.position.y > .6f) continue;
                candidate.y = hit.point.y + .025f;
                bool blocked = false;
                foreach (Collider collider in Physics.OverlapBox(candidate + Vector3.up * .63f * s, new Vector3(.34f, .49f, .48f) * s, Quaternion.Euler(0, Yaw(player), 0), mask, QueryTriggerInteraction.Ignore))
                {
                    if (collider.GetComponentInParent<PlayerControllerB>() != null || collider.GetComponentInParent<GrabbableObject>() != null) continue;
                    if (cart != null && collider.transform.IsChildOf(cart.transform)) continue;
                    blocked = true; break;
                }
                if (!blocked) { result = candidate; return true; }
            }
            result = Vector3.zero; return false;
        }

        private void FixedUpdate()
        {
            if (cart == null || network == null || !network.IsServer) return;
            float dt = Time.fixedDeltaTime;
            Vector3 position = body.position;
            if (groundAnchor != null)
            {
                Vector3 movedPoint = groundAnchor.TransformPoint(groundLocalPoint);
                position += movedPoint - previousGroundPoint;
            }
            if (snapshot.driver >= 0)
            {
                PlayerControllerB driver = Player((ulong)snapshot.driver);
                if (driver == null || Vector3.Distance(driver.transform.position, cart.transform.position) > 3f + size)
                { snapshot.driver = -1; IgnoreRoleCollisions(); }
                else
                {
                    Vector3 target = driver.transform.position + Forward(driver) * (.55f + .6f * size);
                    Vector3 delta = target - position; delta.y = 0;
                    float limit = CartRules.LoadedSpeed(speed.Value, cargo.Count / 2 + (snapshot.passenger >= 0 ? 3 : 0));
                    // Na descida dá pra correr mais com o carrinho; na subida ele pesa.
                    Vector3 downhill = SlopeDownhill();
                    if (delta.sqrMagnitude > .0001f && downhill.sqrMagnitude > .0001f)
                        limit = CartRules.SlopeSpeedLimit(limit, Vector3.Dot(delta.normalized, downhill) / 4f);
                    Vector3 wanted = Vector3.ClampMagnitude(delta * 5f, limit);
                    motorVelocity.x = Mathf.Lerp(motorVelocity.x, wanted.x, dt * 7f);
                    motorVelocity.z = Mathf.Lerp(motorVelocity.z, wanted.z, dt * 7f);
                    body.MoveRotation(Quaternion.Slerp(body.rotation, Quaternion.Euler(0, Yaw(driver), 0), dt * 4f));
                }
            }
            else
            {
                float friction = rollFriction.Value;
                motorVelocity.x /= 1f + friction * dt;
                motorVelocity.z /= 1f + friction * dt;
                // Solto na ladeira: ganha velocidade descendo (e volta se estiver numa subida).
                Vector3 downhill = SlopeDownhill();
                motorVelocity += downhill * slopeBoost.Value * dt;
                Vector3 flat = new Vector3(motorVelocity.x, 0f, motorVelocity.z);
                if (flat.magnitude > maxRollSpeed.Value)
                {
                    flat = flat.normalized * maxRollSpeed.Value;
                    motorVelocity.x = flat.x; motorVelocity.z = flat.z;
                }
            }
            motorVelocity.y += Physics.gravity.y * dt;
            Vector3 horizontal = new Vector3(motorVelocity.x, 0, motorVelocity.z) * dt;
            int mask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
            if (horizontal.sqrMagnitude > .000001f)
            {
                float allowed = horizontal.magnitude;
                Vector3 normal = Vector3.zero;
                foreach (RaycastHit hit in Physics.BoxCastAll(position + Vector3.up * .61f * size, new Vector3(.335f, .45f, .48f) * size,
                    horizontal.normalized, body.rotation, allowed + .025f, mask, QueryTriggerInteraction.Ignore))
                {
                    if (IgnoreWorldCollider(hit.collider) || hit.normal.y > .65f) continue;
                    float distance = Mathf.Max(0, hit.distance - .025f);
                    if (distance < allowed) { allowed = distance; normal = hit.normal; }
                }
                if (allowed < horizontal.magnitude)
                {
                    horizontal = horizontal.normalized * allowed;
                    motorVelocity = Vector3.ProjectOnPlane(motorVelocity, normal);
                }
            }
            Vector3 next = position + horizontal + Vector3.up * motorVelocity.y * dt;
            RaycastHit floor;
            if (FindGround(next, out floor) && motorVelocity.y <= 0 && next.y <= floor.point.y + .005f && position.y >= floor.point.y - .35f)
            {
                next.y = floor.point.y + .005f;
                motorVelocity.y = 0;
                lastGroundNormal = floor.normal; wasGrounded = true;
                groundAnchor = floor.collider.transform;
                groundLocalPoint = groundAnchor.InverseTransformPoint(next);
                previousGroundPoint = next;
            }
            else { groundAnchor = null; wasGrounded = false; }
            body.MovePosition(next);
            body.mass = 18f + cargo.Count * 1.2f + (snapshot.passenger >= 0 ? 25f : 0f);
            if (body.position.y < -150f)
            { Logger.LogWarning("Cart left the world bounds. F5 can create a new cart."); snapshot.driver = -1; snapshot.passenger = -1; RemoveCart(); }
        }

        /// <summary>Aceleração ladeira abaixo no chão atual (zero no plano ou no ar).</summary>
        private Vector3 SlopeDownhill()
        {
            if (!wasGrounded) return Vector3.zero;
            float ax, az;
            CartRules.SlopeAcceleration(lastGroundNormal.x, lastGroundNormal.y, lastGroundNormal.z, Physics.gravity.y, 3f, out ax, out az);
            return new Vector3(ax, 0f, az);
        }

        private bool IgnoreWorldCollider(Collider collider)
        {
            return collider == null || collider.transform.IsChildOf(cart.transform) ||
                collider.GetComponentInParent<PlayerControllerB>() != null || collider.GetComponentInParent<GrabbableObject>() != null;
        }

        private bool FindGround(Vector3 point, out RaycastHit result)
        {
            result = new RaycastHit();
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * .65f * size, Vector3.down, .75f + .65f * size,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                if (IgnoreWorldCollider(hit.collider) || hit.normal.y < .65f || hit.point.y > point.y + .25f) continue;
                if (hit.distance < nearest) { nearest = hit.distance; result = hit; }
            }
            return nearest < float.PositiveInfinity;
        }

        private void LateUpdate()
        {
            if (cart == null || network == null) { RestorePassenger(false); return; }
            if (!network.IsServer)
            {
                body.position = Vector3.Lerp(body.position, new Vector3(snapshot.x, snapshot.y, snapshot.z), Time.deltaTime * 14f);
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.Euler(0, snapshot.yaw, 0), Time.deltaTime * 14f);
            }
            for (int i = 0; i < cargo.Count; i++) cargo[i].Place(cart.transform, i);
            PlayerControllerB player = Local();
            if (player != null && snapshot.passenger == (long)player.actualClientId && !player.isPlayerDead)
            {
                if (passengerPlayer != player)
                {
                    RestorePassenger(false); passengerPlayer = player;
                    passengerPreviousMoveFlag = player.disableMoveInput; player.disableMoveInput = true;
                    passengerPreviousPosition = player.transform.position;
                    Logger.LogInfo("Local player entered cart passenger seat.");
                }
                Vector3 seat = cart.transform.TransformPoint(new Vector3(0, .55f, -.10f));
                player.fallValue = 0; player.fallValueUncapped = 0;
                player.isFallingNoJump = false; player.isFallingFromJump = false;
                player.transform.position = seat;
                player.serverPlayerPosition = seat;
            }
            else RestorePassenger(true);
            float moving = network.IsServer ? motorVelocity.magnitude : snapshot.speed;
            if (wheels != null)
                foreach (Transform wheel in wheels) wheel.Rotate(Vector3.up, moving / (.095f * size) * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
            if (rolling != null) { rolling.volume = Mathf.Clamp01(moving / 4f) * .22f; rolling.pitch = .75f + Mathf.Min(moving, 6f) * .10f; }
            if (network.IsServer && moving > .5f && Time.time > nextNoise && RoundManager.Instance != null)
            { nextNoise = Time.time + 1.5f; RoundManager.Instance.PlayAudibleNoise(cart.transform.position, 10f, .6f, 0, false, 684202); }
        }

        private void IgnoreRoleCollisions()
        {
            if (cart == null || StartOfRound.Instance == null) return;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == null || player.thisController == null) continue;
                bool ignore = snapshot.driver == (long)player.actualClientId || snapshot.passenger == (long)player.actualClientId;
                foreach (Collider collider in cart.GetComponents<Collider>()) Physics.IgnoreCollision(collider, player.thisController, ignore);
            }
        }

        private void RestorePassenger(bool moveOutside)
        {
            if (passengerPlayer == null) return;
            passengerPlayer.disableMoveInput = passengerPreviousMoveFlag;
            if (moveOutside && !passengerPlayer.isPlayerDead && cart != null)
            {
                Vector3 position = SafeExit();
                passengerPlayer.TeleportPlayer(position, false, 0, false, true);
            }
            Logger.LogInfo("Local player left cart passenger seat.");
            passengerPlayer = null;
        }

        private Vector3 SafeExit()
        {
            int mask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
            foreach (Vector3 offset in new Vector3[] { Vector3.right * .95f, Vector3.left * .95f, Vector3.back * 1.15f, Vector3.forward * 1.15f,
                new Vector3(1.1f,0,-1.1f), new Vector3(-1.1f,0,-1.1f), new Vector3(1.1f,0,1.1f), new Vector3(-1.1f,0,1.1f) })
            {
                Vector3 point = cart.transform.TransformPoint(offset);
                foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 1.8f, Vector3.down, 3f, mask, QueryTriggerInteraction.Ignore))
                {
                    if (IgnoreWorldCollider(hit.collider) || hit.normal.y < .65f) continue;
                    Vector3 feet = new Vector3(point.x, hit.point.y + .05f, point.z);
                    bool blocked = false;
                    foreach (Collider wall in Physics.OverlapCapsule(feet + Vector3.up * .3f, feet + Vector3.up * 1.65f, .23f, mask, QueryTriggerInteraction.Ignore))
                        if (!IgnoreWorldCollider(wall)) { blocked = true; break; }
                    if (!blocked) return feet;
                }
            }
            return passengerPreviousPosition;
        }

        private void UnloadAll()
        {
            if (cart == null) return;
            for (int i = 0; i < cargo.Count; i++)
            {
                CargoRecord item = cargo[i]; LoadedItems.Remove(item.Item);
                item.Restore(cart.transform.TransformPoint(new Vector3((i % 3 - 1) * .33f, .2f, -.95f - i / 3 * .3f)));
            }
            int count = cargo.Count; cargo.Clear();
            if (count > 0) { Logger.LogInfo("Cart unloaded " + count + " items."); Tip("Carrinho descarregado: " + count + " item(ns)."); }
        }

        private void RemoveCart()
        {
            RestorePassenger(true); UnloadAll();
            if (cart != null) UnityEngine.Object.Destroy(cart);
            cart = null; body = null; wheels = null; rolling = null;
            snapshot = new CartSnapshot();
            motorVelocity = Vector3.zero; groundAnchor = null;
            if (network != null && network.IsServer && network.IsListening)
            {
                try { Send(StateMessage, JsonUtility.ToJson(snapshot), 0, true); }
                catch (Exception exception) { Logger.LogDebug("Cart teardown broadcast unavailable: " + exception.Message); }
            }
        }

        private void Cleanup()
        {
            RemoveCart();
            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
            }
            network = null; LoadedItems.Clear(); lastRequests.Clear();
        }

        private AudioClip MakeRattle()
        {
            if (rattle != null) return rattle;
            const int rate = 22050; float[] samples = new float[rate * 2]; System.Random random = new System.Random(202);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float bumps = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 43f)), 16f);
                samples[i] = (float)(random.NextDouble() * 2 - 1) * (.06f + bumps * .15f) + Mathf.Sin(t * 370f) * .015f;
            }
            rattle = AudioClip.Create("RodasDoCarrinho", samples.Length, 1, rate, false); rattle.SetData(samples, 0);
            return rattle;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                KeyCode key = Event.current.keyCode;
                if (key == KeyCode.F5 || key == KeyCode.E || key == KeyCode.R || key == KeyCode.T || key == PassengerKeyCode)
                {
                    bool near = cart != null && localPlayer != null && Vector3.Distance(localPlayer.transform.position, cart.transform.position) <= Reach;
                    bool cartUse = key != KeyCode.E || (localPlayer != null && snapshot.driver == (long)localPlayer.actualClientId) || LookingAtCart();
                    if (key == KeyCode.F5 || (near && cartUse)) { HandleKey(key); Event.current.Use(); }
                }
            }
            if (localPlayer == null || network == null) return;
            if (cart != null && Vector3.Distance(localPlayer.transform.position, cart.transform.position) < Reach + 1f)
                GUI.Box(new Rect(12, Screen.height - 42, Mathf.Min(Screen.width - 24, 830), 28), overlay);
            if (quickMenu == null || !quickMenu.isMenuOpen || !network.IsHost) return;
            GUI.Box(new Rect(12, 166, 245, 226), "CARRINHO - TESTE");
            if (GUI.Button(new Rect(22, 196, 225, 26), "Chamar carrinho [F5]")) { quickMenu.CloseQuickMenu(); HandleKey(KeyCode.F5); }
            if (GUI.Button(new Rect(22, 228, 225, 26), "Guardar item da mao [R]")) { quickMenu.CloseQuickMenu(); HandleKey(KeyCode.R); }
            if (GUI.Button(new Rect(22, 260, 225, 26), "Descarregar cesto [T]")) { quickMenu.CloseQuickMenu(); HandleKey(KeyCode.T); }
            if (GUI.Button(new Rect(22, 292, 225, 26), "Dar um empurrao"))
            {
                quickMenu.CloseQuickMenu();
                if (body != null && snapshot.driver < 0 && snapshot.passenger < 0)
                { motorVelocity += cart.transform.forward * 2.2f; Logger.LogInfo("Creative cart push impulse applied."); }
            }
            if (GUI.Button(new Rect(22, 324, 225, 26), "Empurrar / soltar [E]")) { quickMenu.CloseQuickMenu(); Request("push", null); }
            if (GUI.Button(new Rect(22, 356, 225, 26), "Entrar / sair [" + PassengerName + "]")) { quickMenu.CloseQuickMenu(); Request("ride", null); }
        }

        internal static void Tip(string text)
        { if (HUDManager.Instance != null) HUDManager.Instance.DisplayTip("Carrinho", text, false, false, ""); }
        private void OnApplicationQuit() { Cleanup(); }
        private void OnDestroy() { Cleanup(); if (harmony != null) harmony.UnpatchSelf(); Instance = null; }
    }

    [HarmonyPatch(typeof(GrabbableObject), "Update")]
    internal static class CargoUpdatePatch
    { private static bool Prefix(GrabbableObject __instance) { return !CartPlugin.LoadedItems.Contains(__instance); } }
    [HarmonyPatch(typeof(GrabbableObject), "LateUpdate")]
    internal static class CargoLateUpdatePatch
    { private static bool Prefix(GrabbableObject __instance) { return !CartPlugin.LoadedItems.Contains(__instance); } }
}
