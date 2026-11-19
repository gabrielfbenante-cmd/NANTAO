using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace SustoNantao
{
    /// <summary>
    /// "Diretor de suspense": a tensão sobe quando você está sozinho e no escuro dentro do prédio,
    /// e coisas começam a acontecer SÓ PRA VOCÊ — passos te seguindo, batidas na parede, porta rangendo,
    /// luzes piscando, risadinhas e sussurros. No auge, o susto na tela fica bem mais provável.
    /// </summary>
    internal sealed class SuspenseDirector : MonoBehaviour
    {
        /// <summary>Tensão atual do jogador local (0..1). Lida pelo susto na tela.</summary>
        public static float Tension;

        private float checkTimer, cooldown;
        private int lastEvent = -1;

        private bool built;
        private readonly List<AudioClip> steps = new List<AudioClip>();
        private readonly List<AudioClip> knocks = new List<AudioClip>();
        private readonly List<AudioClip> creaks = new List<AudioClip>();
        private readonly List<AudioClip> whispers = new List<AudioClip>();
        private readonly List<AudioClip> giggles = new List<AudioClip>();

        // Passos que te seguem
        private bool following;
        private int stepsLeft;
        private float stepTimer;
        private Vector3 stepPos;

        private SustoPlugin P => SustoPlugin.Instance;

        /// <summary>Depois de um susto na tela, a tensão alivia um pouco.</summary>
        public static void Release() => Tension = Mathf.Min(Tension, .25f);

        private void Update()
        {
            if (P == null || !P.suspenseEnabled.Value) { Tension = 0f; following = false; return; }
            var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (local == null || local.isPlayerDead) { Tension = 0f; following = false; return; }

            float dt = Time.deltaTime;
            bool inside = local.isInsideFactory && !local.isInHangarShipRoom && !local.isPlayerDead && local.isPlayerControlled
                          && StartOfRound.Instance != null && StartOfRound.Instance.shipHasLanded;
            bool alone = Alone(local, 12f);
            bool dark = !LightOn(local);
            Tension = SuspenseRules.UpdateTension(Tension, dt, inside, alone, dark, P.buildMinutes.Value);

            UpdateFootsteps(local, dt);
            if (!inside || local.inTerminalMenu || local.isTypingChat) return;

            cooldown -= dt;
            checkTimer += dt;
            if (checkTimer < 1f) return;
            float interval = checkTimer;
            checkTimer = 0f;
            if (cooldown > 0f || following) return;
            if (UnityEngine.Random.value >= SuspenseRules.EventChance(Tension, P.maxEventsPerMinute.Value, interval)) return;

            int e = SuspenseRules.PickEvent(Tension, UnityEngine.Random.value, lastEvent);
            if (e < 0) return;
            Build();
            Play((SuspenseEvent)e, local);
            lastEvent = e;
            cooldown = 7f + UnityEngine.Random.value * 6f;
        }

        // ------------------------------------------------------------------ eventos

        private void Play(SuspenseEvent e, PlayerControllerB local)
        {
            Transform cam = local.gameplayCamera != null ? local.gameplayCamera.transform : local.transform;
            switch (e)
            {
                case SuspenseEvent.Creak:
                    PlayAt(RandomPoint(cam, 8f, 15f), Pick(creaks), .75f, local);
                    break;
                case SuspenseEvent.Knock:
                    PlayAt(WallPoint(cam, 6f), Pick(knocks), .9f, local);
                    break;
                case SuspenseEvent.Footsteps:
                    Vector3 back = -cam.forward; back.y = 0f;
                    if (back.sqrMagnitude < .01f) back = -local.transform.forward;
                    stepPos = local.transform.position + back.normalized * (5.5f + UnityEngine.Random.value * 2f);
                    stepsLeft = 5 + UnityEngine.Random.Range(0, 4);
                    stepTimer = .2f;
                    following = true;
                    break;
                case SuspenseEvent.Lights:
                    try { if (RoundManager.Instance != null) RoundManager.Instance.FlickerLights(true, false); }
                    catch (Exception ex) { SustoPlugin.Warn("Luzes: " + ex.Message); }
                    break;
                case SuspenseEvent.Giggle:
                    PlayAt(RandomPoint(cam, 10f, 18f), Pick(giggles), .85f, local);
                    break;
                case SuspenseEvent.Whisper:
                    float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                    var src = PlayAt(cam.position + cam.right * side * .45f - cam.forward * .2f, Pick(whispers), .9f, local);
                    if (src != null) { src.minDistance = .3f; src.maxDistance = 4f; }
                    break;
            }
            if (P.debugLog.Value) SustoPlugin.Log($"Suspense: {e} (tensao {Tension:0.00})");
        }

        /// <summary>Passos chegando por trás. Param na hora que você se vira (o silêncio assusta mais).</summary>
        private void UpdateFootsteps(PlayerControllerB local, float dt)
        {
            if (!following) return;
            Transform cam = local.gameplayCamera != null ? local.gameplayCamera.transform : local.transform;
            Vector3 toSteps = stepPos - cam.position;
            if (stepsLeft <= 0 || toSteps.magnitude < 1.8f || Vector3.Dot(cam.forward, toSteps.normalized) > .7f)
            {
                following = false;
                return;
            }
            stepTimer -= dt;
            if (stepTimer > 0f) return;
            Vector3 toward = local.transform.position - stepPos; toward.y = 0f;
            stepPos += toward.normalized * .6f;
            stepPos.y = local.transform.position.y;
            PlayAt(stepPos, Pick(steps), .8f, local);
            stepsLeft--;
            stepTimer = .42f + UnityEngine.Random.value * .12f;
        }

        // ------------------------------------------------------------------ util

        private static bool Alone(PlayerControllerB local, float radius)
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return true;
            foreach (var p in sor.allPlayerScripts)
            {
                if (p == null || p == local || !p.isPlayerControlled || p.isPlayerDead) continue;
                if (Vector3.Distance(p.transform.position, local.transform.position) < radius) return false;
            }
            return true;
        }

        private static bool LightOn(PlayerControllerB local)
        {
            if (local.helmetLight != null && local.helmetLight.enabled) return true;
            if (local.currentlyHeldObjectServer is FlashlightItem held && held.isBeingUsed) return true;
            return local.pocketedFlashlight is FlashlightItem pocket && pocket.isBeingUsed;
        }

        private static int Mask => StartOfRound.Instance != null ? StartOfRound.Instance.collidersAndRoomMaskAndDefault : ~0;

        /// <summary>Ponto aleatório em volta; se tiver parede no caminho, o som vem "de trás" dela.</summary>
        private static Vector3 RandomPoint(Transform cam, float min, float max)
        {
            float ang = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            float dist = UnityEngine.Random.Range(min, max);
            if (Physics.Raycast(cam.position, dir, out RaycastHit hit, dist, Mask, QueryTriggerInteraction.Ignore))
                return hit.point + dir * .3f;
            return cam.position + dir * dist;
        }

        /// <summary>Parede mais próxima em volta (pras batidas).</summary>
        private static Vector3 WallPoint(Transform cam, float range)
        {
            float best = float.MaxValue;
            Vector3 point = cam.position + UnityEngine.Random.onUnitSphere * 3f;
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * Mathf.PI * 2f + UnityEngine.Random.value * .3f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (Physics.Raycast(cam.position, dir, out RaycastHit hit, range, Mask, QueryTriggerInteraction.Ignore) &&
                    hit.distance > 1.2f && hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            return point;
        }

        private static AudioClip Pick(List<AudioClip> list) => list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : null;

        private AudioSource PlayAt(Vector3 position, AudioClip clip, float volume, PlayerControllerB local)
        {
            if (clip == null) return null;
            var go = new GameObject("SustoSuspense");
            go.transform.position = position;
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1.5f;
            a.maxDistance = 30f;
            a.dopplerLevel = 0f;
            // Usa o mesmo canal de áudio do jogador: pega o eco do prédio e o volume do jogo.
            if (local != null && local.movementAudio != null) a.outputAudioMixerGroup = local.movementAudio.outputAudioMixerGroup;
            a.clip = clip;
            a.volume = Mathf.Clamp01(P.suspenseVolume.Value * volume);
            a.Play();
            Destroy(go, clip.length + .3f);
            return a;
        }

        private void Build()
        {
            if (built) return;
            built = true;
            const int rate = 44100;
            // Passos: os do próprio jogo (soam iguais aos dos jogadores) + sintetizados de reserva.
            try
            {
                var surfaces = StartOfRound.Instance != null ? StartOfRound.Instance.footstepSurfaces : null;
                if (surfaces != null)
                    foreach (var s in surfaces)
                        if (s != null && s.clips != null && s.clips.Length > 0) { steps.AddRange(s.clips); break; }
            }
            catch (Exception e) { SustoPlugin.Warn("Passos do jogo: " + e.Message); }
            if (steps.Count == 0) for (int i = 0; i < 4; i++) steps.Add(Clip("SustoPasso" + i, ScareSound.Step(rate, 10 + i), rate));
            for (int i = 0; i < 2; i++) knocks.Add(Clip("SustoBatida" + i, ScareSound.Knock(rate, 20 + i), rate));
            for (int i = 0; i < 2; i++) creaks.Add(Clip("SustoRangido" + i, ScareSound.Creak(rate, 30 + i), rate));
            for (int i = 0; i < 3; i++) whispers.Add(Clip("SustoSussurro" + i, ScareSound.Whisper(rate, 40 + i), rate));
            for (int i = 0; i < 3; i++) giggles.Add(Clip("SustoRisada" + i, ScareSound.Giggle(rate, 50 + i), rate));
        }

        private static AudioClip Clip(string name, float[] samples, int rate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
