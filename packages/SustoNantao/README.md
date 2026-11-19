# SustoNantao

Sustos na tela, do nada, no meio do jogo. **Só quem leva o susto vê a cara**; os amigos perto **ouvem o grito** vindo de você.

## Suspense (antes do susto)
Um **medidor de tensão** escondido sobe quando você está **dentro do prédio, sozinho e no escuro** (lanterna desligada). Perto dos amigos ou fora do prédio, ele cai.
Quanto maior a tensão, mais coisas começam a acontecer — **só pra você** (o amigo do lado não ouve 😈):

| Tensão | O que começa a acontecer |
|---|---|
| baixa | porta rangendo em algum lugar |
| | batidas na parede perto de você |
| média | **passos vindo por trás e chegando mais perto** — param quando você se vira |
| | luzes piscando |
| alta | **risadinhas** ao longe, ecoando no corredor |
| máxima | **sussurros** colados num ouvido |

No auge da tensão, o **susto na tela fica até 4x mais provável**. Depois do susto, a tensão alivia.
Os passos usam o som de passos do próprio jogo; batidas, rangido, sussurro e risadinha são sintetizados pelo mod (originais).

## Tipos de susto (sorteados)
| Tipo | Como é |
|---|---|
| **Rosto** | A cara aparece de uma vez, tremendo e piscando |
| **Investida** | A tela escurece e a criatura **vem correndo do fundo até a sua cara**, abre a boca e grita |
| **Glitch** | Chiado de TV com o rosto piscando no meio, cores trocadas e cortes |
| **Espreita** | A criatura aparece meio apagada no canto, observando… e **pula** pro centro |

As criaturas e o grito são **originais**, gerados pelo mod (4 criaturas, algumas com olhos extras).

## Quando acontece
- Só **dentro do prédio** (nunca na nave, no terminal, no chat ou no menu).
- Chance de **6% por minuto**, com pelo menos **4 minutos** entre um susto e outro e **90 s** dentro do prédio antes do primeiro.
- **F10** dispara um susto na hora (pra testar ou pregar peça em você mesmo).

## Suas imagens e sons
Coloque arquivos em `BepInEx/config/Sustos/` (a pasta é criada no primeiro susto):
- imagens `.png` / `.jpg`
- sons `.wav`

O mod sorteia entre os seus e os padrões (dá pra desligar os padrões na config). Reinicie o jogo depois de adicionar.

## Configuração (`nantao.susto.cfg`)
| Opção | Padrão |
|---|---|
| `Geral / Ativado` | true |
| `Geral / ChancePorMinuto` | 6 % |
| `Geral / IntervaloMinimo` | 240 s |
| `Geral / PrimeiroSustoDepois` | 90 s |
| `Geral / SoDentroDoPredio` | true |
| `Geral / Tipos` | rosto,investida,glitch,espreita |
| `Geral / Duracao` | 0.9 s |
| `Geral / Volume` | 1 |
| `Geral / AmigosOuvemOGrito` | true |
| `Geral / TeclaTeste` | F10 |
| `Midia / UsarRostoPadrao` / `UsarGritoPadrao` | true |
| `Suspense / Ativado` | true |
| `Suspense / MinutosAteTensaoMaxima` | 6 |
| `Suspense / EventosPorMinutoNoMaximo` | 4 |
| `Suspense / Volume` | 0.9 |

Para os amigos ouvirem o grito, todos precisam ter o mod. O susto em si funciona mesmo se só você tiver.
