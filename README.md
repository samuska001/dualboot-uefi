# DualBoot UEFI

Escolha **Windows ou SteamOS antes de o sistema iniciar**, usando teclado ou um controle compatível. Gerenciador UEFI x86-64 escrito em C, com dois cartões visuais, tema por arquivos e diagnóstico USB integrado.

**v0.1.0 experimental.** Testado fisicamente em um PC com Flydigi Direwolf 4 no modo compatível com Xbox 360 (`045E:028E`, interface `FF/5D/01`). Outros PCs, controles, receptores, Bluetooth e outras distribuições Linux precisam de validação própria.

![Menu com Windows selecionado](docs/menu.png)

## Baixar e começar

1. Baixe **DualBoot-0.1.0.zip** na [página de versões](https://github.com/samuska001/dualboot-uefi/releases/tag/v0.1.0). O arquivo “Source code” contém os fontes, sem executáveis.
2. Extraia o ZIP e leia o [manual de instalação e uso](docs/MANUAL.md).
3. Copie a pasta `EFI` para a raiz de um pendrive FAT32 que não tenha `EFI/BOOT`. Não precisa formatar.
4. Inicie pela entrada UEFI do pendrive. A primeira tela é o diagnóstico.
5. Salve o relatório com F2, configure os GUIDs e caminhos do **seu PC** em `EFI/BOOT/dualboot.ini` e teste cada sistema pelo USB.

O pacote não instala Windows/SteamOS, não cria partições e não configura seus destinos sozinho. Os dois destinos vêm desativados e sem contagem automática. A edição pública não é assinada para Secure Boot: não funciona com ele ativado. Veja os requisitos no manual antes de mudar configurações do firmware.

## Controles

| Comando | Ação |
| --- | --- |
| Direcional / analógico esquerdo / setas | Escolher sistema |
| A / Enter | Iniciar seleção |
| B / Esc | Sair ou voltar do diagnóstico |
| W / S | Selecionar Windows / SteamOS |
| D | Abrir diagnóstico |
| M no diagnóstico | Abrir menu |
| F2 no diagnóstico | Salvar `diagnostic.txt` no pendrive |

## Como funciona

O firmware UEFI executa o programa, que desenha o menu diretamente na tela e lê o controle pelo protocolo USB do firmware. Ao confirmar, ele carrega a entrada EFI original do sistema escolhido. `source=firmware` confere GUID, caminho, entrada ativa em BootOrder e arquivo existente; preserva os dados opcionais da entrada original.

O aplicativo não grava BootOrder/BootNext nem altera os carregadores originais. O relatório F2 grava apenas um arquivo ao lado do executável. Para instalação permanente e recuperação, veja o [manual](docs/MANUAL.md#instalação-permanente-opcional).

## Desenvolver

Windows x64 com **PowerShell 7**. Para compilar, não precisa de QEMU:

```powershell
.\scripts\setup-tools.ps1
.\scripts\test.ps1
.\scripts\build.ps1
.\scripts\package.ps1
```

Para a verificação completa, instale [7-Zip](https://www.7-zip.org/) em seu local padrão ou disponibilize `7z.exe` no PATH, depois:

```powershell
.\scripts\setup-tools.ps1 -WithQemu
.\scripts\verify.ps1
```

Zig 0.15.2 compila C para UEFI sem dependências do sistema operacional. Os downloads têm seus hashes conferidos. QEMU/OVMF executa 12 cenários sem rede e sem acesso aos discos físicos. As suítes nativas cobrem configuração, USB, limites do firmware, menu, BMP e fallback. Os resultados da edição pública estão em [docs/VALIDACAO.md](docs/VALIDACAO.md).

Os BMP já vêm prontos. Para reconstruí-los opcionalmente, instale `sharp` com Node.js (`npm install --no-save sharp`) e execute `node scripts/build-theme.cjs`.

## Contribuir

Abra uma [issue](https://github.com/samuska001/dualboot-uefi/issues) com o modelo do PC/placa-mãe, firmware, sistemas e identificação USB do controle. Revise e remova identificadores pessoais de relatórios antes de anexar. Não envie chaves privadas nem backups EFI. Para alterações no código, execute as verificações acima e envie um pull request.

Código sob [licença MIT](LICENSE). Logos e marcas têm seus respectivos titulares; veja [THIRD_PARTY.md](THIRD_PARTY.md). Projeto independente, sem afiliação com Microsoft ou Valve.
