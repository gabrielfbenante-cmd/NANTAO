# NANTAO: mods de Lethal Company

Este repositório tem **quatro mods** (cinco plugins):

| Mod | Pasta | Plugins (DLL) | O que faz |
|---|---|---|---|
| **NantaoAgents** | `Nantao/` | `NantaoAgents.dll` | 10 personagens com habilidades |
| **ChineloComCriativo** | `ChineloCriativo/` | `ChineloArremessavel.dll` + `ModoCriativoTeste.dll` | Chinelo que arremessa e desmaia quem acerta (10% de explosão 5x) + modo criativo |
| **CarrinhoCriativo** | `CarrinhoCriativo/` | `CarrinhoCriativo.dll` | Carrinho grande (20 itens + passageiro) que pega embalo em descidas |
| **SustoNantao** | `Susto/` | `SustoNantao.dll` | Suspense (passos, batidas, risadinhas, sussurros) e sustos na tela (rosto, investida, glitch, espreita) |

## Modpack NantaoTerror (Thunderstore)

Perfil pronto para o r2modman/Thunderstore Mod Manager com **29 mods escolhidos a dedo**: terror e ambientação (vozes imitadas, sanidade, sons), **desempenho para PC fraco** (inclusive o CullFactory, que não renderiza salas que você não vê) e o melhor de luas, monstros e itens novos. Veja [`modpack/LEIA-ME.md`](modpack/LEIA-ME.md).

## Instalar tudo de uma vez (recomendado)

1. No GitHub, abra **Actions**, clique na execução mais recente de **Build NANTAO mods** (precisa estar verde ✅).
2. Em **Artifacts**, baixe **TodosOsMods-com-instalador** e extraia o `.zip` inteiro.
3. Com o jogo **fechado**, clique duas vezes em **`Instalar.bat`**.
   Ele acha o perfil *Default* do Thunderstore Mod Manager/r2modman, faz backup, copia os 4 plugins
   e liga `HideManagerGameObject = true` no `BepInEx.cfg` (necessário para o chinelo e o carrinho).
4. Abra o jogo pelo gerenciador (**Start modded**).

Os outros três artifacts (**NantaoAgents**, **ChineloComCriativo**, **CarrinhoCriativo**) são pacotes separados para o r2modman (**Settings → Import local mod**).

## Mapa de teclas (sem conflitos)

| Tecla | Mod | Ação |
|---|---|---|
| X | NantaoAgents | Habilidade do personagem |
| F1 | NantaoAgents | Menu de personagens |
| F9 | NantaoAgents | Recarregar config |
| Clique esquerdo / G / E | Chinelo | Arremessar / largar / pegar |
| F6 / F7 | Criativo | Entregar chinelo / liga-desliga criativo |
| F5 / E / R / T / V | Carrinho | Chamar / empurrar / guardar / descarregar / passageiro |
| F10 | Susto | Dispara um susto de teste |
| F8 | FPSAdaptativo (instalado separado) | — |

> ⚠️ **Modo criativo:** com ele ligado, o host usa o save de teste **ChineloCriativoTeste**. Para uma partida de verdade com os amigos, aperte **F7** (ou `Criativo / Ativado = false`) **antes** de hospedar.

---

# NantaoAgents

10 personagens com habilidades. Você escolhe um no começo, **cada personagem só pode ser de um jogador**, e quem preferir pode ir sem personagem.

Nomes, visuais e sons foram **feitos do zero**, só inspirados em habilidades de jogos de tiro tático, sem nada extraído de outro jogo. Os nomes podem ser trocados na config.

## Controles

| Tecla | O que faz |
|---|---|
| **F1** | Abre/fecha o menu de personagens (só com a nave em órbita) |
| **X** | Usa a habilidade do seu personagem |
| **F9** | Recarrega a config com o jogo aberto |

O menu abre sozinho quando você entra na nave sem personagem. Clique numa carta para escolher. As cartas mostram quem já pegou cada personagem.

## Os 10 personagens

| # | Nome | Habilidade | Limite |
|---|---|---|---|
| 1 | **Faísca** | **X** liga/desliga a corrida elétrica: braços balançando com raios nas mãos, mais velocidade | Energia: 8 s, recarrega em 10 s |
| 2 | **Âncora** | **X** crava a âncora no chão. **X** de novo: volta pra ela. **Segurar X**: recolhe | Alcance 60 m · recarga 25 s |
| 3 | **Aurora** | **Segure X** perto do corpo de um amigo para reviver (fique parado) | 1 por rodada |
| 4 | **Breu** | **X**: some nas sombras e reaparece no ponto onde você mira | Até 18 m · recarga 12 s |
| 5 | **Névoa** | **X** manda uma fumaça onde você mira. Bloqueia a visão de jogadores **e de monstros** | 2 cargas · 30 s cada |
| 6 | **Brasa** | **X** arremessa um coquetel de fogo: queima monstros e **cura você** dentro do seu fogo | Recarga 25 s |
| 7 | **Uivo** | **X** solta um lobo de energia que corre pelo chão e dá o bote em monstros, atordoando. **X** de novo: bote na hora | Recarga 30 s |
| 8 | **Fenda** | **X** lança uma fenda que desliza pelo chão. **X** de novo: teleporta até ela | Recarga 25 s |
| 9 | **Salva** | **X** joga 3 bombinhas no chão onde você mira; elas piscam e explodem (explosão do próprio jogo) | 2 por rodada |
| 10 | **Clarão** | **X** arremessa uma granada de luz (a do próprio jogo): cega quem olhar e atordoa monstros | Recarga 20 s |

