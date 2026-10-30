# Manual — DualBoot UEFI 0.1.0

**Prefere instalar sem comandos?** Use o [instalador gráfico 0.2.0](INSTALADOR.md). Este documento descreve a alternativa técnica/manual do gerenciador EFI.

## O que você vai instalar

Um pequeno aplicativo que roda no firmware UEFI, antes de Windows ou SteamOS. Ele mostra dois cartões e inicia o carregador original do sistema selecionado. Os sistemas precisam estar instalados e iniciar normalmente pelo menu de boot da placa-mãe.

A primeira instalação recomendada é **no pendrive**. Você pode continuar usando somente o pendrive; removê-lo restaura o funcionamento anterior, desde que não tenha mudado a ordem do firmware. Não há serviço, programa de inicialização ou driver instalado dentro do Windows/Linux.

## Requisitos

- PC com UEFI **x86-64**; não suporta BIOS Legacy/CSM, ARM ou UEFI de 32 bits.
- Windows e SteamOS já instalados em modo UEFI. O cenário físico validado usa SSDs separados; outros layouts não têm validação física neste lançamento.
- Pendrive já em FAT32, com pelo menos 32 MiB livres e sem a pasta `EFI/BOOT`.
- Teclado disponível para diagnóstico e configuração.
- Controle opcional: Flydigi Direwolf 4 no modo Xbox 360 com receptor USB `045E:028E`, interface `FF/5D/01`. Outros dispositivos não têm suporte confirmado. Bluetooth não é suportado.
- **Secure Boot desligado** para os binários públicos desta versão. A cadeia assinada usada em experimentos locais não integra este pacote.

Se seu computador usa BitLocker/Criptografia de Dispositivo, tenha acesso à chave de recuperação antes de mexer no firmware. Não siga este procedimento se não puder recuperá-lo. Não é necessário apagar chaves do firmware, cadastrar certificados ou desativar a criptografia do disco. O pacote não faz essas mudanças.

## 1. Baixar e conferir

