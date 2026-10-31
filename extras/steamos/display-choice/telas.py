#!/usr/bin/python3
# DualBoot-Telas v1: a per-user output preference for the stock SteamOS session.
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

VERSION = '1'
SESSION = Path('/usr/lib/steamos/gamescope-session')
SESSION_SHA256 = 'e1499b725efd8afcf5693b5dc240d7311d2bbc389b92df1bb9eea8dd5688d23e'
REAL_GAMESCOPE = '/usr/bin/gamescope'
UNIT_NAME = 'gamescope-session.service'
MARKER = '# DualBoot-Telas v1'
CHOICES = {
    'monitor': ('Monitor (DP-1)', 'DP-1,HDMI-A-1,*'),
    'tv': ('TV (HDMI-A-1)', 'HDMI-A-1,DP-1,*'),
    'auto': ('Automatico do SteamOS', None),
}


def locations(home=None):
    home = Path.home() if home is None else Path(home)
    app = home / '.local/share/dualboot-telas'
    return {
        'home': home, 'app': app, 'code': app / 'telas.py',
        'shim': app / 'bin/gamescope',
        'choice': home / '.config/dualboot-telas/escolha',
        'unit': home / '.config/systemd/user/gamescope-session.service.d/90-dualboot-telas.conf',
        'desktop': home / '.local/share/applications/dualboot-telas.desktop',
    }


def read_choice(path):
    try:
        value = path.read_text(encoding='utf-8').strip()
        return value if value in CHOICES else 'auto'
    except (OSError, UnicodeError):
        return 'auto'


def output_arguments(args, choice):
    """Replace only Gamescope output options, leaving child arguments untouched."""
    if choice not in CHOICES or CHOICES[choice][1] is None:
        return list(args)
    result = []
    index = 0
    while index < len(args) and args[index] != '--':
        arg = args[index]
        if arg in ('-O', '--prefer-output'):
            if index + 1 == len(args) or args[index + 1] == '--':
                raise ValueError('Opcao de saida sem valor na sessao instalada')
            index += 2
            continue
        if arg.startswith('--prefer-output=') or (arg.startswith('-O') and len(arg) > 2):
            index += 1
            continue
        result.append(arg)
        index += 1
    return result + ['-O', CHOICES[choice][1]] + list(args[index:])


def supported_session(path=SESSION):
    try:
        return hashlib.sha256(path.read_bytes()).hexdigest() == SESSION_SHA256
    except OSError:
        return False


def launch_session():
    paths = locations()
    # If an OS update changes the stock launcher, use it unchanged until reviewed.
    if supported_session() and paths['shim'].is_file() and os.access(paths['shim'], os.X_OK):
        os.environ['DUALBOOT_TELAS_ORIGINAL_PATH'] = os.environ.get('PATH', '/usr/bin:/bin')
        os.environ['PATH'] = str(paths['shim'].parent) + os.pathsep + os.environ.get('PATH', '/usr/bin:/bin')
        print('DualBoot-Telas: seletor de saida habilitado nesta sessao.', file=sys.stderr, flush=True)
    else:
        print('DualBoot-Telas: sessao alterada ou seletor indisponivel; usando SteamOS original.', file=sys.stderr, flush=True)
    os.execv(str(SESSION), [str(SESSION)])


def launch_gamescope(args):
    original_path = os.environ.pop('DUALBOOT_TELAS_ORIGINAL_PATH', None)
    if original_path is not None:
        os.environ['PATH'] = original_path
    choice = read_choice(locations()['choice'])
    try:
        launch_args = output_arguments(args, choice)
    except ValueError as error:
        print(f'DualBoot-Telas: {error}; usando argumentos originais.', file=sys.stderr, flush=True)
        launch_args = list(args)
    print(f'DualBoot-Telas: preferencia={choice}; saida={CHOICES[choice][1] or "padrao"}', file=sys.stderr, flush=True)
    os.execv(REAL_GAMESCOPE, [REAL_GAMESCOPE, *launch_args])


def run(command):
    return subprocess.run(command, check=True, text=True, encoding='utf-8',
                          errors='replace', stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)


