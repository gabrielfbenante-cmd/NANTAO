using System;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Rede via mensagens nomeadas do Netcode (não precisa de NetworkObject novo).
    /// - Escolha de personagem: cliente pede → host confere (1 por jogador) → host manda a lista pra todos.
    /// - Habilidades: dono manda evento → host confere → host repassa pra todos.
    /// Todo mundo na lobby precisa ter o mod.
    /// </summary>
    internal static class AgentNetwork
    {
        private const string MsgPick = "Nantao.Pick";
        private const string MsgRoster = "Nantao.Roster";
        private const string MsgAbilityToServer = "Nantao.AbilityToServer";
        private const string MsgAbilityToClients = "Nantao.AbilityToClients";

        private static bool _registered;
        private static int[] _roster = new int[0];
        private static float _nextHostTick;

        private static NetworkManager NM => NetworkManager.Singleton;
        private static bool IsServer => NM != null && NM.IsServer;

        // ---------------------------------------------------------------- ciclo de vida

        public static void Register()
        {
            if (NM == null || NM.CustomMessagingManager == null) return;
            var cmm = NM.CustomMessagingManager;
            foreach (var m in new[] { MsgPick, MsgRoster, MsgAbilityToServer, MsgAbilityToClients })
                cmm.UnregisterNamedMessageHandler(m);
            cmm.RegisterNamedMessageHandler(MsgPick, OnPick);
            cmm.RegisterNamedMessageHandler(MsgRoster, OnRoster);
            cmm.RegisterNamedMessageHandler(MsgAbilityToServer, OnAbilityToServer);
            cmm.RegisterNamedMessageHandler(MsgAbilityToClients, OnAbilityToClients);
            _registered = true;
            _roster = NewRoster();
            if (ModConfig.DebugLog.Value) Plugin.Log.LogInfo("Rede registrada.");
        }

        public static void Unregister()
        {
            _registered = false;
            _roster = new int[0];
            if (NM == null || NM.CustomMessagingManager == null) return;
            foreach (var m in new[] { MsgPick, MsgRoster, MsgAbilityToServer, MsgAbilityToClients })
                NM.CustomMessagingManager.UnregisterNamedMessageHandler(m);
        }

        private static int[] NewRoster()
        {
            var sor = StartOfRound.Instance;
            int n = sor != null && sor.allPlayerScripts != null ? sor.allPlayerScripts.Length : 4;
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = Characters.None;
            return r;
        }

        /// <summary>Chamado todo frame (StartOfRound.Update). Host limpa vagas de quem saiu e reenvia a lista.</summary>
        public static void Tick()
        {
            if (!_registered || !IsServer || Time.time < _nextHostTick) return;
            _nextHostTick = Time.time + 2f;
            var sor = StartOfRound.Instance;
            if (sor == null) return;
            EnsureRosterSize();
            for (int i = 0; i < _roster.Length; i++)
            {
                if (_roster[i] == Characters.None) continue;
                var p = sor.allPlayerScripts[i];
                bool present = p != null && (p.isPlayerControlled || p.isPlayerDead);
                if (!present) _roster[i] = Characters.None; // saiu da lobby
            }
            BroadcastRoster(); // também serve pra quem acabou de entrar
        }

        private static void EnsureRosterSize()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null || _roster.Length == sor.allPlayerScripts.Length) return;
            var old = _roster;
            _roster = NewRoster();
            Array.Copy(old, _roster, Math.Min(old.Length, _roster.Length));
        }

        // ---------------------------------------------------------------- personagens

        /// <summary>Índice do jogador que tem o personagem, ou -1.</summary>
        public static int OwnerOf(int charId)
        {
            for (int i = 0; i < _roster.Length; i++) if (_roster[i] == charId) return i;
            return -1;
        }

        public static void RequestPick(int charId)
        {
            var local = PlayerAgent.Local;
            if (local == null || !_registered) return;
            if (IsServer)
            {
                HostAssign(local.PlayerIndex, charId);
                return;
            }
            Send(MsgPick, NetworkManager.ServerClientId, 8, w => w.WriteValueSafe(charId));
        }

        private static void OnPick(ulong sender, FastBufferReader r)
        {
            if (!IsServer) return;
            r.ReadValueSafe(out int charId);
            int idx = IndexOfClient(sender);
            if (idx >= 0) HostAssign(idx, charId);
        }

        private static void HostAssign(int idx, int charId)
        {
            EnsureRosterSize();
            if (idx < 0 || idx >= _roster.Length) return;
            bool phaseOk = ModConfig.AllowSwapMidRound.Value || (StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase);
            var def = Characters.Get(charId);
            bool valid = charId == Characters.None || (def != null && def.Implemented);
            int owner = OwnerOf(charId);
            bool free = charId == Characters.None || owner < 0 || owner == idx;

            if (phaseOk && valid && free) _roster[idx] = charId;
            BroadcastRoster(); // manda mesmo se recusou, pra o cliente voltar ao estado certo
        }

        private static void BroadcastRoster()
        {
            ApplyRoster();
            int n = _roster.Length;
            SendToAll(MsgRoster, 8 + 4 * n, w =>
            {
                w.WriteValueSafe(n);
                for (int i = 0; i < n; i++) w.WriteValueSafe(_roster[i]);
            });
        }

        private static void OnRoster(ulong sender, FastBufferReader r)
        {
            if (IsServer) return;
            r.ReadValueSafe(out int n);
            if (n < 0 || n > 256) return;
            var roster = new int[n];
            for (int i = 0; i < n; i++) r.ReadValueSafe(out roster[i]);
            _roster = roster;
            ApplyRoster();
        }

        private static void ApplyRoster()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;
            for (int i = 0; i < _roster.Length && i < sor.allPlayerScripts.Length; i++)
                PlayerAgent.For(sor.allPlayerScripts[i])?.ApplyCharacter(_roster[i]);
        }

        // ---------------------------------------------------------------- habilidades

        public static void SendAbility(int playerIdx, int charId, byte type, AbilityPayload p)
        {
            if (!_registered || NM == null || !NM.IsListening) return;
            if (IsServer)
            {
                HostRelay(playerIdx, charId, type, p);
                return;
            }
            Send(MsgAbilityToServer, NetworkManager.ServerClientId, 64, w => WriteAbility(w, playerIdx, charId, type, p));
        }

        private static void OnAbilityToServer(ulong sender, FastBufferReader r)
        {
            if (!IsServer) return;
            ReadAbility(r, out int idx, out int charId, out byte type, out var p);
            if (IndexOfClient(sender) != idx) return;            // só o dono do jogador
            if (idx < 0 || idx >= _roster.Length || _roster[idx] != charId) return; // tem que ter esse personagem
            HostRelay(idx, charId, type, p);
        }

        private static void HostRelay(int idx, int charId, byte type, AbilityPayload p)
        {
            // Regras extras conferidas pelo host.
            if (charId == 2 && type == ReviveAbility.EvRevive && !ReviveAbility.HostAllows(p)) return;

            Deliver(idx, charId, type, p);
            SendToAll(MsgAbilityToClients, 64, w => WriteAbility(w, idx, charId, type, p));
        }

        private static void OnAbilityToClients(ulong sender, FastBufferReader r)
        {
            if (IsServer) return; // host já entregou
            ReadAbility(r, out int idx, out int charId, out byte type, out var p);
            Deliver(idx, charId, type, p);
        }

        private static void Deliver(int idx, int charId, byte type, AbilityPayload p)
        {
            var sor = StartOfRound.Instance;
            if (sor == null || idx < 0 || idx >= sor.allPlayerScripts.Length) return;
            var agent = PlayerAgent.For(sor.allPlayerScripts[idx]);
            if (agent == null) return;
            if (agent.CharacterId != charId) agent.ApplyCharacter(charId); // lista ainda não chegou
            try { agent.Ability?.OnEvent(type, p, agent.IsLocal); }
            catch (Exception e) { Plugin.Log.LogWarning($"Evento de habilidade falhou: {e}"); }
        }

        private static void WriteAbility(FastBufferWriter w, int idx, int charId, byte type, AbilityPayload p)
        {
            w.WriteValueSafe(idx);
            w.WriteValueSafe(charId);
            w.WriteValueSafe(type);
            w.WriteValueSafe(p.A);
            w.WriteValueSafe(p.B);
            w.WriteValueSafe(p.I);
            w.WriteValueSafe(p.Flag);
        }

        private static void ReadAbility(FastBufferReader r, out int idx, out int charId, out byte type, out AbilityPayload p)
        {
            r.ReadValueSafe(out idx);
            r.ReadValueSafe(out charId);
            r.ReadValueSafe(out type);
            p = default;
            r.ReadValueSafe(out p.A);
            r.ReadValueSafe(out p.B);
            r.ReadValueSafe(out p.I);
            r.ReadValueSafe(out p.Flag);
        }

        // ---------------------------------------------------------------- util

        private static int IndexOfClient(ulong clientId)
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return -1;
            for (int i = 0; i < sor.allPlayerScripts.Length; i++)
            {
                var p = sor.allPlayerScripts[i];
                if (p != null && p.actualClientId == clientId && (p.isPlayerControlled || p.isPlayerDead)) return i;
            }
            return -1;
        }

        private delegate void Writer(FastBufferWriter w);

        private static void Send(string msg, ulong target, int size, Writer write)
        {
            try
            {
                using (var w = new FastBufferWriter(size, Allocator.Temp))
                {
                    write(w);
                    NM.CustomMessagingManager.SendNamedMessage(msg, target, w, NetworkDelivery.Reliable);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Falha ao enviar {msg}: {e.Message}"); }
        }

        private static void SendToAll(string msg, int size, Writer write)
        {
            if (NM == null || !NM.IsListening) return;
            try
            {
                using (var w = new FastBufferWriter(size, Allocator.Temp))
                {
                    write(w);
                    NM.CustomMessagingManager.SendNamedMessageToAll(msg, w, NetworkDelivery.Reliable);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Falha ao enviar {msg}: {e.Message}"); }
        }
    }
}
