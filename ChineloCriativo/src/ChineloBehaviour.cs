using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ChineloArremessavel
{
    public sealed class ChineloBehaviour : MonoBehaviour
    {
        public GrabbableObject Native;
        public Transform Visual;
        public bool IsFlying { get; private set; }
        public bool Pending { get { return Time.unscaledTime < pendingUntil; } }
        internal FlightMessage Flight;
        private Vector3 origin, direction;
        private float started, previousDistance, pendingUntil;
        private bool previousGrabbable;
        private AudioSource sound;
        private static AudioClip whoosh, boom;

        private void Start()
        {
            if (!ChineloPlugin.Instances.Contains(this)) ChineloPlugin.Instances.Add(this);
            Native = GetComponent<GrabbableObject>();
            sound = GetComponent<AudioSource>();
            if (sound == null) sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 1f; sound.minDistance = 1f; sound.maxDistance = 25f;
            if (whoosh == null) whoosh = MakeSound("ChineloWhoosh", .25f, true);
        }

        internal void SetPending() { pendingUntil = Time.unscaledTime + 1.5f; }
        internal void ClearPending() { pendingUntil = 0; }

        internal void BeginFlight(FlightMessage flight)
        {
            if (Native == null || !FlightRules.Valid(flight, true)) return;
            CancelFlight();
            Flight = flight; origin = new Vector3(flight.x, flight.y, flight.z);
            direction = new Vector3(flight.dx, flight.dy, flight.dz).normalized;
            started = Time.time; previousDistance = 0;
            previousGrabbable = Native.grabbable;
            Native.grabbable = false; Native.parentObject = null;
            IsFlying = true; ClearPending();
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(direction);
            if (sound != null && whoosh != null) sound.PlayOneShot(whoosh, .5f);
        }

        internal void CancelFlight()
        {
            if (IsFlying && Native != null) Native.grabbable = previousGrabbable;
            IsFlying = false; ClearPending();
            if (Visual != null) Visual.localRotation = Quaternion.identity;
        }

        internal void FinishFlight()
        {
            IsFlying = false; ClearPending();
            if (Visual != null) Visual.gameObject.SetActive(false);
            if (Native != null) { Native.grabbable = false; Native.itemUsedUp = true; }
        }

        private void LateUpdate()
        {
            if (!IsFlying || Native == null || Visual == null) return;
            bool server = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            float distance = FlightRules.Distance(Time.time - started, Flight.speed, Flight.range);
            Vector3 from = origin + direction * previousDistance;
            float step = Mathf.Max(0, distance - previousDistance);
            if (server && step > 0)
            {
                float nearest = float.PositiveInfinity;
                foreach (RaycastHit hit in Physics.SphereCastAll(from, .10f, direction, step, ~0, QueryTriggerInteraction.Collide))
                {
                    Collider collider = hit.collider;
                    if (collider == null || collider.transform.IsChildOf(transform)) continue;
                    PlayerControllerB player = collider.GetComponentInParent<PlayerControllerB>();
                    if (player != null && player.actualClientId == Flight.player) continue;
                    EnemyAI enemy = collider.GetComponentInParent<EnemyAI>();
                    if (collider.isTrigger && player == null && enemy == null) continue;
                    int world = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
                    if (enemy == null && player == null && (world & (1 << collider.gameObject.layer)) == 0 &&
                        collider.GetComponentInParent<GrabbableObject>() == null) continue;
                    if (hit.distance < nearest) nearest = hit.distance;
                }
                if (nearest < float.PositiveInfinity)
                {
                    transform.position = from + direction * Mathf.Max(0, nearest - .015f);
                    ChineloPlugin.Instance.Explode(this, transform.position, "impact"); return;
                }
            }
            transform.position = origin + direction * distance;
            Visual.Rotate(950f * Time.deltaTime, 0, 0, Space.Self);
            previousDistance = distance;
            if (server && distance >= Flight.range) ChineloPlugin.Instance.Explode(this, transform.position, "range");
            else if (!server && Time.time - started > Flight.range / Flight.speed + 3f) CancelFlight();
        }

        internal static void PlayBoom(Vector3 position)
        {
            if (boom == null) boom = MakeSound("ChineloBoom", .65f, false);
            GameObject audio = new GameObject("ChineloExplosionAudio"); audio.transform.position = position;
            AudioSource source = audio.AddComponent<AudioSource>();
            source.spatialBlend = 1; source.minDistance = 2; source.maxDistance = 35;
            source.PlayOneShot(boom, .8f); UnityEngine.Object.Destroy(audio, 2f);
        }

        private static AudioClip MakeSound(string name, float duration, bool air)
        {
            const int rate = 22050;
            float[] samples = new float[(int)(rate * duration)];
            System.Random random = new System.Random(73);
            float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2 - 1);
                low = low * .85f + noise * .15f;
                float envelope = Mathf.Exp(-t * (air ? 17f : 9f));
                samples[i] = envelope * (air ? noise * .22f : low * 1.5f + Mathf.Sin(t * (380f - t * 150f)) * .4f);
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }

        private void OnDestroy() { ChineloPlugin.Instances.Remove(this); }
    }

    [HarmonyPatch(typeof(GrabbableObject), "Update")]
    internal static class StraightFlightUpdatePatch
    {
        private static bool Prefix(GrabbableObject __instance)
        { ChineloBehaviour chinelo = __instance.GetComponent<ChineloBehaviour>(); return chinelo == null || !chinelo.IsFlying; }
    }

    [HarmonyPatch(typeof(GrabbableObject), "LateUpdate")]
    internal static class StraightFlightLateUpdatePatch
    {
        private static bool Prefix(GrabbableObject __instance)
        { ChineloBehaviour chinelo = __instance.GetComponent<ChineloBehaviour>(); return chinelo == null || !chinelo.IsFlying; }
    }
}
