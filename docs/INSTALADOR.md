# Instalação fácil — DualBoot Setup 0.4.0 experimental

Baixe a edição experimental 0.4.0 na página de releases. O modo tradicional sem assinatura continua disponível; para o novo modo assinado siga SECUREBOOT.md.

1. Abra o EXE obtido da release oficial do projeto.
2. Abra o arquivo e aceite a solicitação normal de administrador do Windows.
3. Confira Windows e SteamOS encontrados, escolha o padrão e o tempo.
4. Clique **Instalar no SSD** e confira o resumo do destino antes de confirmar.
5. Aguarde a mensagem de conclusão. Reinicie quando quiser.

Não precisa editar GUIDs, abrir terminal, montar a partição EFI ou copiar arquivos. O EXE inclui o menu e o tema; não baixa programas e não instala os sistemas operacionais.

## Começar pelo pendrive

Conecte um pendrive que já esteja em FAT32, clique **Verificar novamente**, selecione a mídia e **Preparar pendrive**. Não há formatação. Um USB que já tenha `EFI/BOOT` é recusado para preservar seu conteúdo. O USB preparado abre diretamente o menu; D abre o diagnóstico, F2 salva o relatório e Esc sai. Escolha a entrada UEFI do USB no menu de boot do PC. Nenhuma ordem de boot é alterada ao preparar USB.

## Mudar padrão, tempo ou ordem dos cartões

Abra novamente o mesmo instalador, escolha o registro da instalação na lista de backups, ajuste os controles e clique **Salvar configuração**. Os destinos de sistemas já instalados são mantidos. Para desativar a contagem automática, deixe o tempo em 0.

## Desinstalar / restaurar

Abra o instalador, escolha o registro correto e clique **Restaurar boot anterior**. Ele verifica o estado salvo, restaura a ordem anterior e remove apenas seus próprios arquivos conferidos. Não apaga pastas de outros carregadores. Se detectar arquivos modificados, conteúdo desconhecido ou alterações posteriores na ordem de boot, preserva tudo e explica o motivo.

O botão **Abrir backups** mostra os registros locais em `%ProgramData%\DualBoot\Backups`. Guarde essa pasta e uma cópia do instalador. Registros interrompidos também aparecem para recuperação. Se houver queda de energia, as entradas originais Windows Boot Manager/SteamOS continuam sendo a primeira opção de recuperação pelo menu do firmware. Não há promessa de transação atômica entre arquivos e firmware.

Não apague o arquivo `setup-owner.id` da pasta instalada: ele identifica a propriedade dos arquivos para a restauração. Se esse marcador estiver ausente ou diferente, o assistente preserva a pasta e interrompe a remoção.

## Compatibilidade desta versão

- Windows 10/11 x64, UEFI x64 e .NET Framework 4.8 (normalmente já presente nesses sistemas).
- Windows/SteamOS já instalados e funcionando por suas entradas originais.
- Os destinos precisam ter entradas ativas reconhecíveis, em partições EFI GPT internas acessíveis com sistema de arquivos FAT compatível. As escritas do instalador (ESP Windows e pendrive) exigem FAT32; a ESP SteamOS pode ser FAT/FAT16, pois é consultada somente em leitura.
- O SSD automático usa somente a partição EFI marcada como sistema pelo Windows em execução. Não instala em outra ESP de Windows por tentativa.
- O fluxo tradicional sem assinatura exige **Secure Boot confirmado desligado**. O novo botão Preparar Secure Boot tem fluxo separado descrito em SECUREBOOT.md, com cadastro MOK e ativação BIOS manuais e teste USB obrigatório.
- Para SSD, a criptografia do Windows deve ser confirmada totalmente desligada. Se o estado estiver ativo ou indisponível, o assistente bloqueia essa operação e não desativa proteções.
- Não substitui uma pasta `EFI/DualBoot` existente sem registro deste assistente. Instalações manuais ou com cadeia Secure Boot personalizada são preservadas. A configuração especial do PC de desenvolvimento não é distribuída.
- Controle validado no desenvolvimento: Flydigi Direwolf 4 no modo Xbox 360 com receptor USB compatível. O teclado continua disponível.

O aplicativo cria uma entrada DualBoot antes das entradas originais e escreve apenas em uma nova pasta própria. Não altera BCD, slots SteamOS, arquivos originais, BitLocker, TPM ou bases de certificados; não reinicia automaticamente.

## Autenticidade e suporte

O EXE ainda não tem assinatura Authenticode de uma autoridade comercial. Confira a origem oficial e compare seu SHA256 com o arquivo publicado. Se o Windows ou antivírus bloquear a execução, não desative a proteção; registre a mensagem para análise.

A versão é experimental. O instalador foi validado com firmware simulado, diretórios temporários, testes de falhas e GUI em demonstração. Seu fluxo real de escrita em SSD não foi executado no PC do desenvolvedor para preservar a instalação Secure Boot personalizada. Testes do gerenciador EFI e compatibilidade física constam de `VALIDACAO.md`.

Para layouts não reconhecidos, use o manual técnico `MANUAL.md` e as entradas originais do firmware. Ao abrir uma issue, descreva o bloqueio mostrado e o hardware; não anexe chaves, backups EFI completos ou registros pessoais sem revisão.
