#!/usr/bin/env bash
# DualBoot-Telas v1
set -eu
task_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
exec /usr/bin/python3 "$task_dir/telas.py" instalar
