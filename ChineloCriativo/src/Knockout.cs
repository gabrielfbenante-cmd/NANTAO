using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace ChineloArremessavel
{
    /// <summary>
    /// "Desmaio" depois de tomar a chinelada: o corpo vira um boneco mole (o mesmo ragdoll que o jogo
    /// usa para corpos), voa com o empurrão e fica caído. Quem desmaiou vê a câmera no chão e a tela
    /// escurecendo; depois de alguns segundos levanta onde o corpo caiu.
    /// Roda em TODOS os computadores (cada um mostra o boneco); só o dono do jogador mexe no movimento.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class KnockoutController : MonoBehaviour
    {
        private const float GetUpTime = .9f;

        private PlayerControllerB player;
        private bool active, local;
        private float startedAt, until;
        private GameObject ragdoll;
        private Rigidbody hips, head;
        private readonly List<KeyValuePair<Renderer, bool>> hidden = new List<KeyValuePair<Renderer, bool>>();
        private bool prevMove, prevLook, prevInteract;
        private Vector3 camLocalPosition;
        private bool camSaved;

        public static bool IsKnockedOut(PlayerControllerB player)
        {
            KnockoutController k = player != null ? player.GetComponent<KnockoutController>() : null;
            return k != null && k.active;
        }

        /// <summary>Chamado em todos os PCs quando chega a mensagem do host.</summary>
        public static void Apply(PlayerControllerB player, Vector3 knock, float seconds, int damage)
        {
            if (player == null || player.isPlayerDead || !player.isPlayerControlled) return;
            KnockoutController k = player.GetComponent<KnockoutController>();
            if (k == null) k = player.gameObject.AddComponent<KnockoutController>();
            k.player = player;
            k.Begin(knock, seconds, damage);
        }

        private void Begin(Vector3 knock, float seconds, int damage)
        {
            if (active)
            {
                // Já está no chão: só estica um pouco o tempo.
                until = Mathf.Max(until, Time.time + seconds * .5f);
                return;
            }
            local = player.IsOwner && GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController == player;

            if (local && damage > 0)
            {
                player.DamagePlayer(damage, true, true, CauseOfDeath.Blast, 0, false, Vector3.zero);
                if (player.isPlayerDead) return;
            }

            active = true;
            startedAt = Time.time;
            until = Time.time + seconds;

            ragdoll = RagdollFactory.Spawn(player, knock, !local, out hips, out head);
            Hide();

            if (local)
            {
                if (ChineloPlugin.Instance != null && ChineloPlugin.Instance.dropItem.Value && player.currentlyHeldObjectServer != null)
                {
                    try { player.DiscardHeldObject(false, null, default(Vector3), true); }
                    catch (Exception e) { Debug.LogWarning("Chinelo/desmaio: nao consegui largar o item: " + e.Message); }
                }
                prevMove = player.disableMoveInput;
                prevLook = player.disableLookInput;
                prevInteract = player.disableInteract;
                player.disableMoveInput = true;
                player.disableLookInput = true;
                player.disableInteract = true;
                player.externalForceAutoFade += knock;
                if (player.gameplayCamera != null) { camLocalPosition = player.gameplayCamera.transform.localPosition; camSaved = true; }
                if (HUDManager.Instance != null) HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
        }

        private void Update()
        {
            if (!active) return;
            if (player == null || player.isPlayerDead || !player.isPlayerControlled) { End(false); return; }
            if (Time.time >= until) End(true);
        }

        /// <summary>1 = caído; volta a 0 durante o "levantar".</summary>
        private float Down()
        {
            float fall = Mathf.Clamp01((Time.time - startedAt) / .25f);
            float rise = Mathf.Clamp01((until - Time.time) / GetUpTime);
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(fall, rise));
        }

        private void LateUpdate()
        {
            if (!active || !local || player.gameplayCamera == null) return;
            Transform cam = player.gameplayCamera.transform;
            float k = Down();
            // Câmera vai pra cabeça do boneco (você "é" o corpo caído) e tomba de lado.
            Vector3 lying = head != null ? head.position + Vector3.up * .12f : player.transform.position + Vector3.up * .3f;
            Vector3 standing = cam.parent != null ? cam.parent.TransformPoint(camLocalPosition) : cam.position;
            cam.position = Vector3.Lerp(standing, lying, k);
            Vector3 e = cam.localEulerAngles;
            cam.localRotation = Quaternion.Euler(e.x, e.y, 80f * k);
        }

        private void OnGUI()
        {
            if (!active || !local) return;
            float t = Time.time - startedAt;
            float left = until - Time.time;
            // Apagão rápido, pálpebras "piscando" e clareia ao levantar.
            float alpha = Mathf.Clamp01(t / .2f) * (.72f + .2f * Mathf.Sin(t * 3.1f) * Mathf.Sin(t * 1.3f));
            alpha *= Mathf.Clamp01(left / GetUpTime);
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(Screen.width / 2f - 120, Screen.height * .7f, 240, 30), left > GetUpTime ? "Voce desmaiou..." : "Levantando...");
            GUI.color = Color.white;
        }

        private void End(bool wake)
        {
            if (!active) return;
            active = false;
            Vector3? getUp = null;
            if (wake && hips != null) getUp = hips.position;
            if (ragdoll != null) Destroy(ragdoll);
            ragdoll = null; hips = null; head = null;
            Show();

            if (!local || player == null) return;
            player.disableMoveInput = prevMove;
            player.disableLookInput = prevLook;
            player.disableInteract = prevInteract;
            if (camSaved && player.gameplayCamera != null)
            {
                Transform cam = player.gameplayCamera.transform;
                cam.localPosition = camLocalPosition;
                Vector3 e = cam.localEulerAngles;
                cam.localRotation = Quaternion.Euler(e.x, e.y, 0f);
            }
            camSaved = false;

            // Levanta onde o corpo caiu (se for perto e tiver chão).
            if (getUp.HasValue && !player.isPlayerDead && Vector3.Distance(getUp.Value, player.transform.position) < 15f)
            {
                int mask = StartOfRound.Instance != null ? StartOfRound.Instance.collidersAndRoomMaskAndDefault : ~0;
                RaycastHit hit;
                if (Physics.Raycast(getUp.Value + Vector3.up * 1f, Vector3.down, out hit, 4f, mask, QueryTriggerInteraction.Ignore))
                {
                    try { player.TeleportPlayer(hit.point + Vector3.up * .05f, false, 0f, false, true); }
                    catch (Exception e) { Debug.LogWarning("Chinelo/desmaio: nao consegui levantar no lugar do corpo: " + e.Message); }
                }
            }
        }

        private void Hide()
        {
            hidden.Clear();
            foreach (Renderer r in new Renderer[] { player.thisPlayerModel, player.thisPlayerModelLOD1, player.thisPlayerModelLOD2,
                                                     local ? player.thisPlayerModelArms : null })
            {
                if (r == null) continue;
                hidden.Add(new KeyValuePair<Renderer, bool>(r, r.enabled));
                r.enabled = false;
            }
        }

        private void Show()
        {
            foreach (KeyValuePair<Renderer, bool> pair in hidden)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            hidden.Clear();
        }

        private void OnDestroy()
        {
            if (active) End(false);
        }
    }

    /// <summary>Cria um boneco mole a partir do ragdoll de corpo do próprio jogo (sem a parte de "morto").</summary>
    internal static class RagdollFactory
    {
        public static GameObject Spawn(PlayerControllerB player, Vector3 knock, bool visible, out Rigidbody hips, out Rigidbody head)
        {
            hips = null; head = null;
            StartOfRound sor = StartOfRound.Instance;
            if (sor == null || sor.playerRagdolls == null || sor.playerRagdolls.Count == 0 || sor.playerRagdolls[0] == null) return null;
            try
            {
                // Instancia dentro de um pai desligado: nada do prefab "acorda" antes de limparmos.
                GameObject holder = new GameObject("ChineloDesmaioHolder");
                holder.SetActive(false);
                GameObject go = UnityEngine.Object.Instantiate(sor.playerRagdolls[0], holder.transform);
                go.name = "ChineloDesmaio";
                foreach (DeadBodyInfo c in go.GetComponentsInChildren<DeadBodyInfo>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (ScanNodeProperties c in go.GetComponentsInChildren<ScanNodeProperties>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
                foreach (NetworkBehaviour c in go.GetComponentsInChildren<NetworkBehaviour>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (NetworkObject c in go.GetComponentsInChildren<NetworkObject>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps.gameObject);
                foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) { a.playOnAwake = false; a.Stop(); }
                int radar = LayerMask.NameToLayer("MapRadar");
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    if (t != null && t != go.transform && radar >= 0 && t.gameObject.layer == radar) UnityEngine.Object.DestroyImmediate(t.gameObject);

                go.transform.SetParent(null, false);
                go.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
                UnityEngine.Object.Destroy(holder);

                // Mesma roupa do jogador.
                SkinnedMeshRenderer skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skin != null && player.thisPlayerModel != null) skin.sharedMaterial = player.thisPlayerModel.sharedMaterial;

                // Começa na mesma pose em que o jogador estava.
                Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
                if (player.thisPlayerModel != null)
                    foreach (Transform b in player.thisPlayerModel.bones)
                        if (b != null && !bones.ContainsKey(b.name)) bones[b.name] = b;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    Transform source;
                    if (t != go.transform && bones.TryGetValue(t.name, out source)) t.SetPositionAndRotation(source.position, source.rotation);
                }

                if (!visible) foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

                System.Random rng = new System.Random();
                foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.velocity = knock * .65f + new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() * .5f, (float)rng.NextDouble() - .5f);
                    rb.angularVelocity = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * 6f;
                    string n = rb.name.ToLowerInvariant();
                    if (hips == null) hips = rb; // o primeiro corpo rígido é a raiz (quadril)
                    if (n.Contains("head") || (head == null && n.Contains("spine.004"))) head = rb;
                }
                if (head == null) head = hips;

                if (player.thisController != null)
                    foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(c, player.thisController, true);

                UnityEngine.Object.Destroy(go, 30f); // segurança
                return go;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Chinelo/desmaio: nao consegui criar o boneco: " + e);
                return null;
            }
        }
    }
}
