# Instalador gráfico DualBoot 0.2.0

O usuário pediu reunir o trabalho existente em um instalador que facilite configurar e instalar o projeto. A abordagem é um único EXE Windows x64, em português, com payload embutido e sem dependências para baixar. A instalação autorizada aqui é a criação/publicação do instalador; não aplicar a instalação ao firmware deste PC durante desenvolvimento.

## Abordagem

Scripts com lançador exigiriam PowerShell e manteriam parte da dificuldade; empacotador genérico sozinho não detectaria entradas EFI ou forneceria reversão. Escolha técnica: C#/.NET Framework 4.8 e WinForms, aproveitando a transação NVRAM C# já testada, com arquivos/firmware separados por interfaces. Este é o desenvolvimento do instalador gráfico proposto na conversa e solicitado pelo usuário.

## Fluxo

Ao abrir, solicitar elevação normal do Windows e verificar o computador em leitura. Mostrar Windows/SteamOS detectados, Secure Boot, criptografia, destino e instalação existente. A pessoa escolhe entradas quando houver ambiguidade, padrão e tempo (0 por padrão). Ações: instalar no SSD, preparar pendrive FAT32 existente, restaurar instalação gerenciada, atualizar configuração gerenciada e abrir manual. Não reiniciar automaticamente.

## Detecção e proteção

Ler BootOrder e opções EFI: somente caminhos HD GPT curtos + FILEPATH válidos, sem atalhos, segmentos relativos ou caracteres de controle. Resolver GUID da partição por WMI Storage, sem escolher discos por posição/nome. Para SSD exigir entrada Windows ativa, ESP GPT FAT32 em disco interno, ambos os destinos escolhidos existentes e válidos, Secure Boot confirmado desligado, criptografia Windows confirmada totalmente desligada, ausência de BootNext pendente e de pasta EFI/DualBoot não gerenciada. SteamOS opcional, sem inventar slot A/B. Secure Boot desconhecido bloqueia escrita.

USB: escolher mídia USB existente FAT32, não boot/sistema, recusar EFI/BOOT existente e não formatar. Configurar automaticamente os destinos selecionados; criar menu direto e preservar executável de diagnóstico separado. Nenhuma NVRAM escrita no modo USB.

## Instalação e reversão

Payload verificado por hashes embutidos; caminhos com lista explícita. Criar somente EFI/DualBoot (SSD) ou EFI/BOOT (USB). Arquivos originais dos sistemas não são alterados. Salvar journal durável e backup da configuração/entradas preservadas antes das escritas; copiar e reler arquivos antes de registrar nova entrada. Verificar snapshot antes de cada transação. Usar Transaction.Prepare/Apply/Rollback já existente para SSD, com journal serializável e dados EFI originais preservados. Criar restauração local no ProgramData com permissões administrativas. Reversão resolve a ESP pelo GUID atual e recusa journal inconsistente, arquivos modificados ou firmware alterado depois da instalação; manter entrada se não conseguir restaurar BootOrder. Não apagar conteúdo desconhecido.

Atualização de configuração exige instalação gerenciada e hashes/firmware atuais válidos. Journal registra anterior/novo antes da gravação e permite recuperação de atualização interrompida. Preservar os destinos validados pelo journal; mudar apenas padrão/tempo/ordem. Nenhuma escrita de firmware na atualização.

## Limites explícitos

Decisões após revisão: limitar SSD à ESP IsSystem do Windows em execução, correspondendo ao Windows cuja criptografia é consultada. Recuperar também journals Prepared/FilesReady/Updating e arquivos próprios ausentes; divergências/arquivos parciais não comprovados são preservados. A leitura local revelou ESP SteamOS com FileSystem FAT: aceitar a família FAT para destinos consultados em leitura, mantendo FAT32 obrigatório em todos os destinos de escrita.

Windows 10/11 x64 com .NET Framework 4.8, UEFI x64. Não distribuir chaves, certificados locais, dumps de firmware ou kernels do desenvolvedor. Não alterar Secure Boot, BitLocker, PK/KEK/db/dbx, BCD ou slots SteamOS. Instalações manuais/personalizadas, inclusive a integração Secure Boot existente neste PC, são identificadas e preservadas, sem migração automática. EXE sem assinatura Authenticode comercial; informar isso no manual. Não prometer compatibilidade física universal.

## Verificação e entrega

Testes nativos em firmware simulado e diretórios temporários: opções EFI malformadas/ambíguas, políticas de bloqueio, descoberta de destinos, instalação, falhas parciais, restore após serialização, conflito posterior, atualização de configuração e payload adulterado. Reusar suítes C e 12 cenários QEMU do gerenciador. Abrir GUI em modo de demonstração sem privilégios/escritas físicas para conferir a interface. O teste real do instalador no SSD não será executado neste PC. Gerar EXE/ZIP/hashes, atualizar README/manual/CI e publicar release experimental 0.2.0 após verificação.
