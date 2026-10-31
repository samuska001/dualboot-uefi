#!/usr/bin/env bash
# Read-only SteamOS inspection. Writes only copies/reports beside this script.
set -u
umask 077
task_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)" || exit 1
task_out="$task_dir/resultado/$(date -u +%Y%m%d-%H%M%S)"
mkdir -p -- "$task_out" || { printf 'Nao foi possivel gravar o relatorio nesta unidade.\n'; exit 1; }
task_report="$task_out/animacao.txt"
printf 'Coletando registros de video e da animacao. Nenhuma configuracao sera alterada.\n'
{
  printf 'Diagnostico da animacao SteamOS, somente leitura\n'
  date -u '+UTC %Y-%m-%dT%H:%M:%SZ'
  printf '\nSistema e linha de inicializacao\n'
  uname -r
  cat /etc/os-release /proc/cmdline 2>/dev/null
  printf '\nSessao de coleta (Desktop pode diferir do Modo de Jogo)\n'
  printf 'XDG_SESSION_TYPE=%s\nXDG_CURRENT_DESKTOP=%s\n' "${XDG_SESSION_TYPE:-}" "${XDG_CURRENT_DESKTOP:-}"
  printf '\nTempos de boot\n'
  systemd-analyze time 2>&1 || true
  systemctl show display-manager.service plymouth-quit.service plymouth-quit-wait.service \
    -p Id -p ActiveState -p ActiveEnterTimestampMonotonic -p ExecMainStartTimestampMonotonic 2>&1 || true
  printf '\nConectores e modos anunciados pelo monitor (estado atual)\n'
  for task_connector in /sys/class/drm/card*-*; do
    [[ -r "$task_connector/status" ]] || continue
    printf '\n%s\n' "${task_connector##*/}"
    for task_field in status enabled modes; do
      if [[ -r "$task_connector/$task_field" ]]; then
        printf '%s: ' "$task_field"; head -c 4096 -- "$task_connector/$task_field"; printf '\n'
      fi
    done
  done
  printf '\nPacotes graficos instalados\n'
  pacman -Q 2>/dev/null | grep -Ei '^(gamescope|gamescope-session|steam|mesa|vulkan-radeon|linux-neptune|plymouth|sddm)([ -]|$)' || true
  printf '\nEventos do boot: video, splash e inicio da sessao\n'
  journalctl -b --no-pager -o short-monotonic -n 2500 --case-sensitive=no \
    -g 'gamescope|plymouth|sddm|amdgpu.*(init|drm|display|firmware|error|timeout)|drm.*(mode|connector|link|hdr|vrr|framebuffer|error)' 2>&1 || true
  printf '\nEventos da sessao do usuario neste boot\n'
  journalctl --user -b --no-pager -o short-monotonic -n 1500 --case-sensitive=no \
    -g 'gamescope|plymouth|startup.?movie|startup.?video|display.*mode|connector|modeset|hdr|vrr' 2>&1 || true
  if command -v kscreen-doctor >/dev/null 2>&1; then
    printf '\nSaidas do Desktop na hora da coleta\n'; kscreen-doctor -o 2>&1 || true
  fi
  task_steam=''
  for task_candidate in "$HOME/.local/share/Steam" "$HOME/.steam/steam" "$HOME/.steam/root"; do
    if [[ -d "$task_candidate/steamui" ]]; then task_steam="$(readlink -f -- "$task_candidate")"; break; fi
  done
  printf '\nSteam localizada: %s\n' "${task_steam:-nao encontrada}"
  if [[ -n "$task_steam" ]]; then
    printf '\nSomente preferencias relativas a filmes de inicio\n'
    for task_config in "$task_steam/config/config.vdf" "$task_steam"/userdata/*/config/localconfig.vdf; do
      [[ -f "$task_config" ]] || continue
      printf '\n%s\n' "$task_config"
      grep -Ein '"[^"[:cntrl:]]*(startup|movie)[^"[:cntrl:]]*"[[:space:]]' -- "$task_config" | head -n 60 || true
    done
    printf '\nTrechos dos logs Steam relacionados ao video\n'
    for task_log in console_log.txt cef_log.txt bootstrap_log.txt steamui_system.txt webhelper-linux.txt; do
      task_path="$task_steam/logs/$task_log"
      [[ -f "$task_path" ]] || continue
      printf '\n%s\n' "$task_log"
      tail -n 8000 -- "$task_path" | grep -Ei 'startup.?movie|startup.?video|\.webm|plymouth|modeset|display.*mode|video.*(init|decod|fail|error)|vaapi|vulkan|gamescope' | tail -n 160 || true
    done
    printf '\nFilmes de inicio padrao: metadados e copias limitadas\n'
    task_total=0
    for task_name in deck_startup.webm steam_os_startup.webm steam_os_family_startup.webm startup_machine.webm; do
      task_movie="$task_steam/steamui/movies/$task_name"
      [[ -f "$task_movie" ]] || continue
      task_size="$(stat -c %s -- "$task_movie")" || continue
      printf '%s: %s bytes\n' "$task_name" "$task_size"
      sha256sum -- "$task_movie"
      if command -v ffprobe >/dev/null 2>&1; then
        ffprobe -v error -show_entries format=duration:stream=codec_name,codec_type,width,height,r_frame_rate,pix_fmt -of json "$task_movie" 2>&1 || true
      fi
      if (( task_size <= 16777216 && task_total + task_size <= 33554432 )); then
        if cp -- "$task_movie" "$task_out/$task_name"; then task_total=$((task_total + task_size)); fi
      fi
    done
  fi
  printf '\nFim da coleta. Nenhum loader, servico ou configuracao foi escrito.\n'
} >"$task_report" 2>&1
if [[ ! -s "$task_report" ]]; then printf 'Falha ao gravar o relatorio.\n' >&2; exit 1; fi
printf '\nCOLETA CONCLUIDA.\nRelatorio: %s\n' "$task_report"
printf 'Revise os dados pessoais do relatorio antes de compartilhar.\n'
