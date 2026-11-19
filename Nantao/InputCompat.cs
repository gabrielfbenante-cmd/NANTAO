using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nantao
{
    /// <summary>
    /// Compatibilidade de teclas com outros mods do modpack.
    /// O ReservedWalkieSlot usa X pra falar no rádio, mas o X é das habilidades:
    /// se a tecla dele ainda estiver no padrão, troca pra V (só nesta sessão; o menu de controles continua mandando).
    /// </summary>
    internal static class InputCompat
    {
        private static bool _done;
        private static float _next;

        public static void Tick()
        {
            if (_done || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 3f;
            if (!ModConfig.WalkieOnV.Value) { _done = true; return; }
            try
            {
                foreach (InputAction action in InputSystem.ListEnabledActions())
                {
                    string name = action.name.ToLowerInvariant();
                    if (!name.Contains("walkie") || !name.Contains("activate")) continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding b = action.bindings[i];
                        if (b.isComposite || b.isPartOfComposite) continue;
                        if (!string.IsNullOrEmpty(b.overridePath)) { _done = true; return; } // você já escolheu outra tecla
                        if (string.Equals(b.path, "<Keyboard>/x", StringComparison.OrdinalIgnoreCase))
                        {
                            action.ApplyBindingOverride(i, "<Keyboard>/v");
                            Plugin.Log.LogInfo("Radio (ReservedWalkieSlot): tecla trocada de X para V.");
                            _done = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui ajustar a tecla do radio: " + e.Message);
                _done = true;
            }
        }
    }
}
