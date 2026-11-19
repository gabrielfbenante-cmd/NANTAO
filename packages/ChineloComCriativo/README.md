# ChineloComCriativo

Chinelo arremessável que voa reto na direção da mira.

- **Acertou alguém (90%)** → a pessoa é **arremessada longe e desmaia**: o corpo fica mole e cai no chão, a tela dela escurece, e depois de alguns segundos ela **levanta** onde o corpo caiu. O chinelo cai no chão e pode ser pego de novo.
- **10% de chance de explodir** (ao bater ou no fim do alcance) → explosão **5x mais forte** que a original: muito mais dano em monstros e raio maior. **Visual 3x maior**: explosões do jogo ampliadas, bola de fogo, onda de choque no chão, estilhaços, fumaça e clarão na tela. **Estrondo** longo e grave, ouvido de longe. Quem estiver perto voa e desmaia (sem perder vida, por padrão).

Inclui o **Modo Criativo** para testes: o host recebe chinelos de graça.

| Tecla | Ação |
|---|---|
| Clique esquerdo | Arremessar |
| G / E | Largar / pegar |
| F6 | Entregar ou recuperar um chinelo (host, com o criativo ligado) |
| F7 | Ligar ou desligar o criativo |

## Configuração (`gabri.chineloarremessavel.cfg`)

| Seção | Opção | Padrão | O que faz |
|---|---|---|---|
| Porrada | ForcaDoEmpurrao | 22 | Força do empurrão de quem leva a chinelada |
| Porrada | TempoDesmaiado | 4 s | Tempo caído antes de levantar |
| Porrada | LargarItemDaMao | true | Quem desmaia larga o item da mão |
| Explosao | ChanceDeExplodir | 10 % | Chance de explodir em vez de só bater |
| Explosao | Multiplicador | 5 | Quantas vezes mais forte que a explosão original |
| Explosao | Raio | 6 m | Raio do dano em monstros e do arremesso de jogadores |
| Explosao | DanoEmJogadores | 0 | Dano da explosão em jogadores (0 = só arremessa e desmaia) |
| Explosao | TamanhoVisual | 3 | Tamanho do efeito (1 = explosão normal do jogo) |
| Explosao | Volume | 1 | Volume do estrondo (0 a 1) |

As decisões (acertou ou não, explodiu ou não) são do **host**, com a config dele.

- Todos na lobby precisam ter a mesma versão.
- Com o criativo ligado e `UsarSaveDeTeste = true`, o host usa o save separado **ChineloCriativoTeste**. Para uma partida de verdade, desligue o criativo (F7) **antes** de hospedar.
- Precisa de `HideManagerGameObject = true` em `BepInEx/config/BepInEx.cfg` (o instalador já faz isso).
