# Tema Night — perfis

Menu gráfico sem textos, com símbolos reais dos sistemas e foco por borda/tamanho.

- `background.bmp`: imagem minimalista grafite/azul original, exibida em modo cover, sem distorcer.
- `windows.bmp`: símbolo Windows 11 extraído do material oficial Microsoft.
- `steamos.bmp`: símbolo Steam extraído do SVG do site oficial SteamOS/Valve.
- `theme.ini`: cor de fundo de emergência e cor da borda selecionada (`accent`).

BMP RGB sem compressão, 24/32 bits, até 3840×2160 e 32 MiB por arquivo. Alfa é ignorado. Os logos fornecidos têm transparência composta sobre o fundo do cartão `171C25`; para substituir, preparar a imagem com esse fundo. A proporção da imagem é preservada. Logos ausentes/inválidos abrem o diagnóstico; fundo ausente usa a cor configurada.

A: iniciar; direcional/analógico: escolher; B: sair. Teclado: setas/Enter/Esc; D abre diagnóstico e M volta ao menu. Esses lembretes ficam neste arquivo e no diagnóstico, sem rodapé na tela normal.

Fontes: https://news.microsoft.com/june-24-2021/ e https://store.steampowered.com/steamos/ . Windows é marca da Microsoft; Steam, SteamOS e seu símbolo são marcas da Valve Corporation. Os logos identificam os sistemas locais; o projeto não tem afiliação com essas empresas. Veja THIRD_PARTY.md na raiz do pacote; os PNGs usados na conversão estão em design/profiles/assets no código-fonte.
