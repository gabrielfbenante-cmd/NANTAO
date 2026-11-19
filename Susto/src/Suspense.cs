using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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
                case SuspenseEvent.FlashlightFlicker:
                    if (LightOn(local)) StartCoroutine(FlickerFlashlight(local));
                    else PlayAt(RandomPoint(cam, 8f, 15f), Pick(creaks), .75f, local); // sem luz: range uma porta
                    break;
                case SuspenseEvent.Eyes:
                    if (DarkEyes.Active == null) DarkEyes.Spawn(EyesPoint(cam), local, Pick(giggles), P.suspenseVolume.Value);
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

        /// <summary>A lanterna (ou a luz do capacete) falha algumas vezes e apaga por um instante. Só na sua tela.</summary>
        private IEnumerator FlickerFlashlight(PlayerControllerB local)
        {
            var lights = new List<Behaviour>();
            FlashlightItem flash = local.currentlyHeldObjectServer as FlashlightItem;
            if (flash == null || !flash.isBeingUsed) flash = local.pocketedFlashlight as FlashlightItem;
            if (flash != null && flash.isBeingUsed)
            {
                if (flash.flashlightBulb != null && flash.flashlightBulb.enabled) lights.Add(flash.flashlightBulb);
                if (flash.flashlightBulbGlow != null && flash.flashlightBulbGlow.enabled) lights.Add(flash.flashlightBulbGlow);
            }
            if (local.helmetLight != null && local.helmetLight.enabled) lights.Add(local.helmetLight);
            if (lights.Count == 0) yield break;

            try
            {
                if (flash != null && flash.flashlightAudio != null && flash.flashlightFlicker != null)
                    flash.flashlightAudio.PlayOneShot(flash.flashlightFlicker, .8f);
                int flicks = UnityEngine.Random.Range(4, 8);
                for (int i = 0; i < flicks; i++)
                {
                    foreach (var l in lights) if (l != null) l.enabled = false;
                    yield return new WaitForSeconds(UnityEngine.Random.Range(.04f, .14f));
                    foreach (var l in lights) if (l != null) l.enabled = true;
                    yield return new WaitForSeconds(UnityEngine.Random.Range(.05f, .22f));
                }
                // Apaga de vez por um instante... e volta.
                foreach (var l in lights) if (l != null) l.enabled = false;
                yield return new WaitForSeconds(UnityEngine.Random.Range(.6f, 1.2f));
            }
            finally
            {
                foreach (var l in lights) if (l != null) l.enabled = true;
            }
        }

        /// <summary>Lugar escuro lá na frente (8–16 m), na altura dos olhos, encostado numa parede se tiver.</summary>
        private static Vector3 EyesPoint(Transform cam)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 dir = Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-32f, 32f), 0f) * cam.forward;
                dir.y = Mathf.Clamp(dir.y, -.08f, .08f);
                dir.Normalize();
                bool wall = Physics.Raycast(cam.position, dir, out RaycastHit hit, 16f, Mask, QueryTriggerInteraction.Ignore);
                if (wall && hit.distance < 8f) continue;
                Vector3 p = wall ? hit.point - dir * .25f : cam.position + dir * 15f;
                if (Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit floor, 4f, Mask, QueryTriggerInteraction.Ignore))
                    p.y = floor.point.y + 1.45f + UnityEngine.Random.Range(-.25f, .2f);
                return p;
            }
            return cam.position + cam.forward * 12f;
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

    /// <summary>
    /// Olhos vermelhos e um sorriso fino no escuro (desenho original). Ficam te olhando e somem do nada
    /// quando você chega perto, aponta a lanterna ou depois de um tempo. Só você vê.
    /// </summary>
    internal sealed class DarkEyes : MonoBehaviour
    {
        public static DarkEyes Active;

        private PlayerControllerB local;
        private Transform leftEye, rightEye;
        private LineRenderer smile;
        private Material eyeMat, smileMat;
        private AudioClip vanishGiggle;
        private float volume, age, life, nextBlink, blinkUntil;
        private static Shader unlit;

        public static void Spawn(Vector3 position, PlayerControllerB local, AudioClip giggle, float volume)
        {
            var go = new GameObject("SustoOlhosNoEscuro");
            go.transform.position = position;
            var e = go.AddComponent<DarkEyes>();
            e.local = local;
            e.vanishGiggle = giggle;
            e.volume = volume;
            e.Build();
            Active = e;
        }

        private void Build()
        {
            life = 7f + UnityEngine.Random.value * 6f;
            nextBlink = 1.5f + UnityEngine.Random.value * 2f;
            eyeMat = Glow(new Color(1f, .05f, .02f), 12f);
            smileMat = Glow(new Color(1f, .75f, .7f), 2.5f);
            float spacing = .16f + UnityEngine.Random.value * .05f;
            leftEye = Eye(new Vector3(-spacing / 2f, 0f, 0f));
            rightEye = Eye(new Vector3(spacing / 2f, .01f, 0f));

            // Sorriso: arco largo e fino, cantos subindo.
            var go = new GameObject("Sorriso");
            go.transform.SetParent(transform, false);
            smile = go.AddComponent<LineRenderer>();
            smile.useWorldSpace = false;
            smile.positionCount = 16;
            smile.widthMultiplier = .012f;
            smile.sharedMaterial = smileMat;
            smile.shadowCastingMode = ShadowCastingMode.Off;
            smile.receiveShadows = false;
            float width = .3f + UnityEngine.Random.value * .12f;
            for (int i = 0; i < 16; i++)
            {
                float k = i / 15f * 2f - 1f; // -1..1
                smile.SetPosition(i, new Vector3(k * width / 2f, -.17f + .07f * k * k, 0f));
            }
            smile.enabled = UnityEngine.Random.value < .75f; // às vezes só os olhos
        }

        private Transform Eye(Vector3 offset)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = e.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = e.GetComponent<MeshRenderer>();
            mr.sharedMaterial = eyeMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            e.transform.SetParent(transform, false);
            e.transform.localPosition = offset;
            e.transform.localScale = new Vector3(.055f, .03f, .02f);
            return e.transform;
        }

        private void Update()
        {
            if (local == null || local.isPlayerDead) { Vanish(false); return; }
            Transform cam = local.gameplayCamera != null ? local.gameplayCamera.transform : local.transform;
            age += Time.deltaTime;

            // Sempre virado pra você.
            Vector3 toEyes = transform.position - cam.position;
            transform.rotation = Quaternion.LookRotation(toEyes, Vector3.up);

            // Piscadas.
            if (age > nextBlink) { blinkUntil = age + .12f; nextBlink = age + 2f + UnityEngine.Random.value * 3f; }
            float open = age < blinkUntil ? .1f : 1f;
            leftEye.localScale = new Vector3(.055f, .03f * open, .02f);
            rightEye.localScale = new Vector3(.055f, .03f * open, .02f);

            float dist = toEyes.magnitude;
            bool approached = dist < 6.5f;
            bool lit = LightPointedAt(cam, toEyes);
            if (approached || lit || age > life) Vanish(approached || lit);
        }

        private bool LightPointedAt(Transform cam, Vector3 toEyes)
        {
            if (toEyes.magnitude > 22f) return false;
            bool lightOn = (local.helmetLight != null && local.helmetLight.enabled) ||
                           (local.currentlyHeldObjectServer is FlashlightItem f && f.isBeingUsed);
            return lightOn && Vector3.Angle(cam.forward, toEyes) < 9f;
        }

        /// <summary>Some do nada. Às vezes deixa uma risadinha baixinha pra trás.</summary>
        private void Vanish(bool spooked)
        {
            if (spooked && vanishGiggle != null && UnityEngine.Random.value < .3f)
            {
                var go = new GameObject("SustoRisadaSumindo");
                go.transform.position = transform.position;
                var a = go.AddComponent<AudioSource>();
                a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = 25f;
                if (local != null && local.movementAudio != null) a.outputAudioMixerGroup = local.movementAudio.outputAudioMixerGroup;
                a.clip = vanishGiggle; a.volume = Mathf.Clamp01(volume * .45f); a.Play();
                Destroy(go, vanishGiggle.length + .3f);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (eyeMat != null) Destroy(eyeMat);
            if (smileMat != null) Destroy(smileMat);
        }

        private static Material Glow(Color color, float intensity)
        {
            if (unlit == null)
            {
                unlit = Shader.Find("HDRP/Unlit");
                if (unlit == null)
                    foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                        if (m != null && m.shader != null && m.shader.name == "HDRP/Unlit") { unlit = m.shader; break; }
                if (unlit == null) unlit = Shader.Find("Unlit/Color");
            }
            var mat = new Material(unlit);
            Color c = color * intensity; c.a = 1f;
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (unlit.name.StartsWith("HDRP")) { try { HDMaterial.ValidateMaterial(mat); } catch (Exception) { } }
            return mat;
        }
    }
}
