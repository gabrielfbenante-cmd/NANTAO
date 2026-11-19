using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Nantao
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerPatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(PlayerControllerB __instance)
        {
            if (__instance.GetComponent<PlayerAgent>() == null)
                __instance.gameObject.AddComponent<PlayerAgent>().Init(__instance);
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        private static void UpdatePrefix(PlayerControllerB __instance) => PlayerAgent.For(__instance)?.BeforeGameUpdate();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance) => PlayerAgent.For(__instance)?.AfterGameUpdate();
    }

    [HarmonyPatch(typeof(StartOfRound))]
    internal static class RoundPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix() => AgentNetwork.Register();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix() => AgentNetwork.Tick();

        [HarmonyPatch("OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroyPostfix() => AgentNetwork.Unregister();
    }

    /// <summary>Monstros não enxergam através da fumaça do Breu.</summary>
    [HarmonyPatch(typeof(EnemyAI))]
    internal static class EnemySightPatches
    {
        private static Vector3 Eye(EnemyAI e, Transform overrideEye = null) =>
            overrideEye != null ? overrideEye.position : e.eye != null ? e.eye.position : e.transform.position + Vector3.up;

        private static bool Blocked(EnemyAI e, Vector3 target, Transform overrideEye = null) =>
            ModConfig.SmokeBlocksEnemies.Value && SmokeCloud.Active.Count > 0 && SmokeCloud.Blocks(Eye(e, overrideEye), target);

        private static Vector3 PlayerPoint(PlayerControllerB p) =>
            p.gameplayCamera != null ? p.gameplayCamera.transform.position : p.transform.position + Vector3.up * 1.5f;

        [HarmonyPatch(nameof(EnemyAI.CheckLineOfSightForPlayer))]
        [HarmonyPostfix]
        private static void ForPlayer(EnemyAI __instance, ref PlayerControllerB __result)
        {
            if (__result != null && Blocked(__instance, PlayerPoint(__result))) __result = null;
        }

        [HarmonyPatch(nameof(EnemyAI.CheckLineOfSightForClosestPlayer))]
        [HarmonyPostfix]
        private static void ForClosestPlayer(EnemyAI __instance, ref PlayerControllerB __result)
        {
            if (__result != null && Blocked(__instance, PlayerPoint(__result))) __result = null;
        }

        [HarmonyPatch(nameof(EnemyAI.CheckLineOfSightForPosition))]
        [HarmonyPostfix]
        private static void ForPosition(EnemyAI __instance, ref bool __result, Vector3 objectPosition, Transform overrideEye)
        {
            if (__result && Blocked(__instance, objectPosition, overrideEye)) __result = false;
        }

        [HarmonyPatch(nameof(EnemyAI.CheckLineOfSight))]
        [HarmonyPostfix]
        private static void ForObjects(EnemyAI __instance, ref GameObject __result, Transform useEye)
        {
            if (__result != null && Blocked(__instance, __result.transform.position, useEye)) __result = null;
        }
    }
}
