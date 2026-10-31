# Secure Boot — DualBoot Setup 0.4.0 experimental

O assistente prepara cópias EFI assinadas por computador. O cadastro do certificado no MokManager e a ativação na BIOS/UEFI continuam manuais. O EXE Windows não recebeu assinatura comercial: a assinatura dos arquivos EFI é separada.

## Requisitos

Windows 10/11 x64, UEFI x64, .NET Framework 4.8, ESP Windows atual FAT32 e USB FAT32 existente sem EFI/BOOT. Não há formatação. Pacotes SteamOS podem ocupar centenas de MiB. SSD exige criptografia Windows confirmada desligada, Secure Boot ativo após teste USB e nenhum BootNext pendente.

O perfil SteamOS é experimental: 3.9.x, VARIANT_ID=steamdeck, rootfs/efi A ou B no mesmo disco. OOBE, ambiguidades e formatos desconhecidos são recusados. O teste USB confirma a aceitação do shim nas bases de confiança/revogação do firmware de destino. O aplicativo não atualiza bases OEM.

Instalações personalizadas são preservadas e bloqueiam a preparação nesse PC. O fluxo tradicional 0.2.0 continua separado e seus journals continuam reconhecidos; não há migração silenciosa.

## Preparar e instalar

1. Selecione Windows/SteamOS na janela principal e abra **Preparar Secure Boot**.
2. Para SteamOS, exporte o coletor e execute no Linux instalado:

   ```bash
   bash collect-secureboot-steamos.sh /caminho/para/nova-pasta-coleta
   ```

   Exige python3, findmnt, lsblk e grub-mkstandalone já disponíveis. Não monta partições nem instala ferramentas. Use saída fora de /boot, /efi, /esp e diretórios do sistema. Retorne ao Windows com a pasta completa e selecione-a no assistente.

3. Crie a sessão explicitamente. A chave RSA CNG não exportável permanece no perfil criptográfico Windows. Apenas certificado público e arquivos assinados são copiados para USB. Preserve esse perfil para futuras assinaturas.
4. Confira o SHA256 exibido e prepare o USB. A ordem de boot não muda nessa etapa.
5. No MokManager escolha **Enroll key from disk**, selecione `EFI/BOOT/DualBoot.cer`, confira e confirme. Dependendo do firmware, abra `EFI/BOOT/mmx64.efi` pela seleção de arquivo EFI com Secure Boot desligado. O aplicativo não solicita inscrição silenciosa.
6. Ative Secure Boot manualmente na BIOS, preservando bases OEM, e inicialize `EFI/BOOT/BOOTX64.EFI` pelo USB. Se houver recusa do firmware, registre o diagnóstico; não desative a validação do shim para aparentar compatibilidade.
7. Pelo menu USB, inicie Windows. Copie `EFI/BOOT/secureboot-status.txt` e confirme no assistente que Windows iniciou por esse menu. Essa confirmação é um relato do usuário, separado das verificações técnicas.
8. Se SteamOS foi incluído, inicie-o pelo mesmo USB e execute o comando de `ETAPAS.txt` na pasta da sessão:

   ```bash
   bash collect-secureboot-steamos.sh --verify ID_DA_SESSAO GUID_ROOTFS /caminho/nova-pasta-relatorio
   ```

   Importe `secureboot-linux.txt`. Ele confere sessão, rootfs, boot do bundle, Secure Boot, lockdown integrity e módulos obrigatoriamente assinados. Kernel/initramfs/microcódigo ficam no pacote assinado; hashes dos originais são conferidos antes do boot.

9. Retorne ao Windows, retome a sessão e valide relatórios atuais. O aplicativo também consulta o MOK exato e a validação do shim no Windows ativo. Relatórios antigos/incompletos ou de outra sessão não liberam SSD.
10. Instale no SSD quando liberado. As entradas/carregadores originais são preservados. Não há reinício automático. Uma sessão sem SteamOS prepara **somente Windows**.

## Retomar, verificar e restaurar

Sessões/journals ficam em `%ProgramData%\DualBoot\SecureBoot`, restritos a administradores/SYSTEM. O vínculo com o journal é persistido antes da primeira escrita EFI. Se interromper, retome e use **Restaurar USB/SSD**, preservando os registros.

**Verificar instalação** confere arquivos, entradas preservadas, Secure Boot e MOK. A restauração confere propriedade/hashes e remove somente arquivos próprios. Conteúdo alheio ou volume diferente interrompem a operação. O relatório variável `secureboot-status.txt` precisa corresponder à sessão/identidade. Se F2 gerou `diagnostic.txt`, copie-o para fora antes de restaurar: conteúdo não registrado é preservado.

A restauração não apaga MOK, bases OEM ou chave privada Windows. Não use removedor antigo na instalação assinada. PK/KEK/db/dbx, TPM, BCD e BitLocker não são modificados pelo assistente.

## Atualizar SteamOS

Assinatura de futuras versões não é automática. Arquivos de boot diferentes fazem o guardião recusar o pacote antigo; mantenha Windows e entradas originais disponíveis. Use **Preparar atualização** com coleta nova e teste o novo pacote no USB. Depois restaure a instalação assinada gerenciada anterior e instale a nova sessão. A substituição entre sessões não é atômica; se falhar, as entradas originais continuam disponíveis e os journals devem ser preservados. Instalações personalizadas seguem seu procedimento próprio.

## Evidência e limitações

Testados: estados/rollback com firmware simulado e arquivos reais temporários; digest PE/criptografia CMS e adulterações; criação/reabertura/assinatura CNG com identidades LAB removidas depois; OVMF com shim/MOK aceitando três imagens assinadas e recusando unsigned/adulterado/outro certificado; relatório EFI V2 completo e relido; coleta/bundle com fixtures sintéticos e GRUB existente em leitura.

**O fluxo completo deste EXE ainda precisa de teste físico em outro PC:** coleta ao vivo, cadastro MOK, novo bundle SteamOS, instalação/restauração SSD. O boot personalizado do desenvolvedor não foi substituído. Emulação não prova compatibilidade universal.

Procedência/hashes: `assets/secureboot-provenance.json`. Licença: `licenses/shim-COPYRIGHT.txt`. Kernels, módulos e chaves particulares não são distribuídos.
