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
        private const string KnockState = Guid + ".knock.v1";
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
            current.CustomMessagingManager.RegisterNamedMessageHandler(KnockState, ReceiveKnock);
        }

        private void CleanupFlightNetwork()
        {
            if (flightNetwork != null && flightNetwork.CustomMessagingManager != null)
            {
                flightNetwork.CustomMessagingManager.UnregisterNamedMessageHandler(FlightRequest);
                flightNetwork.CustomMessagingManager.UnregisterNamedMessageHandler(FlightState);
                flightNetwork.CustomMessagingManager.UnregisterNamedMessageHandler(KnockState);
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
                    Vector3 position = new Vector3(message.x, message.y, message.z);
                    if (message.action == "land") { if (chinelo != null) chinelo.LandFlight(position); }
                    else { if (chinelo != null) chinelo.FinishFlight(); PlayExplosion(position); }
                }
            }
            catch (Exception exception) { Logger.LogWarning("Invalid chinelo state: " + exception.Message); }
        }

        /// <summary>
        /// O chinelo bateu (ou chegou no alcance). Só o host decide:
        /// sorteio de explosão; senão vira chinelada: quem foi atingido voa e desmaia, e o chinelo cai no chão.
        /// </summary>
        internal void Impact(ChineloBehaviour chinelo, Vector3 position, PlayerControllerB victim, string reason)
        {
            if (flightNetwork == null || !flightNetwork.IsServer || !chinelo.IsFlying) return;
            if (FlightRules.RollExplosion(UnityEngine.Random.value * 100.0, explodeChance.Value))
            {
                Explode(chinelo, position, reason + " (sorteio de " + explodeChance.Value + "%)");
                return;
            }

            FlightMessage message = chinelo.Flight;
            message.action = "land"; message.x = position.x; message.y = position.y; message.z = position.z;
            chinelo.LandFlight(position); BroadcastFlight(message);

            if (victim != null && !victim.isPlayerDead)
            {
                Vector3 push = new Vector3(message.dx, 0f, message.dz);
                if (push.sqrMagnitude < .01f) push = victim.transform.position - position;
                push.y = 0f; push.Normalize();
                Vector3 knock = push * knockForce.Value + Vector3.up * knockForce.Value * .35f;
                SendKnock(victim, knock, knockSeconds.Value, 0);
                Logger.LogInfo("Chinelada #" + message.shot + " em " + victim.playerUsername + " -> desmaio " + knockSeconds.Value + "s");
            }
            else Logger.LogInfo("Chinelo caiu #" + message.shot + " em " + position + "; reason=" + reason);
        }

        // ---------------------------------------------------------------- desmaio

        private void SendKnock(PlayerControllerB target, Vector3 knock, float seconds, int playerDamage)
        {
            KnockMessage message = new KnockMessage { target = target.actualClientId, kx = knock.x, ky = knock.y, kz = knock.z,
                seconds = seconds, damage = playerDamage };
            if (!FlightRules.ValidKnock(message)) return;
            ApplyKnock(message); // no próprio host
            string json = JsonUtility.ToJson(message);
            foreach (ulong client in flightNetwork.ConnectedClientsIds)
            {
                if (client == flightNetwork.LocalClientId) continue;
                using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
                {
                    writer.WriteValueSafe(json, false);
                    flightNetwork.CustomMessagingManager.SendNamedMessage(KnockState, client, writer, NetworkDelivery.ReliableSequenced);
                }
            }
        }

        private void ReceiveKnock(ulong sender, FastBufferReader reader)
        {
            if (flightNetwork == null || flightNetwork.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                string json; reader.ReadValueSafe(out json, false);
                if (json.Length > 1024) return;
                KnockMessage message = JsonUtility.FromJson<KnockMessage>(json);
                if (FlightRules.ValidKnock(message)) ApplyKnock(message);
            }
            catch (Exception exception) { Logger.LogWarning("Invalid chinelo knock: " + exception.Message); }
        }

        private void ApplyKnock(KnockMessage message)
        {
            PlayerControllerB player = FindPlayer(message.target);
            if (player != null)
                KnockoutController.Apply(player, new Vector3(message.kx, message.ky, message.kz), message.seconds, message.damage);
        }

        // ---------------------------------------------------------------- explosão (10%)

        internal void Explode(ChineloBehaviour chinelo, Vector3 position, string reason)
        {
            if (flightNetwork == null || !flightNetwork.IsServer || !chinelo.IsFlying) return;
            FlightMessage message = chinelo.Flight;
            message.action = "explode"; message.x = position.x; message.y = position.y; message.z = position.z;
            chinelo.FinishFlight(); BroadcastFlight(message); PlayExplosion(position);
            PlayerControllerB thrower = FindPlayer(message.player);
            HashSet<EnemyAI> hitEnemies = new HashSet<EnemyAI>();
            int enemyForce = Mathf.RoundToInt(damage.Value * explodeMultiplier.Value);
            if (enemyForce > 0)
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
                    if (!blocked) enemy.HitEnemyOnLocalClient(enemyForce, (enemy.transform.position - position).normalized, thrower, true, 1);
                }
            // Jogadores perto: arremessados e desmaiados (sem dano, a não ser que DanoEmJogadores > 0).
            if (StartOfRound.Instance != null)
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (player == null || !player.isPlayerControlled || player.isPlayerDead) continue;
                    Vector3 center = player.transform.position + Vector3.up;
                    float falloff = FlightRules.BlastFalloff(Vector3.Distance(center, position), blastRadius.Value);
                    if (falloff <= 0f) continue;
                    Vector3 away = center - position; away.y = 0f;
                    if (away.sqrMagnitude < .01f) away = UnityEngine.Random.insideUnitSphere; away.y = 0f;
                    Vector3 knock = (away.normalized * knockForce.Value * 1.5f + Vector3.up * knockForce.Value * .6f) * falloff;
                    SendKnock(player, knock, knockSeconds.Value + 1.5f, explodePlayerDamage.Value);
                }
            if (RoundManager.Instance != null) RoundManager.Instance.PlayAudibleNoise(position, 60f, 1f, 0, false, 684203);
            Logger.LogInfo("Chinelo exploded #" + message.shot + " at " + position + "; reason=" + reason);
            if (chinelo.Native.NetworkObject.IsSpawned) chinelo.Native.NetworkObject.Despawn(true);
        }

        private void PlayExplosion(Vector3 position)
        {
            // Visual + som grandes (só efeito: a função de mina do jogo também machucaria jogadores).
            BigExplosionFx.Spawn(position, explodeVisualScale.Value, explodeVolume.Value);
            PlayerControllerB local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (HUDManager.Instance != null && local != null)
            {
                float distance = Vector3.Distance(local.transform.position, position);
                if (distance < blastRadius.Value * 2.5f) HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
                else if (distance < 45f) HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
        }
    }
}
