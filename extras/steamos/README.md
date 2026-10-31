# Ferramentas opcionais SteamOS

Estas ferramentas não são executadas pelo instalador Windows. Baixe `DualBoot-SteamOS-Tools-0.4.0.zip`, extraia no SteamOS e consulte cada opção. Não há uso de sudo nem envio automático de relatórios.

## Escolher monitor ou TV

O pacote `display-choice` oferece um seletor gráfico de prioridade para a próxima entrada no Modo de Jogo. Consulte [LEIA-ME.md](display-choice/LEIA-ME.md). A troca é feita pelo Desktop; não troca a tela durante um jogo. O perfil reconhecido usa DP-1/HDMI-A-1 e uma versão específica do iniciador SteamOS. Outros perfis precisam de adaptação; uma versão diferente do iniciador é preservada e executada sem o ajuste.

## Diagnóstico somente em leitura

No Konsole, execute na pasta extraída:

```bash
bash collect-display-routing.sh
bash measure-steamos-startup.sh
bash collect-startup-animation.sh
```

Execute apenas o coletor de que precisa. Os scripts gravam relatórios ao lado deles e consultam a sessão, conectores, registros e tempos de inicialização. Não alteram serviços, firmware, boot ou preferências. O coletor de animação também pode copiar até 32 MiB de filmes de início locais para análise; isso não instala nem redistribui esses filmes pelo projeto.

Relatórios podem conter nomes de usuário, identificadores de hardware, caminhos e registros do sistema. Revise-os antes de anexar a uma issue. Nenhum relatório pessoal, filme comercial, kernel ou chave privada faz parte deste pacote.
