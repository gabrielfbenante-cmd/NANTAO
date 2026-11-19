# Modpack NantaoTerror

Perfil pronto para o **r2modman** ou o **Thunderstore Mod Manager**: terror e ambientação, desempenho para PC fraco e o melhor de luas, monstros e itens novos. É **pouca coisa e boa**, não um pacote gigante.

São **34 mods escolhidos** (mais as dependências deles, **73 pacotes** no total, com versões exatas e testadas juntas na v81). A lista com o motivo de cada um está em [`NantaoTerror.roots.txt`](NantaoTerror.roots.txt).

## Como instalar

1. Baixe o **NantaoTerror.r2z** (aba **Actions** do GitHub → execução mais recente → **Artifacts** → **NantaoTerror-modpack**).
2. Abra o **r2modman** ou o **Thunderstore Mod Manager** e escolha o Lethal Company.
3. Na tela de perfis: **Import / Update** → **From file** → escolha o `NantaoTerror.r2z`.
   - Ele cria o perfil **NantaoTerror** e baixa os 73 pacotes sozinho. O seu perfil atual não é alterado.
4. Para colocar os mods NANTAO (personagens, chinelo, carrinho, susto) nesse perfil: no pacote **TodosOsMods-com-instalador**, clique duas vezes em **`Instalar-no-perfil-NantaoTerror.bat`**.
5. **Start modded**. A primeira abertura demora mais, porque os mods geram cache.

> Todos na lobby precisam do **mesmo modpack**. Mande o `.r2z` para os amigos.

## O que tem

### 😱 Terror e ambientação
| Mod | O que faz |
|---|---|
| Mirage | Monstros imitam **a voz dos seus amigos** |
| LETHALRESONANCE | 700+ sons mais assustadores e imersivos |
| SchizophreniaMod | Alucinações sonoras no prédio (pior quando você está sozinho) |
| InsanityMod (v81) | Sanidade: visão em túnel, tremor de tela, vozes. ⚠️ No máximo de insanidade, você **morre** |
| FlickeringFlashlight | Lanterna pisca quando a bateria está fraca |
| ImmersiveVisor | Visor do capacete com rachaduras, sangue, chuva e neblina |
| HalloweenAction | Trilha de ação mais sombria |
| Black Fog Weather | Clima novo: névoa negra |

### ⚡ Desempenho (PC fraco)
| Mod | O que faz |
|---|---|
| **CullFactory** | **Não renderiza as salas que você não está vendo** |
| PathfindingLagFix | IA dos monstros calculada em paralelo |
| LethalPerformance | Cache de buscas que o jogo repete muito |
| LethalSponge | Otimiza renderização e memória |
| ReXuvination | Otimiza colisões |
| ReverbTriggerFix / EntranceTeleportOptimizations | Eco do áudio e portas de entrada mais leves |
| LightsOut | Desliga luzes de itens na nave |
| DissonanceLagFix / CleanerLogs | Menos travadas de voz e de log |

### 🌙 Conteúdo
| Mod | O que adiciona |
|---|---|
| Acidir | Lua: pântano escuro e mortal |
| Infernis | Lua: vulcânica, de fim de jogo |
| Starlancer Moons | 4 luas (grátis, intermediária, difícil e uma escondida) |
| Mimics | Monstro: saídas de incêndio falsas que devoram |
| Locker | Monstro que brinca com a sua percepção |
| Faceless Stalker | O perseguidor sem rosto + páginas para coletar |
| Men Stalker | Espreita em silêncio, depois caça |
| ImmersiveScrap | Sucata nova no estilo do jogo |
| UsualScrap | Itens novos com funções |

### 🎒 Mochila (8 vagas)
| Mod | O que faz |
|---|---|
| HotbarPlus | **6 vagas comuns** (já configurado), teclas 1–6 para trocar |
| ReservedFlashlightSlot | Vaga **só de lanterna** (lado direito da tela). **F** liga/desliga sem pôr na mão |
| ReservedWalkieSlot | Vaga **só de rádio**. **V** fala no rádio sem pôr na mão |
| InputUtils | Menu do jogo para trocar as teclas dos mods (Configurações → Controles) |

As vagas de lanterna e rádio são **grátis** (não precisa comprar no terminal). Para pegar o item da vaga na mão: segure **Alt** e role o mouse.

## Atualizar o modpack (para quem mexe no repositório)

```bash
python3 tools/resolve_modpack.py $(grep -v "^#" modpack/NantaoTerror.roots.txt | cut -d" " -f1)
cp closure.json modpack/NantaoTerror.mods.json
python3 tools/make_r2z.py modpack/NantaoTerror.mods.json NantaoTerror NantaoTerror.r2z
```
