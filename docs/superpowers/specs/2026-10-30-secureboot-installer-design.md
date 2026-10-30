# Assistente de Secure Boot — proposta para o instalador

Estado: aprovada pelo usuário em 30/10/2026 ("aprovado"). A abordagem integrada de preparação, assinatura e instalação guiadas foi aceita, com decisões técnicas delegadas e sem novas confirmações de planejamento. Implementação em andamento; aprovação não significa funcionalidade já entregue.

## Objetivo

Permitir que outra pessoa prepare uma instalação assinada de Windows/SteamOS usando o aplicativo Windows em português, com etapas claras para registrar seu certificado no MOK e ativar Secure Boot na BIOS. Reaproveitar o fluxo que funcionou no PC de desenvolvimento, removendo caminhos, GUIDs, certificados e hashes particulares.

## Abordagens consideradas

1. **Recomendada: preparação, assinatura e instalação guiadas.** O aplicativo cria uma identidade local, coleta os arquivos do computador de destino, prepara cópias assinadas, faz um teste por USB e permite a instalação gerenciada no SSD após verificar os resultados. É a mudança maior, mas atende ao objetivo de facilitar o processo completo.
2. **Importação de pacote assinado preparado externamente.** Menor implementação, mas exige ferramentas e conhecimento fora do aplicativo. Não atende tão bem à facilidade pedida.

Alterar diretamente as bases OEM da BIOS ou distribuir a chave privada do desenvolvedor não faz parte da proposta. O caminho é shim/MOK, com confirmação física pelo proprietário.

## Experiência proposta

Adicionar ao aplicativo existente uma ação **Preparar Secure Boot**, com andamento persistente:

1. Verificar os sistemas, as partições, Secure Boot, criptografia, espaço, compatibilidade e instalações existentes. Identificar separadamente o estado do Windows, do menu, do MOK e do SteamOS.
2. Exibir um resumo do que será preparado e das etapas manuais. Se a instalação inicial exigir Secure Boot desligado, explicar a ida à BIOS e retomar depois; o aplicativo não altera essa opção.
3. Criar um certificado exclusivo deste computador, com chave privada não exportável no armazenamento criptográfico do Windows e acesso restrito. Reutilizar somente uma identidade própria comprovadamente associada ao journal. Exportar apenas o certificado público para a mídia.
4. Para SteamOS, gerar um coletor Linux em português para ser executado pelo usuário no sistema de destino. Retornar ao Windows com inventário, arquivos necessários e hashes. O instalador valida o formato e prepara cópias assinadas; dados importados não são comandos confiáveis.
5. Preparar um pendrive FAT32 existente, sem formatar nem sobrescrever EFI/BOOT alheio. Incluir shim e MokManager de procedência verificável, menu assinado, certificado público e pacote do SteamOS quando compatível.
6. Orientar o registro do certificado no MokManager, mostrando a impressão digital completa e permitindo cancelamento. A confirmação ocorre na tela de firmware. Não cadastrar chaves silenciosamente.
7. Orientar a ativação manual na BIOS e o teste real dos destinos. Retornar ao aplicativo com diagnóstico vinculado ao pacote preparado. Diferenciar teste de assinatura em emulação de teste físico na máquina do usuário.
8. Habilitar a instalação gerenciada no SSD somente quando as verificações necessárias estiverem completas. Preservar as entradas e os carregadores originais; instalar em diretórios próprios com backup e journal recuperável.
9. Disponibilizar **Verificar instalação**, **Preparar atualização do SteamOS** e **Restaurar instalação anterior**, com regras compatíveis com a cadeia assinada.

O aplicativo deve distinguir estados como incompatível, aguardando coleta, preparado, aguardando MOK, aguardando teste físico, pronto para instalar, instalado e recuperação necessária. Assinatura de arquivo, MOK cadastrado e Secure Boot ativo são verificações distintas. Um relatório ausente, antigo ou incoerente não confirma uma etapa nova.

## Escopo de compatibilidade

Manter Windows x64, UEFI x64 e a base C#/.NET Framework 4.8 existente. O suporte inicial SteamOS é experimental e restrito ao formato de instalação e de carregadores validado pelo projeto. Não declarar suporte universal a distribuições, hardware ou versões futuras.

O perfil compatível precisa ser definido por estrutura, versão e formato dos arquivos reais, não por uma lista de GUIDs do PC de desenvolvimento. Imagens de configuração inicial (`steamdeck-oobe`), layout ambíguo, slots sem evidência e formatos desconhecidos são recusados. Não escolher automaticamente outro slot como recuperação.

Windows pode continuar sendo oferecido sem SteamOS, mas a interface deve informar explicitamente o alcance da preparação. Uma preparação apenas do menu não pode ser apresentada como suporte concluído ao SteamOS.

As ferramentas necessárias ao coletor/construtor devem ser detectadas e documentadas. Dependências do Linux não são instaladas silenciosamente e não se presume WSL configurado. Usar arquivos da máquina do usuário para kernel/initramfs/microcódigo; não distribuir esses arquivos particulares na release.

## Assinatura e cadeia de confiança

