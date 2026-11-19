using System;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;

namespace NeonRun
{
    /// <summary>
    /// Sincroniza "fulano ligou/desligou a corrida" entre todos da lobby usando mensagens
    /// nomeadas do Netcode (não precisa de NetworkObject novo nem de patcher).
    /// Fluxo: cliente -> host -> todos. Todo mundo da lobby precisa ter o mod.
    /// </summary>
    internal static class NeonNetwork
    {
        private const string ToServer = "NeonRun.ToServer";
        private const string ToClients = "NeonRun.ToClients";

        private static bool _registered;

        public static void Register()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            var cmm = nm.CustomMessagingManager;
            cmm.UnregisterNamedMessageHandler(ToServer);
            cmm.UnregisterNamedMessageHandler(ToClients);
            cmm.RegisterNamedMessageHandler(ToServer, OnServerReceive);
            cmm.RegisterNamedMessageHandler(ToClients, OnClientReceive);
            _registered = true;
            if (NeonConfig.DebugLog.Value) Plugin.Log.LogInfo("Rede registrada.");
        }

        public static void Unregister()
        {
            _registered = false;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(ToServer);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(ToClients);
        }

        /// <summary>O jogador local avisa que ligou/desligou.</summary>
        public static void SendState(int playerIndex, bool active)
        {
            var nm = NetworkManager.Singleton;
            if (!_registered || nm == null || !nm.IsListening) return;
            try
            {
                if (nm.IsServer)
                {
                    Broadcast(playerIndex, active);
                    return;
                }
                using (var w = new FastBufferWriter(8, Allocator.Temp))
                {
                    w.WriteValueSafe(playerIndex);
                    w.WriteValueSafe(active);
                    nm.CustomMessagingManager.SendNamedMessage(ToServer, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Falha ao enviar estado: " + e.Message);
            }
        }

        private static void OnServerReceive(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int idx);
            reader.ReadValueSafe(out bool active);

            var player = GetPlayer(idx);
            // Só aceita se quem mandou é dono desse jogador.
            if (player == null || player.actualClientId != sender) return;
            Broadcast(idx, active);
        }

        private static void Broadcast(int idx, bool active)
        {
            var nm = NetworkManager.Singleton;
            Apply(idx, active); // aplica no próprio host
            using (var w = new FastBufferWriter(8, Allocator.Temp))
            {
                w.WriteValueSafe(idx);
                w.WriteValueSafe(active);
                nm.CustomMessagingManager.SendNamedMessageToAll(ToClients, w, NetworkDelivery.Reliable);
            }
        }

        private static void OnClientReceive(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int idx);
            reader.ReadValueSafe(out bool active);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return; // host já aplicou
            Apply(idx, active);
        }

        private static void Apply(int idx, bool active)
        {
            var runner = NeonRunner.For(GetPlayer(idx));
            if (runner != null) runner.SetNetworkState(active);
        }

        private static PlayerControllerB GetPlayer(int idx)
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null || idx < 0 || idx >= sor.allPlayerScripts.Length) return null;
            return sor.allPlayerScripts[idx];
        }
    }
}
