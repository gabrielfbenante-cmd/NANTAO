using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Nantao
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "nantao.agents";
        public const string Name = "NantaoAgents";
        public const string Version = "0.2.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModConfig.Bind(Config);

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{Name} {Version} carregado. Habilidade: {ModConfig.AbilityKey.Value} | Menu: {ModConfig.MenuKey.Value}");
        }

        /// <summary>Recarrega o arquivo de config com o jogo aberto (F8 por padrão).</summary>
        internal static void ReloadConfig()
        {
            Instance.Config.Reload();
            ModConfig.RaiseReloaded();
            Log.LogInfo("Config recarregada.");
        }
    }
}