Todos os efeitos (raios, âncora, fogo, lobo, fumaça etc.) **aparecem para todo mundo na lobby**.

> ⚠️ **Todos na lobby precisam ter o mod.** As regras (um personagem por jogador, troca só em órbita) valem pela config do **host**.

### Fogo amigo

Por padrão, fogo e bombinhas **não machucam** jogadores. Para ligar: `DanoEmAmigos = true` nas seções `Brasa (fogo)` e `Salva (bombinhas)`.
O **Clarão** usa a granada de luz do jogo, que cega **qualquer um** que olhar, inclusive você. Para que ele só atordoe monstros: `Clarao (granada de luz) / CegaJogadores = false`.

## Instalação

Veja **Instalar tudo de uma vez** no topo. Se você usava a versão antiga (**NeonRun**), desinstale ela: a corrida agora é a Faísca.

## Configuração

Arquivo: `BepInEx/config/nantao.agents.cfg` (aparece depois de abrir o jogo uma vez). Edite, salve e aperte **F9** no jogo.

- **Geral**: teclas, troca durante a rodada, volume, efeitos de tela.
- **Personagens**: o nome exibido de cada uma das 10 vagas.
- Uma seção por personagem: alcance, recarga, dano, cores etc.

Se as mãos da Faísca não aparecem na tela, aumente `Faisca (corrida) / PrimeiraPessoaAltura`.
Se a fumaça da Névoa não aparecer, mude `Nevoa (fumaca) / Modo` para `esferas`.

## Se algo ficar estranho

1. Na config: `Debug / LogDetalhado = true`.
2. Jogue, use as habilidades e feche o jogo.
3. Mande o arquivo `BepInEx/LogOutput.log`.

## Como funciona (técnico)

- **BepInEx + Harmony**: um `PlayerAgent` é adicionado a cada `PlayerControllerB`; cada personagem é uma classe `Ability` em `Nantao/Abilities/`.
- **Rede**: mensagens nomeadas do Netcode. A escolha de personagem passa pelo host, que garante um por jogador e reenvia a lista a cada 2 s (para quem entra depois). Os eventos das habilidades vão do dono para o host e do host para todos.
- **Animação**: pose procedural dos braços (1ª e 3ª pessoa), aplicada por cima da animação do jogo.
- **Reviver**: desfaz o estado de morte do jogador em todos os computadores, com base no que o jogo faz no fim da rodada.
- **Fumaça**: neblina volumétrica do HDRP e/ou esferas translúcidas; um patch nas checagens de visão dos monstros (`EnemyAI.CheckLineOfSight*`) faz a fumaça bloquear.
- **Explosões e clarão**: usam `Landmine.SpawnExplosion` e `StunGrenadeItem.StunExplosion` do próprio jogo.

### Adicionar ou trocar um personagem

1. Crie uma classe em `Nantao/Abilities/` herdando de `Ability`.
2. Registre em `Nantao/Characters.cs`.
3. Adicione as opções em `Nantao/ModConfig.cs`.

### Compilar

```bash
# cada plugin:
dotnet build Nantao/Nantao.csproj -c Release
dotnet build ChineloCriativo/ChineloArremessavel.csproj -c Release
dotnet build ChineloCriativo/ModoCriativoTeste.csproj -c Release
dotnet build CarrinhoCriativo/CarrinhoCriativo.csproj -c Release
# sem acesso ao feed do BepInEx: acrescente -p:OfflineLibDir=/pasta/com/BepInEx.dll/e/0Harmony.dll
```

Testes (rodam fora do jogo): `ChineloCriativo/tests/VerifyFlight.csproj` (59 verificações), `CarrinhoCriativo/tests/VerifyRules.csproj` (196) e `Susto/tests/VerifyScare.csproj` (regras, desenho, sons e suspense).

Tudo compila contra Lethal Company **v81** (`LethalCompany.GameLibs.Steam 81.0.5-ngd.0`); a configuração comum fica em `build/LethalMod.props`.

## Organização do repositório

```
Nantao/              NantaoAgents (código)
ChineloCriativo/     chinelo + modo criativo (código e testes)
CarrinhoCriativo/    carrinho (código e testes)
Susto/               sustos na tela (código e testes)
packages/            manifest, README e ícone de cada pacote Thunderstore
install/             instalador para Windows (Instalar.bat)
modpack/             modpack NantaoTerror (lista de mods e versões)
tools/               scripts do modpack (resolver dependências, gerar .r2z)
build/               configuração de compilação compartilhada
```
