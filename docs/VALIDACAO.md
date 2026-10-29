# Validação da edição pública

Edição preparada em 29/10/2026.

## Verificação local executada

`scripts/verify.ps1` passou na árvore pública:

- Configuração e entrada: 49 asserções.
- Plataforma: recuperação USB e ciclo de transferência EFI.
- Menu: layout, pixels, proporção, BMPs ausentes e limites de framebuffer.
- Fallback: falha gráfica preserva erro e oferece teclado.
- Compilação C para UEFI x86-64 com Zig 0.15.2.
- QEMU/OVMF: Menu, Profiles, Assets, Invalid, Diagnostic, Chain, Return, Self, SelfAlias, Timer, ModeReturn e Report.

O teste Report salva e relê o relatório em um disco FAT virtual e verifica ausência de conteúdo antigo. O emulador não recebe discos físicos nem rede. `docs/menu.png` é captura do cenário Profiles, com destinos sintéticos.

## Evidência física e limites

O desenvolvimento original foi testado em um PC com Windows e SteamOS em SSDs separados. O usuário confirmou navegação por direcional/analógico, botão A e início dos sistemas com Flydigi Direwolf 4 no modo USB compatível com Xbox 360.

A edição pública usa o mesmo núcleo, com fundo neutro, configuração desativada e documentação portável. O ZIP público final não foi reinicializado fisicamente nem testado em outros PCs. A instalação permanente descrita no manual depende da função Add Boot Option do firmware e não foi validada em todas as placas-mãe.

Secure Boot local e scripts de instalação ligados ao hardware do desenvolvedor não fazem parte deste lançamento. A automação do GitHub verifica compilação, testes nativos e empacotamento; os 12 cenários QEMU foram executados localmente.
