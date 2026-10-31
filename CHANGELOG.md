# Alterações

## 0.4.0 — atualizações de menu e ferramentas SteamOS

- Expansão/contração centralizada em 180 ms, continuidade em trocas rápidas e ausência de rebote/deslocamento vertical.
- Ícones BGRA com alfa, redução com filtragem, cache do fundo e atualização parcial do GOP; alvo de 60 atualizações/s sem prometer FPS físico.
- Verificação USB periódica ajustada ao novo relógio de quadros e manutenção da recuperação gráfica/textual.
- Silenciamento somente do primeiro byte do formato conhecido de sucesso do hashsum em cópias ELF de leitura; código, erros, relocations e verificação preservados antes da nova assinatura.
- Pacote opcional SteamOS com seletor Monitor/TV para perfil conhecido e diagnósticos de tela, animação e inicialização.
- Testes de motion/cache/alfa, de seções ELF e de auxiliares Linux; instruções e release automatizada atualizadas.
- Carregamento explícito do provedor de certificados integrado ao Windows nas operações de assinatura, sem depender do perfil ou da importação automática do PowerShell.

## 0.3.0 — assistente experimental de Secure Boot

- Identidade e assinaturas locais por computador, teste USB, cadastro MOK manual e ativação manual na BIOS.
- Coleta SteamOS, composição de pacote autocontido e instalação/restauração assinadas com verificação de estado.
- Publicada em 30/10/2026. O fluxo completo do EXE continua exigindo validação física em outros PCs.

## 0.2.0 — instalador gráfico experimental

- EXE independente para Windows x64, com menu EFI, tema e manual embutidos; sem downloads durante o uso.
- Detecção de entradas Windows/SteamOS e configuração automática de GUIDs/caminhos.
- Instalação no SSD, preparação de pendrive FAT32, mudança de padrão/tempo/ordem visual e restauração gerenciada.
- Registros locais duráveis, verificação de hashes, recuperação de etapas interrompidas e preservação de alterações externas.
- Bloqueios para Secure Boot, criptografia não compatível, destinos ambíguos e instalações manuais existentes.
- Sem modificação do núcleo EFI 0.1.0, sem distribuição de chaves/cadeias Secure Boot locais e sem reinício automático.
- Testes com firmware simulado e pastas temporárias; GUI em demonstração. Escrita real do instalador no SSD ainda não validada fisicamente.

## 0.1.0 — primeira edição pública experimental

- Menu gráfico UEFI x86-64 para Windows/SteamOS, com controle USB compatível e teclado.
- Diagnóstico, relatório F2, configuração por GUID GPT e preservação dos dados opcionais das entradas originais.
- Tema neutro, destinos inicialmente desativados e sem inicialização automática.
- Pacote USB pronto, manual em português com configuração, instalação permanente opcional e recuperação.
- Fontes, quatro suítes nativas e 12 cenários QEMU/OVMF.
- Não inclui chaves, cadeia local Secure Boot, dados de discos ou instaladores específicos da máquina de desenvolvimento.
