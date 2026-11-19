using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace NeonRun
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "nantao.neonrun";
        public const string Name = "NeonRun";
        public const string Version = "0.1.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            NeonConfig.Bind(Config);

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{Name} {Version} carregado. Tecla: {NeonConfig.KeyBinding.Value}");
        }

        /// <summary>Recarrega o arquivo de config com o jogo aberto (tecla F8 por padrão).</summary>
        internal static void ReloadConfig()
        {
            Instance.Config.Reload();
            NeonConfig.RaiseReloaded();
            Log.LogInfo("Config recarregada.");
        }
    }
}