Na [release v0.1.0](https://github.com/samuska001/dualboot-uefi/releases/tag/v0.1.0), baixe `DualBoot-0.1.0.zip` e `SHA256SUMS.txt`.

No PowerShell, dentro da pasta do download:

```powershell
Get-FileHash .\DualBoot-0.1.0.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

Compare o hash completo. Extraia o ZIP. Você verá `DualBoot-0.1.0/EFI/BOOT`, este manual e as informações de licença. O ZIP “Source code” do GitHub serve para desenvolver e precisa ser compilado.

## 2. Preparar o pendrive

1. Confira a letra e o conteúdo do pendrive no Explorador.
2. Em Propriedades, confirme que o sistema de arquivos é FAT32.
3. Se já existir `EFI/BOOT`, use outra mídia: não sobrescreva outro carregador.
4. Copie a pasta `EFI` de dentro do pacote para a raiz do pendrive.
5. Confira o resultado: `X:\EFI\BOOT\BOOTX64.EFI`, `DualBoot.efi`, `dualboot.ini` e `themes\night\`.

`X:` é apenas um exemplo; use a letra real. **Não precisa formatar, criar partições ou usar Rufus.** Para preparar uma mídia pelo código-fonte, depois de gerar o pacote, há também `scripts/prepare-usb.ps1 -DriveLetter X`, que verifica USB/FAT32 e recusa sobrescrever `EFI/BOOT`.

## 3. Abrir o diagnóstico

1. Conecte o receptor e ligue o controle, se for usá-lo.
2. Reinicie e abra o menu de boot da placa-mãe. A tecla depende do fabricante; frequentemente F8, F11, F12 ou Esc. Consulte seu manual.
3. Escolha a entrada **UEFI do pendrive**, sem mudar a ordem permanente.
4. O diagnóstico deve aparecer. Mova direcional e analógico e pressione A; os números devem mudar. Teste B por último, pois ele pode sair.
5. Pressione **F2** para salvar `EFI/BOOT/diagnostic.txt` no pendrive. Verifique a indicação de sucesso na tela.
6. Volte ao Windows e abra esse arquivo em um editor de texto.

O relatório contém as entradas de boot, seus caminhos e o **GUID GPT da partição EFI**. Este é o GUID que você precisa; não é o GUID do disco, UUID do sistema de arquivos, letra de unidade ou número `BootXXXX`.

## 4. Configurar os seus sistemas

Abra `EFI/BOOT/dualboot.ini` **do pendrive**. O arquivo inicial vem com GUIDs zerados e `enabled=false`, evitando iniciar destinos de outro computador.

Exemplo ilustrativo de relatório (os GUIDs abaixo são fictícios):

```text
Boot0001 Windows Boot Manager
  \EFI\Microsoft\Boot\bootmgfw.efi
  GPT: 11111111-1111-4111-8111-111111111111  OptionalData bytes=0x0088
Boot0002 SteamOS
  \EFI\steamos\steamcl.efi
  GPT: 22222222-2222-4222-8222-222222222222  OptionalData bytes=0x0000
```

Em cada seção do INI, substitua `partition_guid` pelo GUID da entrada original e `path` pelo caminho **exato** do relatório. Depois habilite a opção com `enabled=true`:

```ini
[menu]
title=DUALBOOT
theme=night
default=windows
order=windows,steamos
timeout=0

[windows]
label=Windows
partition_guid=11111111-1111-4111-8111-111111111111
path=\EFI\Microsoft\Boot\bootmgfw.efi
source=firmware
enabled=true

[steamos]
label=SteamOS
partition_guid=22222222-2222-4222-8222-222222222222
path=\EFI\steamos\steamcl.efi
source=firmware
enabled=true
```

Não copie os GUIDs fictícios. Se só tiver um sistema configurado, deixe o outro com `enabled=false`. Salve como texto **ASCII ou UTF-8 sem BOM**, com nomes e valores somente ASCII. No Windows PowerShell 7, `Set-Content -Encoding ascii` também pode ser usado.

Mantenha `source=firmware`: exige entrada ativa em BootOrder, GUID/caminho correspondentes e arquivo existente, preservando seus dados opcionais. Não escolha um `grubx64.efi` de slot A/B por tentativa. Se não houver uma entrada válida para o sistema, resolva o boot original primeiro.

`source=file` é uma alternativa avançada: usa o arquivo e o GUID configurados sem preservar dados opcionais NVRAM. Sem GUID, procura somente no volume de origem do DualBoot. Não é a configuração recomendada para iniciar os sistemas instalados.

## 5. Testar Windows e SteamOS

Inicie novamente o USB. No diagnóstico, pressione **M** para abrir o menu. Selecione Windows com as setas e confirme com Enter; depois repita o procedimento para SteamOS. Confirme o funcionamento dos dois sistemas e do controle após o início.

O cartão selecionado tem borda e tamanho diferentes. O menu gráfico não tem texto. No diagnóstico você consegue conferir problemas de configuração. Se um carregador retornar ao DualBoot, ele mostra o estado EFI e permite escolher novamente.

Depois de validar, para abrir diretamente o menu, substitua **somente no pendrive** `EFI/BOOT/BOOTX64.EFI` por uma cópia de `EFI/BOOT/DualBoot.efi`. Guarde o `BOOTX64.EFI` de diagnóstico do ZIP no computador para recuperação. Não copie este arquivo para uma pasta de outro carregador.

## Configuração e aparência

| Chave | Valores |
| --- | --- |
| `default` | `windows` ou `steamos`: seleção inicial |
| `order` | `windows,steamos` ou `steamos,windows`: ordem dos cartões |
| `timeout` | `0` espera indefinidamente; `1` a `300` segundos para iniciar a seleção padrão |
| `theme` | Nome da pasta em `themes/`, somente letras, números, hífen e sublinhado |

Qualquer interação cancela a contagem naquela sessão. A mantido durante a conexão não deve confirmar sozinho: solte e pressione novamente.

Os arquivos do tema ficam ao lado do executável em `themes/night/`. `theme.ini` aceita cores RGB hexadecimais. `background.bmp` é opcional; `windows.bmp` e `steamos.bmp` são necessários ao menu gráfico. BMP sem compressão, RGB 24/32 bits, até 3840×2160 e 32 MiB por arquivo. Transparência alfa é ignorada. Fundo ausente usa uma cor; logo inválido/ausente abre o diagnóstico. Veja `EFI/BOOT/themes/night/LEIA-ME.md` no pacote.

## Instalação permanente opcional

**Somente depois de testar os dois sistemas por USB.** A distribuição pública não inclui instalador automático de SSD: os scripts da máquina de desenvolvimento dependem de seu hardware e não são portáveis.

O método abaixo exige que seu firmware ofereça **Add Boot Option / adicionar entrada por arquivo**. Se não oferecer, continue pelo pendrive; esta versão não fornece um método alternativo automático.

1. Faça backup da partição EFI e registre a ordem atual de boot. Tenha uma mídia de recuperação e acesso às entradas originais Windows/SteamOS no menu do firmware.
2. No Windows, abra um terminal como administrador. Escolha uma letra **livre**, como `S:`, e monte a ESP do Windows com `mountvol S: /S`. Se houver múltiplas ESPs e você não souber qual foi montada, pare e confira o layout antes de copiar.
3. No PowerShell elevado, veja `Get-ChildItem S:\EFI`. Confirme a presença do carregador original e que `S:\EFI\DualBoot` **ainda não existe**. Não substitua arquivos de `Microsoft`, `Boot`, `steamos` ou outra pasta.
4. Partindo da pasta extraída do pacote, copie apenas para uma pasta nova:

```powershell
if (Test-Path S:\EFI\DualBoot) { throw 'A pasta DualBoot ja existe; preserve a instalacao atual.' }
Copy-Item .\EFI\BOOT S:\EFI\DualBoot -Recurse
```

5. Copie seu `dualboot.ini` **já testado no pendrive** para `S:\EFI\DualBoot\dualboot.ini`. Confira os arquivos e mantenha `timeout=0`.
6. Desmonte com `mountvol S: /D` e reinicie para o setup do firmware.
7. Use Add Boot Option e selecione o SSD/ESP correto e **`\EFI\DualBoot\DualBoot.efi`**. Nomeie a entrada `DualBoot`. Não selecione o `BOOTX64.EFI` do diagnóstico.
8. Teste primeiro essa nova entrada pelo menu de boot. Só depois, se desejar, coloque-a antes das entradas originais na ordem permanente. Preserve as entradas existentes.

Esses passos copiam arquivos à ESP e criam uma nova entrada no firmware. O aplicativo em execução continua sem gravar variáveis de boot. Não modifique BCD, slots SteamOS ou certificados para seguir este manual.

## Recuperar ou desinstalar

**Uso pelo USB:** remova o pendrive e selecione Windows Boot Manager ou SteamOS no menu de boot. Se tiver alterado a ordem permanente, volte à ordem anterior. Você pode manter o USB para novos testes.

**Instalação permanente:** pelo setup do firmware, restaure primeiro a ordem anterior e teste a entrada original de cada sistema. Depois remova somente a entrada criada com o nome `DualBoot`. Remover a entrada pelo firmware já impede sua execução; os arquivos podem ficar na ESP sem efeito. Para liberar espaço, monte a mesma ESP e exclua **somente `EFI/DualBoot`**, após conferir o caminho e guardar sua configuração. Não apague a pasta EFI inteira nem pastas de outros carregadores.

**Não inicia o sistema:** use a entrada original do firmware. Se nem ela funcionar, use a mídia de recuperação do próprio sistema; não tente corrigir apagando partições ou renomeando carregadores.

## Problemas comuns

| Sintoma | O que conferir |
| --- | --- |
| “Security Violation” / execução bloqueada | Este pacote não é assinado para Secure Boot. Não apague chaves nem cadastre certificados desconhecidos. |
| USB não aparece no menu | FAT32, estrutura `EFI/BOOT/BOOTX64.EFI`, opção UEFI e suporte a boot USB no firmware. |
| Menu volta ao diagnóstico | Sintaxe do INI, UTF-8 sem BOM/ASCII e BMPs válidos. |
| Sistema indisponível | `enabled=true`, GUID da partição correto, caminho do relatório, entrada original ativa em BootOrder e arquivo presente. |
| Controle não responde | Modo Xbox 360, identificação/interface USB e receptor conectado antes do boot. Use teclado; Bluetooth não é suportado. |
| F2 não salva | Pendrive gravável, FAT32 e espaço livre. O relatório fica ao lado do executável. |
| SteamOS retorna ao menu | Teste sua entrada original; confira `steamcl.efi` e seus dados originais, sem escolher manualmente um slot A/B. |

Ao relatar problemas, inclua modelo da placa-mãe, versão do firmware, sistemas e VID/PID do controle. Remova identificadores pessoais antes de publicar relatórios. Nunca compartilhe chave BitLocker, chave privada de assinatura ou cópia completa da ESP.
