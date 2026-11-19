using System;
using UnityEngine;

namespace Nantao
{
    internal sealed class CharacterDef
    {
        public int Id;
        public string DefaultName;
        public string Description;
        public Func<Color> Color;
        public Func<PlayerAgent, Ability> Create;

        public bool Implemented => Create != null;
        public string Name => ModConfig.CharacterName(Id);
    }

    /// <summary>
    /// As 10 vagas de personagem. Cada uma só pode ser usada por UM jogador da lobby.
    /// Pra adicionar um novo: preencha Create com a classe da habilidade.
    /// </summary>
    internal static class Characters
    {
        public const int None = -1;
        public const int Count = 10;

        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                Id = 0, DefaultName = "Faísca",
                Description = "Corrida elétrica: X liga/desliga, energia limitada.",
                Color = () => ModConfig.Primary,
                Create = a => new RunAbility(a),
            },
            new CharacterDef
            {
                Id = 1, DefaultName = "Âncora",
                Description = "X coloca a âncora no chão. X de novo: volta pra ela.",
                Color = () => ModConfig.Anchor,
                Create = a => new AnchorAbility(a),
            },
            new CharacterDef
            {
                Id = 2, DefaultName = "Aurora",
                Description = "Segure X perto do corpo de um amigo pra reviver.",
                Color = () => ModConfig.Revive,
                Create = a => new ReviveAbility(a),
            },
            new CharacterDef
            {
                Id = 3, DefaultName = "Breu",
                Description = "X: some nas sombras e reaparece onde você mira (curta distância).",
                Color = () => ModConfig.Blink,
                Create = a => new BlinkAbility(a),
            },
            new CharacterDef
            {
                Id = 4, DefaultName = "Névoa",
                Description = "X manda uma fumaça onde você mira. Monstros não enxergam através.",
                Color = () => ModConfig.Smoke,
                Create = a => new SmokeAbility(a),
            },
            new CharacterDef
            {
                Id = 5, DefaultName = "Brasa",
                Description = "X arremessa um coquetel de fogo. O fogo queima monstros e cura você.",
                Color = () => ModConfig.Fire,
                Create = a => new FireAbility(a),
            },
            new CharacterDef
            {
                Id = 6, DefaultName = "Uivo",
                Description = "X solta um lobo de energia que corre e atordoa monstros. X de novo: bote.",
                Color = () => ModConfig.Wolf,
                Create = a => new WolfAbility(a),
            },
            new CharacterDef
            {
                Id = 7, DefaultName = "Fenda",
                Description = "X lança uma fenda que anda pelo chão. X de novo: teleporta até ela.",
                Color = () => ModConfig.Rift,
                Create = a => new RiftAbility(a),
            },
            new CharacterDef
            {
                Id = 8, DefaultName = "Salva",
                Description = "X joga bombinhas no chão onde você mira. 2 usos por rodada.",
                Color = () => ModConfig.Bomb,
                Create = a => new BombAbility(a),
            },
            new CharacterDef
            {
                Id = 9, DefaultName = "Clarão",
                Description = "X arremessa uma granada de luz: cega quem olhar e atordoa monstros.",
                Color = () => ModConfig.Flash,
                Create = a => new FlashAbility(a),
            },
        };

        private static CharacterDef Placeholder(int id) => new CharacterDef
        {
            Id = id,
            DefaultName = $"Personagem {id + 1}",
            Description = "Em breve.",
            Color = () => new Color(0.35f, 0.35f, 0.4f),
            Create = null,
        };

        public static CharacterDef Get(int id) => id >= 0 && id < Count ? All[id] : null;
    }
}
