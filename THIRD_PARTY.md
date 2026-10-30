# Referências e materiais de terceiros

O aplicativo, renderizador e fonte bitmap foram escritos para este projeto. Código e documentação próprios são distribuídos sob MIT. Logos e marcas de terceiros não recebem uma nova licença por este repositório.

- [UEFI 2.10](https://uefi.org/specs/UEFI/2.10/): protocolos e ABI.
- [EDK II](https://github.com/tianocore/edk2): referência dos serviços de boot.
- [UsbXbox360Dxe](https://github.com/chenx-dust/UsbXbox360Dxe): referência de identificação e relatórios Xbox 360; nenhum arquivo ou executável desse driver é incorporado.
- [rEFInd](https://www.rodsbooks.com/refind/): referência de comportamento; nenhum código ou executável incorporado.
- [Zig 0.15.2](https://ziglang.org/download/0.15.2/): compilador baixado separadamente, SHA256 `3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c`.
- [QEMU para Windows](https://qemu.weilnetz.de/w64/): QEMU/OVMF baixados separadamente para testes. Mantêm as licenças próprias; não integram o ZIP público.
- [7-Zip](https://www.7-zip.org/) e [sharp](https://sharp.pixelplumbing.com/): ferramentas opcionais externas, sem redistribuição.

## Tema

O fundo abstrato grafite/azul foi criado para o projeto. A edição pública usa esse fundo neutro; não inclui imagens de jogos.

O símbolo Windows 11 vem do [material de imprensa Microsoft](https://news.microsoft.com/june-24-2021/), [arquivo original](https://msftstories.thesourcemediaassets.com/sites/612/2021/06/Windows-11-Logo-1000x404.png). Windows é marca da Microsoft Corporation.

O símbolo Steam vem do [site oficial SteamOS](https://store.steampowered.com/steamos/), [SVG original](https://store.akamai.steamstatic.com/public/shared/images/header/logo_steam.svg). Steam, SteamOS e seu símbolo pertencem à Valve Corporation.

Os símbolos foram extraídos/rasterizados para identificar os sistemas locais. Não se declara afiliação, patrocínio ou aprovação dessas empresas. Os direitos de uso das marcas continuam sendo dos respectivos titulares.

## shim e MokManager (modo assinado 0.3.0)

Binários públicos Fedora 44 shim-x64 16.1-5, procedência e SHA256 em assets/secureboot-provenance.json. Copyright/licença BSD de upstream preservados em licenses/shim-COPYRIGHT.txt, incluído no pacote. Fonte upstream: https://github.com/rhboot/shim/tree/16.1. Aceitação em firmware/revogações precisa do teste USB na máquina de destino. O pacote não contém kernels ou módulos GRUB privados; esses arquivos são coletados no computador do usuário.
