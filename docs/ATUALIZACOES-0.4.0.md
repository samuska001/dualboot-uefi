# Atualizações de 31/10/2026 — edição pública 0.4.0

## Menu, imagens e desempenho

Integra as revisões locais de foco, estabilidade e resposta: ícones compactos sem cartões, centro fixo, crescimento até 124% em 180 ms, cerca de 70% da mudança nos primeiros 60 ms, desaceleração no final, sem rebote e com continuidade quando a seleção muda durante o movimento.

Alfa ponderado e filtragem bilinear preservam bordas; texturas de 512×512 evitam decodificar imagens grandes repetidamente. O fundo da região central é reutilizado e somente essa região é transferida ao GOP nas animações. A solicitação de quadros é de 16,67 ms; benchmarks CPU e testes não medem o FPS físico do firmware. A varredura USB usa um prazo de 2 segundos, compatível com o novo relógio de quadros.

O código aceita fundos até 4K, mas a distribuição usa o fundo neutro e logos oficiais creditados em THIRD_PARTY.md. Fundos de jogos, imagens particulares e caminhos de Downloads do desenvolvedor continuam locais. A personalização continua possível pelos BMPs do tema.

## Inicialização silenciosa com verificação preservada

O construtor público de bundles SteamOS modifica uma cópia do módulo ELF que contém o formato conhecido `%s: OK\n`. A mudança é exclusivamente tornar vazio o primeiro byte dessa string em uma única seção `.rodata` de leitura. Exige ELF64 little-endian, tabela válida, formato único e mensagens de erro conhecidas; rejeita sobreposição com código/dados graváveis. Outros módulos sem esse formato são preservados.

Código de comparação, valores de retorno, relocations e mensagens de erro permanecem idênticos. A cópia recomposta recebe a assinatura local normal do assistente. Os módulos originais no disco e os kernels não são publicados ou modificados em seu local original. Uma imagem já assinada não é editada por esse fluxo.

## Vídeo e diagnóstico SteamOS

O pacote opcional contém:

- `display-choice`: seletor gráfico Monitor/TV/Automático para a próxima sessão, via complemento systemd do usuário. Perfil conhecido: DP-1/HDMI-A-1 e iniciador SteamOS 3.9.5 de hash fixado. Uma mudança do iniciador faz o auxiliar voltar ao original. A troca exige sair ao Desktop; não troca a tela durante um jogo.
- `collect-display-routing.sh`: conectores, sessão gráfica e roteamento observado.
- `measure-steamos-startup.sh`: tempos systemd e registros relevantes, sem desativar serviços.
- `collect-startup-animation.sh`: diagnóstico de vídeo/animação, com cópias locais limitadas para análise.

Esses auxiliares não são executados pelo instalador Windows. Veja `extras/steamos/README.md`. Não são incluídos filmes comerciais, dumps de sistema, resultados de coleta ou instaladores que dependam de hashes/partições pessoais.

## Limites da publicação

O assistente mantém o fluxo Secure Boot 0.3.0, com certificados próprios por PC, cadastro MOK e ativação BIOS manuais. Não distribui chaves privadas nem aplica alterações no computador do desenvolvedor durante esta publicação. O fluxo completo do EXE e o seletor de tela continuam experimentais, sem promessa de compatibilidade física universal.

As revisões de aparência e os experimentos de recuperação específicos do SSD de desenvolvimento não viram um instalador universal por simples cópia. O núcleo portátil, os testes, o silenciamento validado e os auxiliares independentes estão nesta edição; os procedimentos locais dependentes de identidade, mídia ou slots permanecem documentados somente no ambiente de desenvolvimento.
