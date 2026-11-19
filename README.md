# NANTAO: NeonRun (mod de Lethal Company)

Corrida elétrica inspirada na sensação de "correr com energia nas mãos". Animação, efeitos e sons foram **feitos do zero**, sem nada extraído de outro jogo.

## O que o mod faz

| | Você vê | Seus amigos veem |
|---|---|---|
| Braços balançando com mãos retas | ✅ 1ª pessoa | ✅ no seu personagem |
| Pernas na animação de corrida | ✅ | ✅ |
| Raios, brilho, luz e rastro nas mãos | ✅ | ✅ |
| Zumbido elétrico e estalos | ✅ | ✅ (som 3D) |
| Velocidade extra | ✅ | ✅ (você se move mais rápido) |
| FOV maior, vinheta e linhas de velocidade | ✅ | — (só na sua tela) |
| Barra de energia | ✅ | — |

**Tecla: X** (liga/desliga). A energia dura 8 s e recarrega em 10 s; tudo é configurável.

> ⚠️ **Todo mundo na lobby precisa ter o mod instalado.**

## Como baixar

1. No GitHub, abra a aba **Actions** deste repositório.
2. Clique na execução mais recente de **Build NeonRun** (precisa estar verde ✅).
3. Lá embaixo, em **Artifacts**, baixe **NeonRun**. Vem um `.zip`.

## Como instalar (jeito fácil: r2modman / Thunderstore Mod Manager)

1. Instale o **r2modman** (ou o Thunderstore Mod Manager) e selecione o Lethal Company.
2. Crie ou escolha um perfil.
3. **Settings → Import local mod** → escolha o `NeonRun.zip` que você baixou.
   - O BepInEx é instalado automaticamente como dependência.
4. Clique em **Start modded**.

### Instalação manual

1. Instale o [BepInExPack do Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) na pasta do jogo.
2. Abra o jogo uma vez e feche (para o BepInEx criar as pastas).
3. Copie `NeonRun.dll` para `Lethal Company/BepInEx/plugins/`.

## Ajustando ao vivo (para ficar do jeito que você quer)

Depois de abrir o jogo uma vez com o mod, aparece o arquivo:

```
BepInEx/config/nantao.neonrun.cfg
```

Edite com o Bloco de Notas, salve e aperte **F8** dentro do jogo. A mudança vale na hora, sem reiniciar.

Os ajustes que mais mudam a sensação:

| Seção | Opção | O que faz |
|---|---|---|
| Animacao | `FrequenciaBalanco` | Velocidade do balanço dos braços |
| Animacao | `AmplitudeBalanco` | Quanto os braços vão pra frente e pra trás |
| Animacao | `PrimeiraPessoaAltura` | **Aumente se as mãos não aparecem na tela** |
| Animacao | `PrimeiraPessoaFrente` | Braços mais projetados pra frente |
| Animacao | `PrimeiraPessoaAbertura` | Braços mais abertos ou mais fechados |
| Animacao | `DedosRetos` | Mão reta (1) ou natural (0) |
| Visual | `CorPrincipal` / `CorSecundaria` | Cores da energia (hex) |
| Visual | `Brilho`, `RaiosPorMao`, `TamanhoRaios` | Intensidade dos raios |
| Visual | `AumentoFOV` | Quanto a visão "abre" correndo |
| Movimento | `VelocidadeExtra` | Velocidade a mais (m/s) |
| Movimento | `DuracaoEnergia` / `TempoRecarga` | Quanto tempo dura e recarrega |
| Controles | `Tecla` | Trocar o X por outra tecla (ex: `<Keyboard>/q`) |

## Se algo ficar estranho

1. Na config, coloque `Debug / LogDetalhado = true`.
2. Abra o jogo, ligue a corrida e feche.
3. Mande o arquivo `BepInEx/LogOutput.log`. Ele mostra quais ossos do braço o mod encontrou e quais animações o jogo tem, e com isso dá para corrigir.

## Como funciona (técnico)

- **BepInEx + Harmony**: o mod se "pendura" no `PlayerControllerB` do jogo.
- **Pose dos braços**: depois da animação normal do jogo, cada osso (ombro → antebraço → mão → dedos) é girado para apontar numa direção calculada a cada frame (balanço em seno). Funciona igual no modelo de 1ª pessoa e no corpo que os outros veem.
- **Rede**: mensagens nomeadas do Netcode (`cliente → host → todos`) avisam quem ligou ou desligou. O estado é reenviado a cada 1 s, assim quem entra depois também vê.
- **Efeitos**: `LineRenderer` (raios), `TrailRenderer` (rastro) e luz HDRP, todos criados por código. Os sons são sintetizados na hora.

### Compilar

```bash
# Normal (feeds nuget.org + nuget.bepinex.dev)
dotnet build NeonRun/NeonRun.csproj -c Release

# Sem acesso ao feed do BepInEx: aponte para uma pasta com BepInEx.dll e 0Harmony.dll
dotnet build NeonRun/NeonRun.csproj -c Release -p:OfflineLibDir=/caminho/libs
```

Compilado contra Lethal Company **v81** (`LethalCompany.GameLibs.Steam 81.0.5-ngd.0`).
