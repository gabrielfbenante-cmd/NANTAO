using System;
using BepInEx.Configuration;
using UnityEngine;

namespace NeonRun
{
    /// <summary>
    /// Tudo que dá pra ajustar sem recompilar. O arquivo fica em
    /// BepInEx/config/nantao.neonrun.cfg e pode ser recarregado no jogo com F8.
    /// </summary>
    internal static class NeonConfig
    {
        // Controles
        public static ConfigEntry<string> KeyBinding;
        public static ConfigEntry<string> ReloadKeyBinding;

        // Movimento / energia
        public static ConfigEntry<float> ExtraSpeed;
        public static ConfigEntry<float> AirSpeedFactor;
        public static ConfigEntry<float> EnergySeconds;
        public static ConfigEntry<float> RechargeSeconds;
        public static ConfigEntry<float> RechargeDelay;
        public static ConfigEntry<float> MinEnergyToActivate;

        // Animação dos braços
        public static ConfigEntry<float> SwingFrequency;
        public static ConfigEntry<float> SwingAmount;
        public static ConfigEntry<float> PoseStrength;
        public static ConfigEntry<float> BlendTime;
        public static ConfigEntry<float> FirstPersonArmsForward;
        public static ConfigEntry<float> FirstPersonArmsLift;
        public static ConfigEntry<float> FirstPersonArmsSpread;
        public static ConfigEntry<float> FingerStraighten;
        public static ConfigEntry<bool> ForceLegSprintAnimation;

        // Visual
        public static ConfigEntry<string> PrimaryColor;
        public static ConfigEntry<string> SecondaryColor;
        public static ConfigEntry<float> GlowIntensity;
        public static ConfigEntry<int> BoltsPerHand;
        public static ConfigEntry<float> BoltSize;
        public static ConfigEntry<bool> HandLights;
        public static ConfigEntry<float> HandLightIntensity;
        public static ConfigEntry<bool> HandTrails;
        public static ConfigEntry<float> FovBoost;
        public static ConfigEntry<bool> ScreenEffects;

        // Som
        public static ConfigEntry<float> Volume;

        // Debug
        public static ConfigEntry<bool> DebugLog;

        public static event Action Reloaded;

        public static void Bind(ConfigFile cfg)
        {
            KeyBinding = cfg.Bind("Controles", "Tecla", "<Keyboard>/x",
                "Tecla que liga/desliga a corrida (formato do Unity Input System, ex: <Keyboard>/x, <Keyboard>/q, <Mouse>/forwardButton).");
            ReloadKeyBinding = cfg.Bind("Controles", "TeclaRecarregarConfig", "<Keyboard>/f8",
                "Recarrega este arquivo com o jogo aberto, pra ajustar os valores ao vivo.");

            ExtraSpeed = cfg.Bind("Movimento", "VelocidadeExtra", 5.5f,
                "Metros por segundo somados à sua velocidade enquanto a corrida está ligada.");
            AirSpeedFactor = cfg.Bind("Movimento", "FatorNoAr", 0.6f,
                "Quanto da velocidade extra vale enquanto você está no ar (0 a 1).");
            EnergySeconds = cfg.Bind("Movimento", "DuracaoEnergia", 8f,
                "Quantos segundos de corrida a energia cheia dura.");
            RechargeSeconds = cfg.Bind("Movimento", "TempoRecarga", 10f,
                "Segundos para recarregar de 0 até cheia.");
            RechargeDelay = cfg.Bind("Movimento", "AtrasoRecarga", 1.0f,
                "Segundos depois de desligar até começar a recarregar.");
            MinEnergyToActivate = cfg.Bind("Movimento", "EnergiaMinima", 0.15f,
                "Energia mínima (0 a 1) para conseguir ligar.");

            SwingFrequency = cfg.Bind("Animacao", "FrequenciaBalanco", 2.7f,
                "Balanços de braço por segundo correndo.");
            SwingAmount = cfg.Bind("Animacao", "AmplitudeBalanco", 1.0f,
                "Quanto os braços vão pra frente/trás (1 = padrão).");
            PoseStrength = cfg.Bind("Animacao", "ForcaPose", 1.0f,
                "0 = animação normal do jogo, 1 = pose de corrida completa.");
            BlendTime = cfg.Bind("Animacao", "TempoTransicao", 0.18f,
                "Segundos para entrar/sair da pose.");
            FirstPersonArmsForward = cfg.Bind("Animacao", "PrimeiraPessoaFrente", 0.25f,
                "Primeira pessoa: quanto os braços ficam projetados pra frente.");
            FirstPersonArmsLift = cfg.Bind("Animacao", "PrimeiraPessoaAltura", 0.35f,
                "Primeira pessoa: quanto as mãos sobem (aumente se as mãos não aparecem na tela).");
            FirstPersonArmsSpread = cfg.Bind("Animacao", "PrimeiraPessoaAbertura", 0.25f,
                "Primeira pessoa: distância lateral entre os braços.");
            FingerStraighten = cfg.Bind("Animacao", "DedosRetos", 0.85f,
                "0 = dedos normais, 1 = mão totalmente reta/aberta.");
            ForceLegSprintAnimation = cfg.Bind("Animacao", "PernasCorrendo", true,
                "Força a animação de corrida das pernas enquanto a habilidade está ligada.");

            PrimaryColor = cfg.Bind("Visual", "CorPrincipal", "#2FD0FF", "Cor principal da energia (hex).");
            SecondaryColor = cfg.Bind("Visual", "CorSecundaria", "#FFE24A", "Cor secundária dos raios (hex).");
            GlowIntensity = cfg.Bind("Visual", "Brilho", 4f, "Intensidade do brilho dos raios.");
            BoltsPerHand = cfg.Bind("Visual", "RaiosPorMao", 4, "Quantos raios elétricos em cada mão.");
            BoltSize = cfg.Bind("Visual", "TamanhoRaios", 1.0f, "Multiplicador do tamanho dos raios.");
            HandLights = cfg.Bind("Visual", "LuzNasMaos", true, "As mãos iluminam o ambiente.");
            HandLightIntensity = cfg.Bind("Visual", "IntensidadeLuz", 1.0f, "Multiplicador da luz das mãos.");
            HandTrails = cfg.Bind("Visual", "RastroNasMaos", true, "Rastro de energia seguindo as mãos.");
            FovBoost = cfg.Bind("Visual", "AumentoFOV", 9f, "Graus de FOV adicionados quando você corre (só pra você).");
            ScreenEffects = cfg.Bind("Visual", "EfeitosDeTela", true, "Vinheta elétrica e linhas de velocidade (só pra você).");

            Volume = cfg.Bind("Som", "Volume", 0.8f, "Volume do zumbido elétrico e dos estalos.");

            DebugLog = cfg.Bind("Debug", "LogDetalhado", false,
                "Escreve no log os ossos encontrados e os parâmetros de animação (útil pra me mandar se algo ficar estranho).");
        }

        public static void RaiseReloaded() => Reloaded?.Invoke();

        public static Color Primary => ParseColor(PrimaryColor.Value, new Color(0.18f, 0.82f, 1f));
        public static Color Secondary => ParseColor(SecondaryColor.Value, new Color(1f, 0.89f, 0.29f));

        private static Color ParseColor(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }
    }
}