def write_atomic(path, data, mode=0o600):
    # Only known user-owned files are written. Never follow a target symlink.
    if path.is_symlink():
        raise RuntimeError(f'O caminho e um link simbolico: {path}')
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix='.dualboot-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(data)
        os.chmod(temporary, mode)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def desktop_quote(value):
    value = str(value).replace('%', '%%')
    for char in ('\\', '"', '`', '$'):
        value = value.replace(char, '\\' + char)
    return '"' + value + '"'


def desktop_content(paths):
    return ('[Desktop Entry]\nType=Application\nVersion=1.0\n'
            'Name=Escolher tela do SteamOS\n'
            'Comment=Escolher Monitor ou TV para a proxima entrada no Modo de Jogo\n'
            'Icon=preferences-desktop-display\nTerminal=false\nCategories=Settings;\n'
            f'Exec=/usr/bin/python3 {desktop_quote(paths["code"])} escolher\n'
            'X-DualBoot-Telas=1\n').encode('utf-8')


def unit_content(paths):
    # %h is resolved by systemd for this user; do not change the stock script.
    return (MARKER + '\n[Service]\nExecStart=\n'
            'ExecStart=/usr/bin/python3 "%h/.local/share/dualboot-telas/telas.py" --session\n').encode('utf-8')


