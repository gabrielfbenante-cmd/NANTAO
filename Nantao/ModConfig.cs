using System;
using BepInEx.Configuration;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Tudo que dá pra ajustar sem recompilar. Arquivo: BepInEx/config/nantao.agents.cfg
    /// (F9 no jogo recarrega).
    /// </summary>
    internal static class ModConfig
    {
        // ---------------- Geral
        public static ConfigEntry<string> AbilityKey;
        public static ConfigEntry<string> MenuKey;
        public static ConfigEntry<string> ReloadKeyBinding;
        public static ConfigEntry<bool> AllowSwapMidRound;
        public static ConfigEntry<bool> OpenMenuOnJoin;
        public static ConfigEntry<float> Volume;
        public static ConfigEntry<bool> ScreenEffects;
        public static ConfigEntry<bool> DebugLog;

        // Nomes exibidos (10 vagas)
        public static readonly ConfigEntry<string>[] Names = new ConfigEntry<string>[Characters.Count];

        // ---------------- Faísca (corrida)
        public static ConfigEntry<float> ExtraSpeed;
        public static ConfigEntry<float> AirSpeedFactor;
        public static ConfigEntry<float> EnergySeconds;
        public static ConfigEntry<float> RechargeSeconds;
        public static ConfigEntry<float> RechargeDelay;
        public static ConfigEntry<float> MinEnergyToActivate;
        public static ConfigEntry<float> SwingFrequency;
        public static ConfigEntry<float> SwingAmount;
        public static ConfigEntry<float> PoseStrength;
        public static ConfigEntry<float> BlendTime;
        public static ConfigEntry<float> FirstPersonArmsForward;
        public static ConfigEntry<float> FirstPersonArmsLift;
        public static ConfigEntry<float> FirstPersonArmsSpread;
        public static ConfigEntry<float> FingerStraighten;
        public static ConfigEntry<bool> ForceLegSprintAnimation;
        public static ConfigEntry<string> PrimaryColor;
        public static ConfigEntry<string> SecondaryColor;
        public static ConfigEntry<float> GlowIntensity;
        public static ConfigEntry<int> BoltsPerHand;
        public static ConfigEntry<float> BoltSize;
        public static ConfigEntry<bool> HandLights;
        public static ConfigEntry<float> HandLightIntensity;
        public static ConfigEntry<bool> HandTrails;
        public static ConfigEntry<float> FovBoost;

        // ---------------- Âncora (teleporte)
        public static ConfigEntry<float> AnchorRange;
        public static ConfigEntry<float> AnchorWindup;
        public static ConfigEntry<float> AnchorCooldown;
        public static ConfigEntry<float> AnchorRecallHold;
        public static ConfigEntry<string> AnchorColor;

        // ---------------- Aurora (reviver)
        public static ConfigEntry<float> ReviveTime;
        public static ConfigEntry<float> ReviveRange;
        public static ConfigEntry<int> RevivesPerRound;
        public static ConfigEntry<string> ReviveColor;

        // ---------------- Névoa (fumaça)
        public static ConfigEntry<int> SmokeCharges;
        public static ConfigEntry<float> SmokeRecharge;
        public static ConfigEntry<float> SmokeDuration;
        public static ConfigEntry<float> SmokeRadius;
        public static ConfigEntry<float> SmokeRange;
        public static ConfigEntry<bool> SmokeBlocksEnemies;
        public static ConfigEntry<string> SmokeMode;
        public static ConfigEntry<string> SmokeColor;

        // ---------------- Breu (passo sombrio)
        public static ConfigEntry<float> BlinkRange;
        public static ConfigEntry<float> BlinkWindup;
        public static ConfigEntry<float> BlinkCooldown;
        public static ConfigEntry<string> BlinkColor;

        // ---------------- Brasa (coquetel de fogo)
        public static ConfigEntry<float> FireRange;
        public static ConfigEntry<float> FireRadius;
        public static ConfigEntry<float> FireDuration;
        public static ConfigEntry<float> FireCooldown;
        public static ConfigEntry<int> FirePlayerDamage;
        public static ConfigEntry<bool> FireFriendlyFire;
        public static ConfigEntry<int> FireSelfHeal;
        public static ConfigEntry<float> FireEnemyHitInterval;
        public static ConfigEntry<string> FireColor;

        // ---------------- Uivo (lobo)
        public static ConfigEntry<float> WolfSpeed;
        public static ConfigEntry<float> WolfMaxTime;
        public static ConfigEntry<float> WolfStunTime;
        public static ConfigEntry<float> WolfStunRadius;
        public static ConfigEntry<float> WolfCooldown;
        public static ConfigEntry<string> WolfColor;

        // ---------------- Fenda (fenda jogada)
        public static ConfigEntry<float> RiftSpeed;
        public static ConfigEntry<float> RiftTravelTime;
        public static ConfigEntry<float> RiftLifetime;
        public static ConfigEntry<float> RiftCooldown;
        public static ConfigEntry<string> RiftColor;

        // ---------------- Salva (bombinhas)
        public static ConfigEntry<int> BombUsesPerRound;
        public static ConfigEntry<int> BombCount;
        public static ConfigEntry<float> BombSpread;
        public static ConfigEntry<float> BombFuse;
        public static ConfigEntry<int> BombDamage;
        public static ConfigEntry<float> BombRadius;
        public static ConfigEntry<float> BombRange;
        public static ConfigEntry<bool> BombFriendlyFire;
        public static ConfigEntry<string> BombColor;

        // ---------------- Clarão (granada de luz)
        public static ConfigEntry<float> FlashRange;
        public static ConfigEntry<float> FlashCooldown;
        public static ConfigEntry<float> FlashStunTime;
        public static ConfigEntry<float> FlashRadius;
        public static ConfigEntry<bool> FlashBlindsPlayers;
        public static ConfigEntry<string> FlashColor;

        public static event Action Reloaded;

        public static void Bind(ConfigFile cfg)
        {
            // Geral
            AbilityKey = cfg.Bind("Geral", "TeclaHabilidade", "<Keyboard>/x",
                "Tecla da habilidade do personagem (formato Unity Input System: <Keyboard>/x, <Keyboard>/q, <Mouse>/forwardButton...).");
            MenuKey = cfg.Bind("Geral", "TeclaMenu", "<Keyboard>/f1", "Abre o menu de escolha de personagem.");
            ReloadKeyBinding = cfg.Bind("Geral", "TeclaRecarregarConfig", "<Keyboard>/f9",
                "Recarrega este arquivo com o jogo aberto. (Não use F8: é do FPSAdaptativo.)");
            // Configs da versão 0.2.0 vinham com F8, que conflita com o FPSAdaptativo.
            if (ReloadKeyBinding.Value == "<Keyboard>/f8") ReloadKeyBinding.Value = "<Keyboard>/f9";
            AllowSwapMidRound = cfg.Bind("Geral", "TrocarDuranteRodada", false,
                "Se false, só dá pra escolher/trocar personagem com a nave em órbita. (Vale a config do HOST.)");
            OpenMenuOnJoin = cfg.Bind("Geral", "AbrirMenuAoEntrar", true,
                "Abre o menu automaticamente quando você entra na nave sem personagem.");
            Volume = cfg.Bind("Geral", "Volume", 0.8f, "Volume dos sons das habilidades.");
            ScreenEffects = cfg.Bind("Geral", "EfeitosDeTela", true, "Vinhetas e efeitos de tela (só pra você).");
            DebugLog = cfg.Bind("Debug", "LogDetalhado", false,
                "Escreve no log ossos encontrados, parâmetros de animação, etc. Útil pra mandar se algo ficar estranho.");

            for (int i = 0; i < Characters.Count; i++)
            {
                var def = Characters.All[i];
                Names[i] = cfg.Bind("Personagens", $"Nome{i + 1:00}", def.DefaultName,
                    $"Nome exibido da vaga {i + 1}. ({(def.Implemented ? def.Description : "ainda não implementado")})");
            }

            // Faísca
            const string F = "Faisca (corrida)";
            ExtraSpeed = cfg.Bind(F, "VelocidadeExtra", 5.5f, "Metros por segundo a mais com a corrida ligada.");
            AirSpeedFactor = cfg.Bind(F, "FatorNoAr", 0.6f, "Quanto da velocidade extra vale no ar (0 a 1).");
            EnergySeconds = cfg.Bind(F, "DuracaoEnergia", 8f, "Segundos de corrida com energia cheia.");
            RechargeSeconds = cfg.Bind(F, "TempoRecarga", 10f, "Segundos pra recarregar de 0 até cheia.");
            RechargeDelay = cfg.Bind(F, "AtrasoRecarga", 1.0f, "Segundos depois de desligar até começar a recarregar.");
            MinEnergyToActivate = cfg.Bind(F, "EnergiaMinima", 0.15f, "Energia mínima (0 a 1) para ligar.");
            SwingFrequency = cfg.Bind(F, "FrequenciaBalanco", 2.7f, "Balanços de braço por segundo.");
            SwingAmount = cfg.Bind(F, "AmplitudeBalanco", 1.0f, "Quanto os braços vão pra frente/trás.");
            PoseStrength = cfg.Bind(F, "ForcaPose", 1.0f, "0 = animação normal, 1 = pose de corrida completa.");
            BlendTime = cfg.Bind(F, "TempoTransicao", 0.18f, "Segundos pra entrar/sair da pose.");
            FirstPersonArmsForward = cfg.Bind(F, "PrimeiraPessoaFrente", 0.25f, "1ª pessoa: braços projetados pra frente.");
            FirstPersonArmsLift = cfg.Bind(F, "PrimeiraPessoaAltura", 0.35f, "1ª pessoa: altura das mãos (aumente se não aparecem).");
            FirstPersonArmsSpread = cfg.Bind(F, "PrimeiraPessoaAbertura", 0.25f, "1ª pessoa: distância lateral entre os braços.");
            FingerStraighten = cfg.Bind(F, "DedosRetos", 0.85f, "0 = dedos normais, 1 = mão reta.");
            ForceLegSprintAnimation = cfg.Bind(F, "PernasCorrendo", true, "Força a animação de corrida das pernas.");
            PrimaryColor = cfg.Bind(F, "CorPrincipal", "#2FD0FF", "Cor principal da energia (hex).");
            SecondaryColor = cfg.Bind(F, "CorSecundaria", "#FFE24A", "Cor secundária dos raios (hex).");
            GlowIntensity = cfg.Bind(F, "Brilho", 4f, "Intensidade do brilho dos efeitos (vale pra todos os personagens).");
            BoltsPerHand = cfg.Bind(F, "RaiosPorMao", 4, "Raios elétricos em cada mão.");
            BoltSize = cfg.Bind(F, "TamanhoRaios", 1.0f, "Multiplicador do tamanho dos raios.");
            HandLights = cfg.Bind(F, "LuzNasMaos", true, "Mãos iluminam o ambiente.");
            HandLightIntensity = cfg.Bind(F, "IntensidadeLuz", 1.0f, "Multiplicador da luz das mãos.");
            HandTrails = cfg.Bind(F, "RastroNasMaos", true, "Rastro de energia seguindo as mãos.");
            FovBoost = cfg.Bind(F, "AumentoFOV", 9f, "Graus de FOV a mais correndo (só pra você).");

            // Âncora
            const string A = "Ancora (teleporte)";
            AnchorRange = cfg.Bind(A, "Alcance", 60f, "Distância máxima (m) da âncora pra conseguir teleportar.");
            AnchorWindup = cfg.Bind(A, "TempoCarregar", 0.5f, "Segundos entre apertar X e teleportar.");
            AnchorCooldown = cfg.Bind(A, "Recarga", 25f, "Segundos de recarga depois de teleportar.");
            AnchorRecallHold = cfg.Bind(A, "SegurarParaRecolher", 0.7f, "Segure X esse tempo para recolher a âncora sem teleportar.");
            AnchorColor = cfg.Bind(A, "Cor", "#FFC23D", "Cor da âncora (hex).");

            // Aurora
            const string R = "Aurora (reviver)";
            ReviveTime = cfg.Bind(R, "TempoReviver", 3.5f, "Segundos segurando X perto do corpo para reviver.");
            ReviveRange = cfg.Bind(R, "Alcance", 3f, "Distância máxima (m) até o corpo.");
            RevivesPerRound = cfg.Bind(R, "RevivesPorRodada", 1, "Quantas vezes pode reviver por rodada (recarrega na órbita).");
            ReviveColor = cfg.Bind(R, "Cor", "#4DFFC3", "Cor da energia de cura (hex).");

            // Névoa
            const string B = "Nevoa (fumaca)";
            SmokeCharges = cfg.Bind(B, "Cargas", 2, "Quantas fumaças guardadas no máximo.");
            SmokeRecharge = cfg.Bind(B, "RecargaPorCarga", 30f, "Segundos pra recuperar uma carga.");
            SmokeDuration = cfg.Bind(B, "Duracao", 15f, "Segundos que cada fumaça dura.");
            SmokeRadius = cfg.Bind(B, "Raio", 4.5f, "Raio da fumaça (m).");
            SmokeRange = cfg.Bind(B, "Alcance", 45f, "Distância máxima (m) que você consegue mandar a fumaça.");
            SmokeBlocksEnemies = cfg.Bind(B, "BloqueiaVisaoMonstros", true, "Monstros não enxergam através da fumaça.");
            SmokeMode = cfg.Bind(B, "Modo", "ambos",
                "Como a fumaça é desenhada: 'volumetrica' (neblina real do jogo), 'esferas' (camadas translúcidas) ou 'ambos'. Se não aparecer, tente 'esferas'.");
            SmokeColor = cfg.Bind(B, "Cor", "#3A4256", "Cor da fumaça (hex).");

            // Breu
            const string O = "Breu (passo sombrio)";
            BlinkRange = cfg.Bind(O, "Alcance", 18f, "Distância máxima (m) do teleporte curto.");
            BlinkWindup = cfg.Bind(O, "TempoCarregar", 0.8f, "Segundos entre apertar X e aparecer no destino.");
            BlinkCooldown = cfg.Bind(O, "Recarga", 12f, "Segundos de recarga.");
            BlinkColor = cfg.Bind(O, "Cor", "#7A3CFF", "Cor das sombras (hex).");

            // Brasa
            const string Fo = "Brasa (fogo)";
            FireRange = cfg.Bind(Fo, "Alcance", 25f, "Distância máxima (m) do arremesso.");
            FireRadius = cfg.Bind(Fo, "Raio", 3f, "Raio da área de fogo (m).");
            FireDuration = cfg.Bind(Fo, "Duracao", 7f, "Segundos que o fogo queima.");
            FireCooldown = cfg.Bind(Fo, "Recarga", 25f, "Segundos de recarga.");
            FirePlayerDamage = cfg.Bind(Fo, "DanoPorSegundo", 10, "Dano por segundo em jogadores dentro do fogo (se DanoEmAmigos).");
            FireFriendlyFire = cfg.Bind(Fo, "DanoEmAmigos", false, "O fogo machuca outros jogadores.");
            FireSelfHeal = cfg.Bind(Fo, "CuraPropria", 5, "Vida por segundo que você recupera dentro do SEU fogo (0 = desliga).");
            FireEnemyHitInterval = cfg.Bind(Fo, "IntervaloAcertoMonstros", 1f, "Segundos entre cada 'acerto' do fogo nos monstros.");
            FireColor = cfg.Bind(Fo, "Cor", "#FF6A1F", "Cor do fogo (hex).");

            // Uivo
            const string W = "Uivo (lobo)";
            WolfSpeed = cfg.Bind(W, "Velocidade", 9f, "Velocidade do lobo (m/s).");
            WolfMaxTime = cfg.Bind(W, "DuracaoMaxima", 3.5f, "Segundos que o lobo corre antes de sumir.");
            WolfStunTime = cfg.Bind(W, "TempoAtordoar", 4f, "Segundos que os monstros ficam atordoados.");
            WolfStunRadius = cfg.Bind(W, "RaioAtordoar", 3.5f, "Raio do bote (m).");
            WolfCooldown = cfg.Bind(W, "Recarga", 30f, "Segundos de recarga.");
            WolfColor = cfg.Bind(W, "Cor", "#7CFF6B", "Cor do lobo (hex).");

            // Fenda
            const string Rf = "Fenda (teleporte jogado)";
            RiftSpeed = cfg.Bind(Rf, "Velocidade", 7f, "Velocidade da fenda andando pelo chão (m/s).");
            RiftTravelTime = cfg.Bind(Rf, "TempoAndando", 3f, "Segundos que a fenda anda antes de parar.");
            RiftLifetime = cfg.Bind(Rf, "Duracao", 10f, "Segundos até a fenda sumir se você não usar.");
            RiftCooldown = cfg.Bind(Rf, "Recarga", 25f, "Segundos de recarga.");
            RiftColor = cfg.Bind(Rf, "Cor", "#3E7BFF", "Cor da fenda (hex).");

            // Salva
            const string S = "Salva (bombinhas)";
            BombUsesPerRound = cfg.Bind(S, "UsosPorRodada", 2, "Quantas vezes por rodada (recarrega em órbita).");
            BombCount = cfg.Bind(S, "BombasPorUso", 3, "Quantas bombinhas caem em cada uso.");
            BombSpread = cfg.Bind(S, "Espalhamento", 2.2f, "Quanto as bombinhas se espalham (m).");
            BombFuse = cfg.Bind(S, "Pavio", 1.2f, "Segundos no chão antes de explodir.");
            BombDamage = cfg.Bind(S, "DanoJogadores", 35, "Dano em jogadores pegos na explosão (se DanoEmAmigos).");
            BombRadius = cfg.Bind(S, "RaioExplosao", 4.5f, "Raio de dano da explosão (m).");
            BombRange = cfg.Bind(S, "Alcance", 35f, "Distância máxima (m) do arremesso.");
            BombFriendlyFire = cfg.Bind(S, "DanoEmAmigos", false, "As explosões machucam jogadores (inclusive você).");
            BombColor = cfg.Bind(S, "Cor", "#FF3B3B", "Cor das bombinhas (hex).");

            // Clarão
            const string L = "Clarao (granada de luz)";
            FlashRange = cfg.Bind(L, "Alcance", 30f, "Distância máxima (m) do arremesso.");
            FlashCooldown = cfg.Bind(L, "Recarga", 20f, "Segundos de recarga.");
            FlashStunTime = cfg.Bind(L, "TempoAtordoar", 4f, "Segundos que os monstros ficam atordoados.");
            FlashRadius = cfg.Bind(L, "Raio", 8f, "Raio em que os monstros são atordoados quando CegaJogadores=false (m).");
            FlashBlindsPlayers = cfg.Bind(L, "CegaJogadores", true,
                "true = usa a granada de luz do próprio jogo (cega QUALQUER jogador que olhar, inclusive você). false = só atordoa monstros.");
            FlashColor = cfg.Bind(L, "Cor", "#E8F4FF", "Cor do clarão (hex).");
        }

        public static void RaiseReloaded() => Reloaded?.Invoke();

        public static string CharacterName(int id) =>
            id >= 0 && id < Characters.Count && Names[id] != null ? Names[id].Value : "?";

        public static Color Primary => ParseColor(PrimaryColor.Value, new Color(0.18f, 0.82f, 1f));
        public static Color Secondary => ParseColor(SecondaryColor.Value, new Color(1f, 0.89f, 0.29f));
        public static Color Anchor => ParseColor(AnchorColor.Value, new Color(1f, 0.76f, 0.24f));
        public static Color Revive => ParseColor(ReviveColor.Value, new Color(0.3f, 1f, 0.76f));
        public static Color Smoke => ParseColor(SmokeColor.Value, new Color(0.23f, 0.26f, 0.34f));
        public static Color Blink => ParseColor(BlinkColor.Value, new Color(0.48f, 0.24f, 1f));
        public static Color Fire => ParseColor(FireColor.Value, new Color(1f, 0.42f, 0.12f));
        public static Color Wolf => ParseColor(WolfColor.Value, new Color(0.49f, 1f, 0.42f));
        public static Color Rift => ParseColor(RiftColor.Value, new Color(0.24f, 0.48f, 1f));
        public static Color Bomb => ParseColor(BombColor.Value, new Color(1f, 0.23f, 0.23f));
        public static Color Flash => ParseColor(FlashColor.Value, new Color(0.91f, 0.96f, 1f));

        public static Color ParseColor(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }
}
