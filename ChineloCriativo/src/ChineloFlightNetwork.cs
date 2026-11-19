using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ChineloArremessavel
{
    public sealed partial class ChineloPlugin
    {
        private const string FlightRequest = Guid + ".flight.request.v1";
        private const string FlightState = Guid + ".flight.state.v1";
        private NetworkManager flightNetwork;
        private long nextShot;
        private readonly Dictionary<ulong, float> lastThrow = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> pendingItems = new HashSet<ulong>();
        private readonly HashSet<long> finishedShots = new HashSet<long>();

        private void UpdateFlightNetwork()
        {
            NetworkManager current = NetworkManager.Singleton;
            if (current == null || !current.IsListening) { if (flightNetwork != null) CleanupFlightNetwork(); return; }
            if (current == flightNetwork) return;
            CleanupFlightNetwork(); flightNetwork = current;
            current.CustomMessagingManager.RegisterNamedMessageHandler(FlightRequest, ReceiveFlightRequest);
            current.CustomMessagingManager.RegisterNamedMessageHandler(FlightState, ReceiveFlightState);
        }

        private void CleanupFlightNetwork()
        {
            if (flightNetwork != null && flightNetwork.CustomMessagingManager != null)
            {
                flightNetwork.CustomMessagingManager.UnregisterNamedMessageHandler(FlightRequest);
                flightNetwork.CustomMessagingManager.UnregisterNamedMessageHandler(FlightState);
            }
            StopAllCoroutines(); flightNetwork = null;
            lastThrow.Clear(); pendingItems.Clear(); finishedShots.Clear(); nextShot = 0;
            foreach (ChineloBehaviour chinelo in Instances.ToArray())
                if (chinelo != null) chinelo.CancelFlight();
        }

        private ChineloBehaviour FindChinelo(string text)
        {
            ulong id; NetworkObject obj;
            if (flightNetwork == null || !ulong.TryParse(text, out id) ||
                !flightNetwork.SpawnManager.SpawnedObjects.TryGetValue(id, out obj)) return null;
            return obj.GetComponent<ChineloBehaviour>();
        }

        private PlayerControllerB FindPlayer(ulong id)
        {
            if (StartOfRound.Instance == null) return null;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                if (player != null && player.actualClientId == id && player.isPlayerControlled && !player.isPlayerDead) return player;
            return null;
        }

        private void RequestFlight(ChineloBehaviour chinelo, PlayerControllerB player)
        {
            UpdateFlightNetwork();
            if (flightNetwork == null || player.gameplayCamera == null) return;
            Vector3 start = player.gameplayCamera.transform.position;
            Vector3 direction = player.gameplayCamera.transform.forward.normalized;
            FlightMessage request = new FlightMessage { action = "request", item = chinelo.Native.NetworkObjectId.ToString(),
                x = start.x, y = start.y, z = start.z, dx = direction.x, dy = direction.y, dz = direction.z };
            chinelo.SetPending();
            if (flightNetwork.IsServer) AcceptFlight(flightNetwork.LocalClientId, request);
            else SendFlight(FlightRequest, request, NetworkManager.ServerClientId);
            // Native release clears the inventory; custom flight starts afterwards.
            player.DiscardHeldObject(true, null, player.transform.position + Vector3.up * .1f, false);
        }

        private void SendFlight(string name, FlightMessage message, ulong recipient)
        {
            string json = JsonUtility.ToJson(message);
            using (FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(json, false);
                flightNetwork.CustomMessagingManager.SendNamedMessage(name, recipient, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void BroadcastFlight(FlightMessage message)
        {
            if (flightNetwork == null) return;
            foreach (ulong client in flightNetwork.ConnectedClientsIds)
                if (client != flightNetwork.LocalClientId) SendFlight(FlightState, message, client);
        }

        private void ReceiveFlightRequest(ulong sender, FastBufferReader reader)
        {
            if (flightNetwork == null || !flightNetwork.IsServer) return;
            try
            {
                string json; reader.ReadValueSafe(out json, false);
                if (json.Length <= 1024) AcceptFlight(sender, JsonUtility.FromJson<FlightMessage>(json));
            }
            catch (Exception exception) { Logger.LogWarning("Invalid chinelo request: " + exception.Message); }
        }

        private void AcceptFlight(ulong sender, FlightMessage request)
        {
            if (!FlightRules.Valid(request, true) || request.action != "request") return;
            PlayerControllerB player = FindPlayer(sender);
            ChineloBehaviour chinelo = FindChinelo(request.item);
            if (player == null || chinelo == null || chinelo.Native == null || chinelo.IsFlying ||
                chinelo.Native.isPocketed || !chinelo.Native.grabbable) return;
            if (chinelo.Native.playerHeldBy != player ||
                (player.currentlyHeldObjectServer != chinelo.Native && !chinelo.Native.isHeld)) return;
            Vector3 start = new Vector3(request.x, request.y, request.z);
            if (Vector3.Distance(start, player.gameplayCamera.transform.position) > 2f) return;
            float last;
            if (lastThrow.TryGetValue(sender, out last) && Time.unscaledTime - last < .6f) return;
            if (!pendingItems.Add(chinelo.Native.NetworkObjectId)) return;
            lastThrow[sender] = Time.unscaledTime;
            request.player = sender;
            StartCoroutine(LaunchAfterRelease(chinelo, request));
        }

        private IEnumerator LaunchAfterRelease(ChineloBehaviour chinelo, FlightMessage request)
        {
            ulong id = chinelo.Native.NetworkObjectId;
            try
            {
                float deadline = Time.unscaledTime + 1f;
                yield return null;
                while (chinelo != null && chinelo.Native != null && chinelo.Native.isHeld && Time.unscaledTime < deadline)
                    yield return null;
                if (flightNetwork == null || !flightNetwork.IsServer || chinelo == null || chinelo.Native == null ||
                    chinelo.Native.isHeld || chinelo.Native.isPocketed || FindPlayer(request.player) == null) yield break;
                request.action = "launch"; request.shot = ++nextShot;
                request.range = range.Value; request.speed = flightSpeed.Value;
                chinelo.BeginFlight(request); BroadcastFlight(request);
                Logger.LogInfo("Chinelo straight launch #" + request.shot + " from (" + request.x + ", " + request.y + ", " + request.z +
                    ") direction (" + request.dx + ", " + request.dy + ", " + request.dz + "), range=" + request.range + ", speed=" + request.speed);
            }
            finally { pendingItems.Remove(id); if (chinelo != null) chinelo.ClearPending(); }
        }

        private void ReceiveFlightState(ulong sender, FastBufferReader reader)
        {
            if (flightNetwork == null || flightNetwork.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                string json; reader.ReadValueSafe(out json, false);
                if (json.Length > 1024) return;
                FlightMessage message = JsonUtility.FromJson<FlightMessage>(json);
                if (message == null || finishedShots.Contains(message.shot)) return;
                if (message.action == "launch" && FlightRules.Valid(message, true))
                {
                    ChineloBehaviour chinelo = FindChinelo(message.item);
                    if (chinelo != null) chinelo.BeginFlight(message);
                }
                else if (FlightRules.Valid(message, false))
                {
                    finishedShots.Add(message.shot);
                    ChineloBehaviour chinelo = FindChinelo(message.item);
                    if (chinelo != null) chinelo.FinishFlight();
                    PlayExplosion(new Vector3(message.x, message.y, message.z));
                }
            }
            catch (Exception exception) { Logger.LogWarning("Invalid chinelo state: " + exception.Message); }
        }

        internal void Explode(ChineloBehaviour chinelo, Vector3 position, string reason)
        {
            if (flightNetwork == null || !flightNetwork.IsServer || !chinelo.IsFlying) return;
            FlightMessage message = chinelo.Flight;
            message.action = "explode"; message.x = position.x; message.y = position.y; message.z = position.z;
            chinelo.FinishFlight(); BroadcastFlight(message); PlayExplosion(position);
            PlayerControllerB thrower = FindPlayer(message.player);
            HashSet<EnemyAI> hitEnemies = new HashSet<EnemyAI>();
            if (damage.Value > 0)
                foreach (Collider collider in Physics.OverlapSphere(position, blastRadius.Value, ~0, QueryTriggerInteraction.Collide))
                {
                    EnemyAI enemy = collider.GetComponentInParent<EnemyAI>();
                    if (enemy == null || enemy.isEnemyDead || !hitEnemies.Add(enemy)) continue;
                    Vector3 target = collider.ClosestPoint(position);
                    bool blocked = false;
                    foreach (RaycastHit hit in Physics.RaycastAll(position, (target - position).normalized,
                        Vector3.Distance(position, target), StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.IsChildOf(chinelo.transform) || hit.collider.GetComponentInParent<EnemyAI>() != null ||
                            hit.collider.GetComponentInParent<PlayerControllerB>() != null || hit.collider.GetComponentInParent<GrabbableObject>() != null) continue;
                        blocked = true; break;
                    }
                    if (!blocked) enemy.HitEnemyOnLocalClient(damage.Value, (enemy.transform.position - position).normalized, thrower, true, 1);
                }
            if (RoundManager.Instance != null) RoundManager.Instance.PlayAudibleNoise(position, 25f, 1f, 0, false, 684203);
            Logger.LogInfo("Chinelo exploded #" + message.shot + " at " + position + "; reason=" + reason);
            if (chinelo.Native.NetworkObject.IsSpawned) chinelo.Native.NetworkObject.Despawn(true);
        }

        private void PlayExplosion(Vector3 position)
        {
            // Instantiate the visual only: the native mine helper also hurts players.
            if (StartOfRound.Instance != null && StartOfRound.Instance.explosionPrefab != null)
            {
                GameObject effect = UnityEngine.Object.Instantiate(StartOfRound.Instance.explosionPrefab, position, Quaternion.Euler(-90f, 0, 0));
                effect.SetActive(true); UnityEngine.Object.Destroy(effect, 5f);
            }
            ChineloBehaviour.PlayBoom(position);
            if (HUDManager.Instance != null && GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null &&
                Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, position) < 14f)
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
        }
    }
}
