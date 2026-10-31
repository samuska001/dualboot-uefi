# Validação da edição pública

## Edição 0.4.0 — 31/10/2026

- Quatro suítes C aprovadas: configuração, USB/transferência EFI, geometria/alfa/animação/cache e fallback. O teste de foco cobre crescimento inicial rápido e conclusão em 180 ms.
- Os 12 cenários QEMU/OVMF tradicionais passaram com o novo renderizador e tema público transparente.
- Instalador: 28 verificações core, 30 de deployment tradicional, 28 de workflow assinado e 40 de coleta/bundle, além do harness de recuperação WinForms.
- Auxiliares: 13 testes do seletor de saída e sete do coletor Secure Boot, sem serviços ou firmware físicos.
- CNG real: duas identidades descartáveis foram criadas, reabertas, usadas para assinatura e removidas com suas chaves/certificados conferidos. Adulteração e certificado diferente foram recusados. Uma falha do carregamento automático do provedor Cert em Windows PowerShell foi reproduzida e corrigida por importação explícita do módulo de segurança integrado ao Windows.
- OVMF com shim/MOK aceitou os templates novos assinados e recusou entradas sem assinatura, adulteradas ou assinadas por outra identidade; relatório V2 conferido com Secure Boot virtual ativo. Sem rede ou discos físicos.
- O silenciamento ELF é testado para alteração de exatamente um byte da string de sucesso; seções executáveis, sobreposições, formato duplicado e tabela truncada são recusados.
- O EXE e os auxiliares continuam experimentais. Esta publicação não aplica modificações ao boot do desenvolvedor nem comprova o fluxo físico universal do instalador ou do seletor de tela.

## Instalador 0.3.0 — base publicada em 30/10/2026

- EXE x64 .NET Framework 4.8 compilado com recursos verificados: 28 verificações core, 30 deployment tradicionais, 28 workflow/deployment assinado e 34 coleta/bundle no build empacotado.
- Chaves descartáveis CNG: criação não exportável, reabertura exata, assinatura de templates, rejeição de adulteração e certificado diferente. As duas identidades LAB e suas chaves foram removidas; somente CER/EFI públicos ficaram em `build/secureboot-lab`.
- OVMF/shim/MOK: três imagens assinadas aceitas; unsigned, adulterada e assinada por outro certificado recusadas; SecureBoot=1, SetupMode=0, validação shim habilitada. Relatório EFI V2 gerado, relido e extraído da imagem FAT virtual. Evidência local `build/qemu-secureboot-installer/result.json`.
- Testes C do binding/SHA256/MOK passaram. Coletor Linux: sete testes Python sintéticos; o script Bash não foi executado no SteamOS vivo durante este desenvolvimento.
- Interface capturada com `--render-secure-demo`, sem administrador, criação de certificado, varredura ou escrita em discos de boot. O EXE final passou `--self-test`.
- Recuperação: journal vinculado duravelmente à sessão antes de qualquer escrita EFI; falha de metadados/cópia, alteração externa e restauração testadas com arquivos temporários e firmware simulado.
- Harness WinForms confirmou que uma falha com recuperação pendente habilita Restaurar na mesma janela, sem reabrir, com sessão completa e checkbox sincronizados. Revisão final independente não encontrou achados P0/P1/P2 pendentes após a correção.
- A verificação completa do núcleo/build EFI e os 12 cenários QEMU tradicionais passaram, incluindo releitura do relatório FAT pelo fallback pyfatfs. O aviso de depreciação pkg_resources é de uma dependência do leitor de teste; não está no runtime do instalador.
- O novo EXE não foi instalado no SSD físico deste PC. Coleta ao vivo, novo bundle SteamOS, cadastro MOK e instalação/restauração físicos em outro computador continuam pendentes. A instalação particular existente foi preservada.
- O fluxo tradicional permanece compatível com journals 0.2.0. O cenário antigo Report pode usar 7-Zip ou Python/pyfatfs para releitura independente do FAT; não altera o runtime do instalador.

Manual: `SECUREBOOT.md`. A edição 0.3.0 foi publicada em 30/10/2026; os limites físicos descritos continuam aplicáveis.

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
