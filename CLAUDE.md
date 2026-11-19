# NANTAO — contexto do projeto (leia antes de mexer)

Mods de **Lethal Company v81** feitos com o usuário (fala português; responda em português, de forma simples).
Este arquivo resume tudo o que foi feito numa sessão anterior na nuvem, para continuar no PC do usuário.

## O que existe

| Pasta | Mod (DLL) | Resumo |
|---|---|---|
| `Nantao/` | NantaoAgents 0.3.0 | 10 personagens (F1 menu, X habilidade, F9 recarrega config), 1 por jogador na lobby; vida 0–100 no canto superior esquerdo; troca a tecla do rádio (ReservedWalkieSlot) de X para V |
| `ChineloCriativo/` | ChineloArremessavel 1.2.0 + ModoCriativoTeste | Chinelo: acertou alguém = empurrão + desmaio (ragdoll); 10% de explosão 5x (visual/som grandes). Criativo F6/F7 (usa save de teste ao hospedar!) |
| `CarrinhoCriativo/` | CarrinhoCriativo 1.2.0 | Carrinho 1.5x, cesto 20 itens, passageiro (C), embalo em descidas. F5/E/R/T |
| `Susto/` | SustoNantao 1.1.0 | Suspense (tensão: passos, batidas, risadinhas, sussurros, lanterna falhando, olhos no escuro) + sustos na tela (rosto, investida, glitch, espreita). F10 testa |
| `modpack/` | NantaoTerror (.r2z) | 34 mods do Thunderstore escolhidos (terror, desempenho, luas/monstros/itens, mochila 6+lanterna+rádio) = 73 pacotes. Lista: `NantaoTerror.roots.txt`; versões: `NantaoTerror.mods.json`; configs prontas: `modpack/config/` |
| `install/` | — | `Instalar.bat` / `Instalar-no-perfil-NantaoTerror.bat` copiam os DLLs para o perfil do Thunderstore Mod Manager/r2modman e ligam `HideManagerGameObject = true` |
| `packages/` | — | manifest/README/ícone de cada pacote Thunderstore |
| `tools/` | — | `resolve_modpack.py` (dependências) e `make_r2z.py` (gera o .r2z) |

Personagens (NantaoAgents), nomes originais e editáveis na config: Faísca (corrida), Âncora (âncora/TP), Aurora (reviver),
Breu (TP curto), Névoa (fumaça), Brasa (fogo), Uivo (lobo), Fenda (fenda/TP), Salva (bombinhas), Clarão (granada de luz).

## Regras combinadas com o usuário
- **Nada copiado** de Valorant, FNAF etc.: nomes, visuais e sons são originais (sintetizados/gerados por código). O usuário pediu cópia idêntica e isso foi recusado; manter.
- "Poucos e bons" no modpack: só mods bem avaliados e atualizados para v81.
- Teclas em uso (evitar conflito): X, F1, F9 (NantaoAgents) · LMB/G/E, F6, F7 (chinelo) · F5/E/R/T/C (carrinho) · F10 (susto) · F (lanterna) · V (rádio) · F8 (FPSAdaptativo do usuário) · 1–6 (HotbarPlus).
- Commits em português; sempre rodar build + testes antes de enviar.

## Compilar e testar (Windows ou Linux)
```bash
dotnet build Nantao/Nantao.csproj -c Release            # idem para os outros .csproj
dotnet build ChineloCriativo/tests/VerifyFlight.csproj -c Release -o out/t1 && dotnet out/t1/VerifyFlight.dll
dotnet build CarrinhoCriativo/tests/VerifyRules.csproj -c Release -o out/t2 && dotnet out/t2/VerifyRules.dll
dotnet build Susto/tests/VerifyScare.csproj -c Release -o out/t3 && dotnet out/t3/VerifyScare.dll
```
Sem acesso ao feed nuget.bepinex.dev: `-p:OfflineLibDir=<pasta com BepInEx.dll e 0Harmony.dll> --source https://api.nuget.org/v3/index.json`.
O GitHub Actions (`.github/workflows/build.yml`) compila tudo, roda os testes e gera os pacotes (artifacts).

No PC do usuário dá para **instalar direto no jogo**: perfil em
`%APPDATA%\Thunderstore Mod Manager\DataFolder\LethalCompany\profiles\<Perfil>\BepInEx\plugins\` (jogo fechado; ver `install/Instalar-no-jogo.ps1`).

## Pendências
1. **FPSAdaptativo** (mod de FPS do usuário, feito com o ChatGPT, fica em `C:/Users/gabri/Documents/Codex/...`): trazer o código pro repositório, comparar com os mods de desempenho do modpack (CullFactory, LethalPerformance, LethalSponge...), evitar conflito e melhorar (ex.: resolução dinâmica, sombras/distância adaptativas). Usa F8.
2. Caprichar efeitos/sons dos personagens que faltam (Aurora, Breu, Névoa, Uivo, Fenda, Salva, Clarão) — Faísca, Âncora e Brasa já foram.
3. Nada foi testado dentro do jogo ainda: testar com amigos e ajustar (força do empurrão do chinelo, câmera do desmaio, embalo do carrinho, posição da vida na tela, revive da Aurora).
4. Ideia em aberto: mais habilidades por personagem (teclas Z/C sugeridas — atenção: C agora é do carrinho).
