# Validação da edição pública

## Instalador 0.2.0

- Compilação do EXE Windows x64 com .NET Framework, sem baixar dependências para o usuário.
- 58 verificações do instalador (28 de parser/política/payload/seleção e 30 de cópia/restore/configuração/falhas), com firmware simulado e pastas temporárias.
- Releitura de payload embutido e hashes, configuração do sistema opcional desativada quando ausente, rejeição de caminhos relativos e registros inconsistentes.
- Leitura local sem elevação confirmou as ESPs e seus volumes; consultas privilegiadas UEFI/BitLocker ficaram desconhecidas e bloqueadas, como esperado.
- GUI renderizada em modo de demonstração, com dados fictícios e ações de escrita desativadas (`docs/installer.png`).
- Não foi executada instalação/desinstalação física pelo EXE: a instalação personalizada Secure Boot existente foi preservada. Emulação e testes de arquivos não substituem validação física do instalador.
- O núcleo EFI permanece 0.1.0 e passa pelas mesmas quatro suítes C e pelos 12 cenários QEMU/OVMF descritos abaixo.
- Revisão independente: corrigidos os casos de destino concorrente não pertencente ao instalador, registro maior do que o limite de leitura, seleção Windows ambígua e metadados pendentes de configuração. Os casos foram cobertos por regressões; limites de NVRAM/FAT32 elevado/perda de energia física continuam sem validação real.

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
