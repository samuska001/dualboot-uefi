#!/usr/bin/env bash
# Profiles startup only. Never enables/disables services or writes boot files.
set -u
umask 077
task_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)" || exit 1
task_out="$task_dir/resultado/boot-$(date -u +%Y%m%d-%H%M%S)"
mkdir -p -- "$task_out" || { printf 'Nao foi possivel gravar nesta unidade.\n' >&2; exit 1; }
export SYSTEMD_PAGER=cat
export SYSTEMD_COLORS=0
printf 'Medindo a inicializacao deste boot. Nenhum servico ou arquivo de boot sera alterado.\n'
{
  date -u '+UTC %Y-%m-%dT%H:%M:%SZ'
  printf '\nBoot ID\n'; cat /proc/sys/kernel/random/boot_id
  printf '\nKernel\n'; uname -r; cat /proc/cmdline
  printf '\nTempos gerais\n'; systemd-analyze time 2>&1 || true
  printf '\nCadeia critica do sistema\n'
  systemd-analyze critical-chain graphical.target display-manager.service network-online.target 2>&1 || true
  printf '\nServicos por duracao (nao implica que todos bloqueiem o boot)\n'
  systemd-analyze blame --no-pager 2>&1 | head -n 100 || true
  printf '\nCadeia critica da sessao do usuario\n'
  systemd-analyze --user critical-chain gamescope-session.service 2>&1 || true
  printf '\nDuracoes da sessao do usuario\n'
  systemd-analyze --user blame --no-pager 2>&1 | head -n 80 || true
  printf '\nMarcos e dependencias das unidades de entrada\n'
  systemctl show sddm.service display-manager.service graphical.target \
    systemd-udev-settle.service NetworkManager-wait-online.service \
    NetworkManager.service plymouth-quit.service plymouth-quit-wait.service \
    -p Id -p LoadState -p ActiveState -p SubState -p After -p Before \
    -p ExecMainStartTimestampMonotonic -p ExecMainExitTimestampMonotonic \
    -p ActiveEnterTimestampMonotonic -p InactiveExitTimestampMonotonic 2>&1 || true
  printf '\nMarcos da sessao Gamescope (podera estar parada apos entrar no Desktop)\n'
  systemctl --user show gamescope-session.service gamescope-session.target \
    -p Id -p LoadState -p ActiveState -p After -p Before \
    -p ExecMainStartTimestampMonotonic -p ExecMainExitTimestampMonotonic \
    -p ActiveEnterTimestampMonotonic -p InactiveExitTimestampMonotonic 2>&1 || true
  printf '\nUnidades de sessao instaladas\n'
  systemctl --user list-unit-files --type=service --no-pager --no-legend 2>/dev/null | grep -Ei 'gamescope|steam|portal' || true
  printf '\nEventos relevantes de inicializacao neste boot\n'
  journalctl -b --no-pager -o short-monotonic -n 3000 --case-sensitive=no \
    -g 'Starting.*(Gamescope|Display Manager|Plymouth|Network|udev|File System)|Started.*(Gamescope|Display Manager|Network)|Finished.*(Plymouth|Network|udev|File System)|Reached target.*(graphical|network)|gamescope.*version|selecting mode|HDR output|Startup - Steam Client launched|steamui.*ready|startup.?movie|amdgpu.*(initializ|firmware|timeout|error)|drm.*(fb0|initializ|error)' 2>&1 || true
  printf '\nLinha de tempo do kernel referente a disco e video\n'
  journalctl -b -k --no-pager -o short-monotonic -n 2000 --case-sensitive=no \
    -g 'nvme|btrfs|ext4|amdgpu|drm|firmware|timeout|error|fail|clocksource' 2>&1 || true
  printf '\nUnidades do caminho grafico: configuracao atual (somente leitura)\n'
  systemctl cat sddm.service plymouth-quit.service plymouth-quit-wait.service NetworkManager-wait-online.service 2>&1 || true
  systemctl --user cat gamescope-session.service 2>&1 || true
  printf '\nAnimacao selecionada, apenas campos relativos ao filme\n'
  if [[ -r "$HOME/.local/share/Steam/config/config.vdf" ]]; then
    python3 - "$HOME/.local/share/Steam/config/config.vdf" <<'PY'
import re, sys
from pathlib import Path
text = Path(sys.argv[1]).read_text(encoding='utf-8', errors='replace')
match = re.search(r'"StartupMovie"\s*\{([^{}]{0,4096})\}', text, re.I)
if match:
    print(match.group(0))
    if 'steamos-inicio-completo.webm' in match.group(0):
        print('OBSERVACAO: filme com espera de 3,5 segundos selecionado; ele nao acelera o boot.')
else:
    print('Bloco StartupMovie nao encontrado.')
PY
  fi
  printf '\nTempos fornecidos pelo carregador EFI (se legiveis)\n'
  python3 - <<'PY'
from pathlib import Path
for name in ('LoaderTimeInitUSec', 'LoaderTimeExecUSec', 'LoaderInfo'):
    path = Path('/sys/firmware/efi/efivars') / (name + '-4a67b082-0a4c-41cf-b6c7-440b29bb8c4f')
    try:
        data = path.read_bytes()
        if len(data) > 4096:
            raise ValueError('Campo excede limite')
        print(name + '=' + data[4:].decode('utf-16-le').rstrip('\0'))
    except Exception as exc:
        print(name + ': ' + type(exc).__name__)
PY
  printf '\nFim da medicao. Nenhum ajuste aplicado.\n'
} >"$task_out/tempos.txt" 2>&1
systemd-analyze plot >"$task_out/sistema.svg" 2>"$task_out/plot-errors.txt" || true
systemd-analyze --user plot >"$task_out/sessao.svg" 2>>"$task_out/plot-errors.txt" || true
if [[ ! -s "$task_out/tempos.txt" ]]; then printf 'Relatorio nao foi gravado.\n' >&2; exit 1; fi
printf '\nMEDICAO CONCLUIDA.\nPasta: %s\n' "$task_out"
printf 'Os registros ficam nesta unidade; revise os dados pessoais antes de compartilhar.\n'
