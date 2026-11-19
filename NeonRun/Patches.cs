using GameNetcodeStuff;
using HarmonyLib;

namespace NeonRun
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerPatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(PlayerControllerB __instance)
        {
            if (__instance.GetComponent<NeonRunner>() == null)
                __instance.gameObject.AddComponent<NeonRunner>().Init(__instance);
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        private static void UpdatePrefix(PlayerControllerB __instance)
        {
            NeonRunner.For(__instance)?.BeforeGameUpdate();
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
            NeonRunner.For(__instance)?.AfterGameUpdate();
        }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    internal static class RoundPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix() => NeonNetwork.Register();

        [HarmonyPatch("OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroyPostfix() => NeonNetwork.Unregister();
    }
}
