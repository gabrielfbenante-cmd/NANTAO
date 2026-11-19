# CarrinhoCriativo

Carrinho de supermercado **grande** (1,5x o tamanho original), com cesto para **20 itens** e lugar para **1 passageiro em cima**. Quem simula o carrinho é o host.

- **Descidas:** solto, o carrinho **rola sozinho ladeira abaixo** e vai pegando velocidade (numa subida ele volta). Empurrando numa descida, dá pra ir bem mais rápido. Soltando no embalo (E), ele continua rolando. Em chão quase reto ele fica parado.
- **Passageiro:** aperte **C** perto do carrinho para subir. Dá pra ir em cima enquanto alguém empurra, ou descer a ladeira de carona.
- **Itens:** com um item na mão, perto do carrinho, aperte **R**. Os itens se empilham em camadas dentro do cesto.

| Tecla | Ação |
|---|---|
| F5 | Chamar o carrinho (host) |
| E | Empurrar / soltar (olhando para o carrinho) |
| R | Guardar no cesto o item da mão |
| T | Descarregar o cesto |
| C | Subir / descer (passageiro) — configurável em `Teclas / Passageiro` (o V ficou para o rádio) |

## Configuração (`gabri.carrinhocriativo.cfg`)

| Seção | Opção | Padrão | O que faz |
|---|---|---|---|
| Carrinho | Tamanho | 1.5 | Tamanho (1 = original, até 2.5) |
| Carrinho | CapacidadeCesto | 20 | Itens no cesto (até 40) |
| Carrinho | VelocidadeMaxima | 4 | Velocidade empurrando no plano |
| Descida | Impulso | 1.6 | Quanto acelera nas descidas (0 = desliga) |
| Descida | AtritoRodas | 0.35 | Atrito solto (menor = rola mais longe) |
| Descida | VelocidadeMaximaRolando | 12 | Velocidade máxima rolando sozinho |

O tamanho e as regras valem pela config do **host**.

- Todos na lobby precisam ter a mesma versão.
- O carrinho **não é salvo** com a nave: descarregue antes de sair.
- Precisa de `HideManagerGameObject = true` em `BepInEx/config/BepInEx.cfg` (o instalador já faz isso).
