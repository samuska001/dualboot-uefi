# Alterações

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
