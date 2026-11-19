using System.Collections.Generic;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Clarão: X arremessa uma granada de luz que estoura um pouco acima do chão.
    /// Por padrão usa a granada de atordoamento do próprio jogo (cega quem olhar e atordoa monstros).
    /// </summary>
    internal sealed class FlashAbility : Ability
    {
        private const byte EvThrow = 1, EvStun = 2;

        private float _cooldownUntil;
        private float _gesture;
        private string _msg;
        private float _msgUntil;

        public FlashAbility(PlayerAgent agent) : base(agent) { }

        public override void OnPress()
        {
            if (P.isPlayerDead || !P.isPlayerControlled || P.inSpecialInteractAnimation) return;
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                Agent.PlaySfx(Sounds.Fail, 0.6f);
                _msg = $"Recarregando ({cd:0}s)";
                _msgUntil = Time.time + 1.5f;
                return;
            }
            Vector3 from = HandPosition();
            Vector3 to = AimPoint(ModConfig.FlashRange.Value, 1.3f);
            Throw(from, to);
            Send(EvThrow, new AbilityPayload { A = from, B = to });
            _cooldownUntil = Time.time + ModConfig.FlashCooldown.Value;
        }

        public override void HudTick(AgentHud hud)
        {
            Color c = Color.Lerp(ModConfig.Flash, new Color(0.6f, 0.8f, 1f), 0.3f);
            if (_msg != null && Time.time < _msgUntil) { hud.Status(_msg, new Color(1f, 0.55f, 0.35f)); return; }
            float cd = Cooldown(_cooldownUntil);
            if (cd > 0f)
            {
                hud.Status($"Clarão recarregando: {cd:0}s", new Color(0.7f, 0.7f, 0.7f));
                hud.Bar(1f - cd / Mathf.Max(0.1f, ModConfig.FlashCooldown.Value), new Color(0.6f, 0.6f, 0.6f));
            }
            else hud.Status("[X] arremessar clarão (não olhe!)", c);
        }

        public override void OnEvent(byte type, AbilityPayload p, bool fromSelf)
        {
            if (type == EvStun)
            {
                var e = EnemyById(p.I);
                if (e != null && !e.isEnemyDead && e.enemyType != null && e.enemyType.canBeStunned)
                {
                    try { e.SetEnemyStunned(true, ModConfig.FlashStunTime.Value, P); }
                    catch (System.Exception ex) { Plugin.Log.LogWarning("Atordoar: " + ex.Message); }
                }
                return;
            }
            if (fromSelf || type != EvThrow) return;
            Throw(p.A, p.B);
        }

        private void Throw(Vector3 from, Vector3 to)
        {
            _gesture = 0.45f;
            Agent.PlaySfx(Sounds.SmokeThrow);
            ArcProjectile.Spawn(from, to, 0.5f, ModConfig.Flash, Pop, 0.9f);
        }

        /// <summary>Roda em todos os computadores quando a granada chega.</summary>
        private void Pop(Vector3 pos)
        {
            if (ModConfig.FlashBlindsPlayers.Value)
            {
                try
                {
                    // Igual a granada do jogo: cada PC cega o próprio jogador (se olhou) e atordoa monstros.
                    StunGrenadeItem.StunExplosion(pos, true, 1f, ModConfig.FlashStunTime.Value, 7.5f, false, null, P, 0f);
                    return;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Granada do jogo falhou, usando clarão próprio: " + e.Message);
                }
            }

            // Modo "só monstros": clarão visual + o dono manda atordoar quem está no raio.
            BurstFx.Spawn(pos, ModConfig.Flash, 2.5f, 0.35f);
            Sounds.PlayAt(pos, Sounds.RunOn);
            if (IsLocal)
            {
                foreach (var e in new List<EnemyAI>(EnemiesNear(pos, ModConfig.FlashRadius.Value)))
                {
                    if (e.thisNetworkObject == null) continue;
                    Send(EvStun, new AbilityPayload { I = (int)e.thisNetworkObject.NetworkObjectId });
                }
            }
        }

        public override void LateTick(float dt)
        {
            if (_gesture <= 0f) return;
            _gesture -= dt;
            float w = Mathf.Sin(Mathf.Clamp01(1f - _gesture / 0.45f) * Mathf.PI);
            // Arremesso por cima do ombro.
            Agent.PoseArm(true,
                new Vector3(0.25f, 0.6f, 0.5f), new Vector3(0.05f, 0.8f, 0.8f),
                new Vector3(0.3f, -0.1f, 0.9f), new Vector3(0f, 0.5f, 1f), w, 0.4f);
        }
    }
}
