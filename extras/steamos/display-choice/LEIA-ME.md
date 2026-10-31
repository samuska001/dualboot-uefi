# Escolher tela do SteamOS — experimental

Seletor de prioridade entre **Monitor (DP-1)**, **TV (HDMI-A-1)** e **Automático** para a próxima entrada no Modo de Jogo. O perfil foi preparado para SteamOS 3.9.5/Gamescope 3.16.30.1 com iniciador original de hash `e1499b725efd8afcf5693b5dc240d7311d2bbc389b92df1bb9eea8dd5688d23e`.

## Instalar

Extraia o pacote no SteamOS e, pelo Desktop, abra o Konsole nessa pasta:

```bash
bash instalar.sh
```

Escolha a saída na janela. Depois volte normalmente ao Modo de Jogo. O aplicativo **Escolher tela do SteamOS** ficará no menu de aplicativos do Desktop, em Configurações. A escolha vale para as próximas sessões e reinícios. Não é necessário sudo nem há reinício automático.

O seletor modifica somente a prioridade `-O/--prefer-output`; resolução, frequência, HDR, VRR e argumentos dos jogos são preservados. Os outros conectores continuam como alternativas. Ele não detecta de forma confiável que uma TV em standby deva ser ignorada: selecione Monitor quando desejar usá-lo.

## Compatibilidade e desfazer

O perfil pressupõe os nomes DP-1 e HDMI-A-1. Confira seus conectores com `collect-display-routing.sh` antes de instalar. Não foi validado fisicamente em outros PCs. Uma atualização que mude o hash do iniciador faz o auxiliar executar a sessão original sem alterar os argumentos de vídeo.

Escolha **Automático** para usar os argumentos originais na próxima sessão. Para remover o complemento da sessão e o atalho:

```bash
python3 "$HOME/.local/share/dualboot-telas/telas.py" desinstalar
```

A implementação usa apenas arquivos na pasta do usuário e um complemento systemd do usuário. O iniciador original não é editado e o PATH original é restaurado antes de executar o Gamescope real. A desinstalação recusa conflitos para preservar alterações externas. Os testes cobrem instalação, reversão, cancelamento, argumentos, PATH e pacote adulterado; a mudança física de imagem ainda depende de teste no seu PC.