def choose_dialog():
    command = ['/usr/bin/kdialog', '--title', 'Tela do SteamOS', '--menu',
               'Escolha a tela para a proxima entrada no Modo de Jogo.\n'
               'A preferencia fica salva. Para trocar, volte ao Desktop.']
    for key, (name, _) in CHOICES.items():
        command.extend([key, name])
    try:
        # The chooser stays open until the user chooses or cancels.
        result = subprocess.run(command, text=True, encoding='utf-8',
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    except OSError as error:
        raise RuntimeError('Abra no Modo Desktop, ou use: telas.py monitor / tv / auto') from error
    if result.returncode == 1:
        return None
    if result.returncode or result.stdout.strip() not in CHOICES:
        raise RuntimeError('Nao foi possivel abrir a escolha de tela: ' + result.stderr.strip())
    return result.stdout.strip()


def save_choice(choice, paths):
    if choice not in CHOICES:
        raise ValueError('Escolha invalida')
    write_atomic(paths['choice'], (choice + '\n').encode())


def notify(message):
    print(message)
    try:
        subprocess.run(['/usr/bin/kdialog', '--title', 'Tela do SteamOS', '--msgbox', message], check=False)
    except OSError:
        pass


def installation_payload(source, paths):
    manifest = json.loads((source / 'pacote.json').read_text(encoding='utf-8'))
    payload = {}
    for name, key, mode in (('telas.py', 'code', 0o644), ('gamescope-shim', 'shim', 0o755)):
        content = (source / name).read_bytes()
        if hashlib.sha256(content).hexdigest() != manifest['sha256'][name]:
            raise RuntimeError('O pacote foi alterado: ' + name)
        payload[paths[key]] = (content, mode)
    payload[paths['unit']] = (unit_content(paths), 0o644)
    payload[paths['desktop']] = (desktop_content(paths), 0o755)
    return payload


def install(source):
    if not supported_session():
        raise RuntimeError('O iniciador SteamOS mudou. Execute novamente coletar.sh antes de instalar.')
    if not Path(REAL_GAMESCOPE).is_file():
        raise RuntimeError('Gamescope nao encontrado')
    paths = locations()
    effective = run(['systemctl', '--user', 'show', UNIT_NAME, '-p', 'ExecStart', '--value']).stdout
    if str(SESSION) not in effective and str(paths['code']) not in effective:
        raise RuntimeError('Ja existe outro iniciador de sessao. Nao foi substituido.')
    payload = installation_payload(source, paths)
    # Refuse unrelated files, including a pre-existing unrecognised preference.
    for path in payload:
        if path.is_symlink() or (path.exists() and b'DualBoot-Telas' not in path.read_bytes()):
            raise RuntimeError('Arquivo existente nao reconhecido: ' + str(path))
    if paths['choice'].exists() and read_choice(paths['choice']) == 'auto' and paths['choice'].read_text().strip() != 'auto':
        raise RuntimeError('Preferencia existente nao reconhecida')
    choice = choose_dialog()
    if choice is None:
        print('Instalacao cancelada, sem alteracoes.')
        return 0
    payload[paths['choice']] = ((choice + '\n').encode(), 0o600)
    previous = {path: (path.read_bytes(), path.stat().st_mode & 0o777) if path.exists() else None for path in payload}
    written = []
    try:
        for path, (content, mode) in payload.items():
            write_atomic(path, content, mode)
            written.append(path)
        run(['systemctl', '--user', 'daemon-reload'])
        effective = run(['systemctl', '--user', 'show', UNIT_NAME, '-p', 'ExecStart', '--value']).stdout
        if str(paths['code']) not in effective or '--session' not in effective:
            raise RuntimeError('A sessao nao carregou o seletor; revertendo a instalacao.')
    except Exception:
        for path in reversed(written):
            before = previous[path]
            if before is None:
                path.unlink()
            else:
                write_atomic(path, *before)
        try:
            run(['systemctl', '--user', 'daemon-reload'])
        except Exception:
            pass
        raise
    receipt = {'state': 'InstalledAwaitingGameModeTest', 'version': VERSION,
               'session_sha256': SESSION_SHA256, 'choice': choice,
               'physical_test_confirmed': False, 'system_files_changed': False}
    try:
        write_atomic(source / 'instalacao.json', json.dumps(receipt, indent=2).encode())
    except OSError as error:
        print('Instalado; nao foi possivel salvar o recibo na unidade: ' + str(error))
    notify('Instalado. Escolha salva: ' + CHOICES[choice][0] + '.\n\n'
           'Volte ao Modo de Jogo para aplicar. Nas proximas vezes, use\n'
           'Escolher tela do SteamOS no menu de aplicativos do Desktop.\n\n'
           'O computador nao sera reiniciado por este instalador.')
    return 0


def uninstall():
    paths = locations()
    unit = paths['unit']
    if unit.is_symlink() or (unit.exists() and unit.read_bytes() != unit_content(paths)):
        raise RuntimeError('O arquivo da sessao foi alterado; nao foi removido.')
    previous = unit.read_bytes() if unit.exists() else None
    if previous is not None:
        unit.unlink()
    try:
        run(['systemctl', '--user', 'daemon-reload'])
    except Exception:
        if previous is not None:
            write_atomic(unit, previous, 0o644)
        raise
    # Leave the harmless helper files in place; only remove the hook and menu entry.
    desktop = paths['desktop']
    if desktop.is_file() and not desktop.is_symlink() and desktop.read_bytes() == desktop_content(paths):
        desktop.unlink()
    notify('Selecao original do SteamOS restaurada para a proxima entrada no Modo de Jogo.')
    return 0


def main(args=None):
    args = sys.argv[1:] if args is None else args
    action = args[0] if args else 'escolher'
    if action == '--session':
        launch_session()
    if action == '--gamescope':
        launch_gamescope(args[1:])
    if action == 'instalar':
        return install(Path(__file__).resolve().parent)
    if action == 'desinstalar':
        return uninstall()
    paths = locations()
    if action == 'status':
        print(json.dumps({'choice': read_choice(paths['choice']), 'session_supported': supported_session(),
                          'installed': paths['unit'].is_file()}))
        return 0
    if action in CHOICES or action == 'escolher':
        if not paths['unit'].is_file():
            raise RuntimeError('Instale primeiro: bash instalar.sh')
        choice = choose_dialog() if action == 'escolher' else action
        if choice is not None:
            save_choice(choice, paths)
            notify('Escolha salva: ' + CHOICES[choice][0] + '.\n\n'
                   'Entre novamente no Modo de Jogo para aplicar.\n'
                   'A preferencia sera mantida nos proximos inicios.')
        return 0
    raise ValueError('Use instalar, escolher, monitor, tv, auto, status ou desinstalar.')


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print('ERRO: ' + str(error), file=sys.stderr)
        if '--session' in sys.argv or '--gamescope' in sys.argv:
            raise
        try:
            subprocess.run(['/usr/bin/kdialog', '--title', 'Tela do SteamOS', '--error', str(error)], check=False)
        except OSError:
            pass
        sys.exit(1)
