using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Nantao
{
    /// <summary>Dados que uma habilidade manda pela rede.</summary>
    internal struct AbilityPayload
    {
        public Vector3 A;
        public Vector3 B;
        public int I;
        public bool Flag;
    }

    /// <summary>
    /// Base de toda habilidade. Existe uma instância por jogador que escolheu o personagem,
    /// em TODOS os computadores da lobby (assim todo mundo vê os efeitos).
    /// </summary>
    internal abstract class Ability
    {
        protected readonly PlayerAgent Agent;
        protected PlayerControllerB P => Agent.Player;
        protected bool IsLocal => Agent.IsLocal;

        protected Ability(PlayerAgent agent) { Agent = agent; }

        // ---- Só no dono (quem está jogando com o personagem)
        public virtual void OnPress() { }
        public virtual void OnHold(float held) { }
        public virtual void OnRelease(float held) { }
        /// <summary>Depois do Update do jogo, só no dono.</summary>
        public virtual void OwnerTick(float dt) { }
        /// <summary>Preenche o HUD (só no dono).</summary>
        public virtual void HudTick(AgentHud hud) { }
        public virtual float FovOffset => 0f;

        // ---- Em todos os computadores
        /// <summary>LateUpdate: poses, efeitos, sons.</summary>
        public virtual void LateTick(float dt) { }
        /// <summary>Evento recebido pela rede. fromSelf = eu mesmo mandei (eco do host).</summary>
        public virtual void OnEvent(byte type, AbilityPayload p, bool fromSelf) { }
        public virtual void Dispose() { }

        protected void Send(byte type, AbilityPayload p) =>
            AgentNetwork.SendAbility(Agent.PlayerIndex, Agent.CharacterId, type, p);

        protected void Send(byte type) => Send(type, default);

        protected static int Flags(PlayerControllerB p) =>
            (p.isInsideFactory ? 1 : 0) | (p.isInElevator ? 2 : 0) | (p.isInHangarShipRoom ? 4 : 0);

        protected static void ApplyFlags(PlayerControllerB p, int f)
        {
            p.isInsideFactory = (f & 1) != 0;
            p.isInElevator = (f & 2) != 0;
            p.isInHangarShipRoom = (f & 4) != 0;
        }

        protected static int LayerMask =>
            StartOfRound.Instance != null ? StartOfRound.Instance.collidersAndRoomMaskAndDefault : Physics.DefaultRaycastLayers;

        /// <summary>Ponto no chão onde o jogador está mirando (até <paramref name="range"/> metros).</summary>
        protected Vector3 AimPoint(float range, float raise = 0f)
        {
            var cam = P.gameplayCamera.transform;
            Vector3 point;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, range, LayerMask, QueryTriggerInteraction.Ignore))
                point = hit.point + hit.normal * 0.35f;
            else
                point = cam.position + cam.forward * range;
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out var ground, 25f, LayerMask, QueryTriggerInteraction.Ignore))
                point = ground.point;
            return point + Vector3.up * raise;
        }

        /// <summary>Posição da mão direita (pra sair projétil dela).</summary>
        protected Vector3 HandPosition()
        {
            var rig = Agent.FxRig;
            if (rig != null && rig.Valid && rig.Right.Hand != null) return rig.Right.Hand.position;
            var cam = P.gameplayCamera != null ? P.gameplayCamera.transform : P.transform;
            return cam.position + cam.right * 0.3f - cam.up * 0.2f;
        }

        /// <summary>Direção horizontal pra onde o jogador olha.</summary>
        protected Vector3 FlatForward()
        {
            var f = P.gameplayCamera != null ? P.gameplayCamera.transform.forward : P.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : P.transform.forward;
        }

        /// <summary>true no frame em que a nave volta pra órbita (pra recarregar usos por rodada).</summary>
        private bool _lastOrbit = true;
        protected bool EnteredOrbit()
        {
            bool orbit = StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase;
            bool entered = orbit && !_lastOrbit;
            _lastOrbit = orbit;
            return entered;
        }

        protected static float Cooldown(float until) => Mathf.Max(0f, until - Time.time);

        // ---- Monstros

        protected static IEnumerable<EnemyAI> EnemiesNear(Vector3 pos, float radius)
        {
            var rm = RoundManager.Instance;
            if (rm == null || rm.SpawnedEnemies == null) yield break;
            float r2 = radius * radius;
            for (int i = 0; i < rm.SpawnedEnemies.Count; i++)
            {
                var e = rm.SpawnedEnemies[i];
                if (e == null || e.isEnemyDead) continue;
                if ((e.transform.position - pos).sqrMagnitude <= r2) yield return e;
            }
        }

        protected static EnemyAI EnemyById(int id)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue((ulong)id, out var no) && no != null ? no.GetComponent<EnemyAI>() : null;
        }

        protected PlayerControllerB PlayerByIndex(int idx)
        {
            var sor = StartOfRound.Instance;
            return sor != null && idx >= 0 && idx < sor.allPlayerScripts.Length ? sor.allPlayerScripts[idx] : null;
        }
    }
}