- Usar cópia oficial de shim/MokManager com origem, licença, versão e SHA256 fixados no processo de empacotamento. Verificar compatibilidade de arquitetura, SBAT e possíveis revogações; assinatura Microsoft por si só não prova aceitação em todo firmware.
- Assinar o menu EFI e os componentes próprios com a identidade gerada no computador de destino. A chave privada permanece local e não integra logs, ZIPs, pendrives ou Git.
- Conferir a integridade Authenticode/PE e o certificado exato das saídas. Não confundir um certificado autoassinado não confiável pelo Windows com uma assinatura criptograficamente inválida, nem tratar a mera presença de uma assinatura como prova de integridade.
- Manter a validação do shim habilitada. Não desativar lockdown ou a exigência de assinatura para apresentar o boot como compatível.
- O pacote SteamOS precisa proteger a cadeia efetivamente carregada, incluindo kernel, initramfs, microcódigo e módulos aplicáveis. Bloquear arquivos que mudem desde a coleta. A assinatura do menu não substitui essa verificação.
- Não alterar PK, KEK, db, dbx, BCD, TPM ou BitLocker. O MOK é cadastrado pelo proprietário no fluxo do MokManager.

## Transações, manutenção e recuperação

Estender os manifests/journals existentes com versão de esquema explícita para identidade pública, componentes assinados, estado do processo, GUIDs detectados e backups. Não marcar testes físicos anteriores como testes do novo pacote. Manter compatibilidade de leitura/restauração da instalação pública 0.2.0.

Antes de qualquer escrita, conferir hashes, destinos, diretórios sem links, espaço, estado do firmware e ausência de alterações concorrentes. Fazer cópia durável e releitura antes de promover uma entrada de boot. Uma falha após a cópia deve restaurar arquivos e registros coerentes; falhas de recuperação devem manter os backups e um diagnóstico utilizável.

Preservar instalações personalizadas não gerenciadas, inclusive a instalação atual do PC de desenvolvimento. Não remover o bloqueio geral da versão antiga sem substituí-lo por verificações específicas do modo assinado.

Atualização SteamOS continua um processo explícito: nova coleta, detecção de mudança, nova preparação/assinatura e novo teste. Não prometer assinatura automática de futuras versões. O assistente deve impedir reutilizar um pacote antigo como se correspondesse ao kernel recém-atualizado. A restauração não remove certificados MOK ou chaves OEM automaticamente.

## Validação e entrega

Desenvolver no checkout público existente, inicialmente em uma branch própria. Não instalar o novo EXE sobre a instalação personalizada deste PC. Não mudar o firmware físico, gerar uma nova identidade de produção ou reiniciar automaticamente durante o desenvolvimento.

Testes necessários:

- Políticas e estados: Secure Boot ligado/desligado/desconhecido, criptografia desconhecida, instalação personalizada, falta de MOK, incompatibilidade SteamOS e relatórios antigos.
- Identidade e assinatura: certificado exclusivo, validade, chave não exportável, alteração do binário após assinatura e assinante diferente do esperado.
- Importação/coleta: caminhos com escape/links, arquivos excessivos, campos duplicados, hashes divergentes, seleção ambígua de slot e mudança depois da coleta.
- Transações: SSD/USB simulados com arquivos reais temporários, metadados parcialmente gravados, falta de espaço, falha de cópia e restauração em uma sessão posterior.
- OVMF/QEMU: cadeia aceita com a identidade correta e recusada com assinatura ausente, arquivo modificado ou identidade diferente. Verificar assinatura obrigatória, MOK e manutenção das variáveis pertinentes.
- Interface: demonstração das etapas, retomada após reinício, mensagens de bloqueio e restauração. Testes anteriores do instalador e do núcleo EFI continuam passando.

Entregar EXE experimental, pacote, hashes e manual coerentes com os testes executados. Compatibilidade física fora do PC já testado permanece pendente até teste real correspondente; a interface deve mostrar isso. Preparar os artefatos e a proposta de release para revisão antes de publicar uma nova versão.

## Fontes técnicas consultadas

- Código do instalador em `installer/Core.cs`, `Native.cs`, `Deployment.cs`, `MainForm.cs` e `Program.cs`.
- Implementação particular existente em `scripts/prepare-secureboot-integration.ps1`, `sign-secureboot-bundles.ps1`, `build-secureboot-bundles.py` e `prepare-grub-core-linux.sh`, na árvore local original. Referência técnica apenas; não copiar identidades ou caminhos específicos para o código público.
- Ubuntu: https://documentation.ubuntu.com/security/security-features/platform-protections/secure-boot/
- GNU GRUB: https://www.gnu.org/software/grub/manual/grub/html_node/UEFI-secure-boot-and-shim.html

## Aprovação

Revisão técnica independente concluída em 30/10/2026: nenhum ponto importante pendente para planejar a implementação. Nenhum código de produto ou configuração física foi alterado nesta preparação.

O usuário aprovou a proposta integrada, mantendo MOK/BIOS como confirmações manuais. Publicação de uma nova release será submetida à revisão dos artefatos; desenvolvimento e testes locais já estão autorizados.
