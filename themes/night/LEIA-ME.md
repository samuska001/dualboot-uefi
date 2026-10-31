# Tema Night — perfis

Menu gráfico sem textos ou cartões, com símbolos reais dos sistemas e expansão centralizada da seleção.

- `background.bmp`: imagem minimalista grafite/azul original, exibida em modo cover, sem distorcer.
- `windows.bmp`: símbolo Windows 11 extraído do material oficial Microsoft.
- `steamos.bmp`: símbolo Steam extraído do SVG do site oficial SteamOS/Valve.
- `theme.ini`: cor de fundo de emergência e cor da contagem opcional (`accent`).

BMP sem compressão, 24/32 bits, até 3840×2160 e 32 MiB por arquivo. O fundo é opaco; logos BGRA32 preservam alfa reto (0 transparente, 255 opaco). Os ícones públicos são pré-filtrados a 512×512. A seleção cresce até 124% em 180 ms, com centro fixo, curva de desaceleração, continuidade em trocas rápidas e bordas filtradas. O temporizador solicita um quadro a cada 16,67 ms; a taxa física depende do firmware. O cache reaproveita o fundo e envia somente a região central durante a animação. A proporção é preservada. Logos ausentes/inválidos abrem o diagnóstico; fundo ausente usa a cor configurada.

A: iniciar; direcional/analógico: escolher; B: sair. Teclado: setas/Enter/Esc; D abre diagnóstico e M volta ao menu. Esses lembretes ficam neste arquivo e no diagnóstico, sem rodapé na tela normal.

Fontes: https://news.microsoft.com/june-24-2021/ e https://store.steampowered.com/steamos/ . Windows é marca da Microsoft; Steam, SteamOS e seu símbolo são marcas da Valve Corporation. Os logos identificam os sistemas locais; o projeto não tem afiliação com essas empresas. Veja THIRD_PARTY.md na raiz do pacote; os PNGs usados na conversão estão em design/profiles/assets no código-fonte.
