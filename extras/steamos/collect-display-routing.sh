#!/usr/bin/env bash
# Inspect display routing and the installed session. No sudo or config writes.
set -u
umask 077

task_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)" || exit 1
task_out="$(mktemp -d "$task_dir/resultado-$(date -u +%Y%m%d-%H%M%S)-XXXXXX")" || {
  printf 'Nao foi possivel salvar o diagnostico nesta unidade.\n' >&2
  exit 1
}
task_report="$task_out/telas.txt"

task_read_file() {
  local task_path="$1" task_size
  [[ -f "$task_path" && -r "$task_path" ]] || return 0
  task_size="$(stat -c %s -- "$task_path")" || return 0
  printf '\nARQUIVO: %s (%s bytes)\n' "$task_path" "$task_size"
  if (( task_size > 262144 )); then
    printf 'Omitido: excede limite de leitura.\n'
    return 0
  fi
  cat -- "$task_path"
  printf '\n'
}

printf 'Coletando telas e configuracao da sessao. Aguarde...\n'
{
  printf 'DualBoot: diagnostico de telas, somente leitura\n'
  date -u '+UTC %Y-%m-%dT%H:%M:%SZ'
  cat /etc/os-release 2>/dev/null
  printf '\nKernel: '; uname -r
  printf 'Sessao: %s / %s\n' "${XDG_SESSION_TYPE:-}" "${XDG_CURRENT_DESKTOP:-}"

  printf '\nCONECTORES, ESTADO E MODOS\n'
  for task_connector in /sys/class/drm/card*-*; do
    [[ -r "$task_connector/status" ]] || continue
    printf '\n%s\n' "${task_connector##*/}"
    for task_field in status enabled modes; do
      if [[ -r "$task_connector/$task_field" ]]; then
        printf '%s: ' "$task_field"
        head -c 4096 -- "$task_connector/$task_field"
        printf '\n'
      fi
    done
  done

  if command -v python3 >/dev/null 2>&1; then
    printf '\nMODELOS INFORMADOS PELO EDID (sem numeros de serie)\n'
    python3 - <<'PY'
from pathlib import Path
for path in sorted(Path('/sys/class/drm').glob('card*-*/edid')):
    try:
        with path.open('rb') as stream:
            data = stream.read(128)
        if len(data) != 128 or data[:8] != b'\x00\xff\xff\xff\xff\xff\xff\x00':
            continue
        vendor = int.from_bytes(data[8:10], 'big')
        make = ''.join(chr(64 + ((vendor >> shift) & 31)) for shift in (10, 5, 0))
        models = []
        for offset in (54, 72, 90, 108):
            block = data[offset:offset + 18]
            if block[:5] == b'\x00\x00\x00\xfc\x00':
                models.append(block[5:18].decode('ascii', errors='replace').strip())
        print(f'{path.parent.name}: {make} / {", ".join(models) or "sem nome de modelo"}')
    except OSError as error:
        print(f'{path.parent.name}: {type(error).__name__}')
PY
  fi

  printf '\nFERRAMENTAS DISPONIVEIS\n'
  for task_tool in gamescope gamescopectl gamescope-control xprop kscreen-doctor kdialog zenity python3; do
    command -v "$task_tool" || true
  done
  if command -v gamescope >/dev/null 2>&1; then
    printf '\nVERSAO DO GAMESCOPE\n'; gamescope --version 2>&1 || true
    printf '\nOPCOES DO GAMESCOPE\n'; gamescope --help 2>&1 | head -n 180 || true
  fi
  printf '\nPACOTES DA SESSAO\n'
  pacman -Q 2>/dev/null | grep -Ei '^(gamescope|gamescope-session|steamos|jupiter|steam-jupiter|steamdeck|sddm)([ -]|$)' || true

  printf '\nUNIDADE DA SESSAO DO USUARIO\n'
  systemctl --user cat gamescope-session.service 2>&1 || true
  printf '\nARQUIVOS DA SESSAO INSTALADA\n'
  for task_path in \
    /usr/bin/gamescope-session \
    /usr/lib/steamos/gamescope-session \
    /usr/bin/steamos-session-select \
    /usr/share/wayland-sessions/gamescope.desktop \
    /usr/share/xsessions/gamescope.desktop \
    /etc/gamescope-session/environment \
    /etc/default/gamescope-session; do
    task_read_file "$task_path"
  done

  printf '\nPREFERENCIAS DE SAIDA EM ARQUIVOS DE AMBIENTE\n'
  for task_path in "$HOME"/.config/environment.d/*.conf /etc/environment.d/*.conf; do
    [[ -r "$task_path" && -f "$task_path" ]] || continue
    grep -HEn '^[[:space:]]*(export[[:space:]]+)?(OUTPUT_CONNECTOR|GAMESCOPE_OUTPUT|GAMESCOPE_OUTPUT_NAME|GAMESCOPE_DISPLAY_OUTPUT|STEAM_DISPLAY_OUTPUT)[[:space:]]*=' "$task_path" || true
  done

  printf '\nREGISTROS DE SAIDA NO MODO DE JOGO NESTE BOOT\n'
  journalctl --user -b --no-pager -o short-monotonic -n 180 --case-sensitive=no \
    -g 'drm:.*(connector|selecting mode|preferred output)|edid:|console: gamescope version|Output.*(HDMI|DP-)' 2>&1 || true
  if command -v kscreen-doctor >/dev/null 2>&1; then
    printf '\nSAIDAS NO DESKTOP (configuracao independente do Modo de Jogo)\n'
    kscreen-doctor -o 2>&1 || true
  fi
  printf '\nFim. Nenhuma preferencia de tela ou servico foi alterado.\n'
} >"$task_report" 2>&1

if [[ ! -s "$task_report" ]]; then
  printf 'Falha ao gravar o diagnostico.\n' >&2
  exit 1
fi
printf '\nCOLETA CONCLUIDA.\nRelatorio: %s\n' "$task_report"
printf 'Revise os dados pessoais dos registros antes de compartilhar.\n'
