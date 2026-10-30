#!/usr/bin/env bash
# Collects the running supported SteamOS slot; never installs or mounts anything.
set -euo pipefail
umask 077
task_tools=(python3 findmnt lsblk)
if [[ "${1:-}" != --verify ]]; then task_tools+=(grub-mkstandalone); fi
for task_tool in "${task_tools[@]}"; do
  command -v "$task_tool" >/dev/null || { printf 'Ferramenta necessaria ausente: %s. Nada foi instalado.\n' "$task_tool" >&2; exit 1; }
done
if [[ $# -ne 1 && ! ( $# -eq 4 && "${1:-}" == --verify ) ]]; then
  printf 'Uso: bash collect-secureboot-steamos.sh /caminho/nova-pasta-coleta\n' >&2
  printf 'Verificar boot: bash collect-secureboot-steamos.sh --verify SESSIONID ROOTGUID /caminho/nova-pasta-relatorio\n' >&2
  exit 1
fi
python3 - "$@" <<'PY'
import hashlib, json, os, pathlib, re, shlex, stat, subprocess, sys, uuid

def fail(message):
    raise RuntimeError(message)

def run(*args):
    return subprocess.check_output(args, text=True, encoding='utf-8').strip()

def regular(path):
    path = pathlib.Path(path)
    for parent in [path] + list(path.parents):
        if parent.is_symlink():
            fail('Link nao permitido: ' + str(parent))
    if not path.is_file() or not stat.S_ISREG(path.stat().st_mode):
        fail('Arquivo regular necessario: ' + str(path))
    return path

def mounted(target):
    info = json.loads(run('findmnt', '--json', '--target', target, '--output', 'TARGET,SOURCE,FSTYPE'))['filesystems']
    if len(info) != 1 or info[0]['target'] != target:
        fail('Ponto de montagem ambiguo: ' + target)
    source = re.sub(r'\[.*\]$', '', info[0]['source'])
    if not source.startswith('/dev/'):
        fail('Origem de montagem nao suportada: ' + target)
    data = json.loads(run('lsblk', '--json', '--nodeps', '--output', 'PATH,TYPE,PARTLABEL,PARTUUID,PKNAME,FSTYPE', source))['blockdevices']
    if len(data) != 1 or data[0]['type'] != 'part':
        fail('Particao real unica necessaria: ' + target)
    p = data[0]
    p['partuuid'] = str(uuid.UUID(p['partuuid']))
    if not p['pkname']:
        fail('Disco pai nao identificado.')
    return p

def destination_path(destination):
    dest = pathlib.Path(destination).absolute()
    for p in [dest] + list(dest.parents):
        if p.is_symlink():
            fail('Destino com link nao permitido.')
    if any(dest == pathlib.Path(p) or pathlib.Path(p) in dest.parents for p in ('/boot', '/efi', '/esp', '/sys', '/proc', '/dev', '/etc', '/usr')):
        fail('Destino em diretorio de sistema/boot nao permitido.')
    if dest.exists():
        fail('Escolha uma pasta nova; o coletor nao sobrescreve arquivos.')
    parent = next(p for p in dest.parents if p.exists())
    source = json.loads(run('findmnt', '--json', '--target', str(parent), '--output', 'SOURCE'))['filesystems']
    if len(source) != 1:
        fail('Montagem do destino ambigua.')
    device = re.sub(r'\[.*\]$', '', source[0]['source'])
    if device.startswith('/dev/'):
        parts = json.loads(run('lsblk', '--json', '--nodeps', '--output', 'PARTTYPE,PARTLABEL', device))['blockdevices']
        if len(parts) != 1:
            fail('Particao destino ambigua.')
        part = parts[0]
        if (part.get('parttype') or '').lower() == 'c12a7328-f81f-11d2-ba4b-00a0c93ec93b' or re.fullmatch(r'(?:esp|efi-[AB])', part.get('partlabel') or '', re.I):
            fail('A coleta nao grava em particoes EFI/ESP.')
    return dest

def collect(destination):
    # os-release is parsed as data, never sourced or executed.
    release = {}
    for line in pathlib.Path('/etc/os-release').read_text().splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        key, sep, val = line.partition('=')
        if not sep or key in release:
            fail('os-release ambiguo.')
        parts = shlex.split(val)
        if len(parts) != 1:
            fail('os-release invalido.')
        release[key] = parts[0]
    if release.get('ID') != 'steamos' or release.get('VARIANT_ID') != 'steamdeck':
        fail('Somente SteamOS instalado VARIANT_ID=steamdeck e suportado; oobe e recusado.')
    if not re.fullmatch(r'3\.9\.\d{1,3}', release.get('VERSION_ID', '')):
        fail('Perfil experimental limitado a SteamOS 3.9.x.')
    # Trigger an existing automount by reading it, without mounting a new partition.
    cfgpath = regular('/efi/EFI/steamos/grub.cfg')
    root, efi = mounted('/'), mounted('/efi')
    if root['fstype'] != 'btrfs' or efi['fstype'] != 'vfat' or root['pkname'] != efi['pkname']:
        fail('Layout rootfs/efi nao suportado.')
    match = re.fullmatch(r'rootfs-([AB])', root['partlabel'] or '')
    if not match or efi['partlabel'] != 'efi-' + match[1]:
        fail('Slot ativo nao identificado inequivocamente.')
    slot = match[1]
    text = cfgpath.read_text(encoding='utf-8')
    kernels = re.findall(r'^\s*steamenv_boot\s+linux\s+(/boot/[A-Za-z0-9._+-]+)\s+([^\r\n]+)$', text, re.M)
    kernels = {(k, ' '.join(a.split())) for k, a in kernels}
    if len(kernels) != 1:
        fail('Entradas kernel diferentes/ausentes: perfil nao suportado.')
    kernel, args = kernels.pop()
    if not re.fullmatch(r'[A-Za-z0-9_./,:=+@%~-]+(?: [A-Za-z0-9_./,:=+@%~-]+)*', args) or len(args) > 4096:
        fail('Argumentos kernel fora da grammar segura.')
    blocked = ('init=', 'rdinit=', 'lockdown=', 'module.sig_enforce=', 'enforcemodulesig=', 'dualboot.session=', 'rd.break')
    if any(t.startswith(blocked) or t in ('single', 'break', '--') for t in args.split()):
        fail('Argumentos de politica nao permitidos.')
    searches = set(re.findall(r'^\s*search --no-floppy --part-uuid --set=root ([0-9a-fA-F-]+)\s*$', text, re.M))
    if {s.lower() for s in searches} != {root['partuuid']}:
        fail('Configuracao GRUB nao corresponde ao rootfs ativo.')
    initrds = {' '.join(s.split()) for s in re.findall(r'^\s*initrd\s+([^\r\n]+)$', text, re.M)}
    if len(initrds) != 1:
        fail('Initramfs ambiguo: perfil nao suportado.')
    initrds = initrds.pop().split()
    if not 1 <= len(initrds) <= 4 or len(set(initrds + [kernel])) != len(initrds) + 1:
        fail('Initramfs duplicados/excessivos.')
    if any(not re.fullmatch(r'/boot/[A-Za-z0-9._+-]+', p) for p in initrds):
        fail('Caminho initramfs nao suportado.')
    modules = pathlib.Path('/usr/lib/grub/x86_64-efi')
    required = ['linux', 'chain', 'normal', 'configfile', 'steamenv', 'fat', 'btrfs', 'part_gpt', 'loadenv', 'search', 'search_part_uuid', 'echo', 'test', 'sleep', 'boot', 'gzio', 'gcry_sha256', 'hashsum', 'tar', 'memdisk']
    optional = ['all_video', 'video', 'efi_gop', 'efi_uga', 'gfxterm', 'terminal', 'serial', 'zstd', 'gcry_sha512']
    selected = required + [m for m in optional if (modules / (m + '.mod')).is_file()]
    for name in selected:
        regular(modules / (name + '.mod'))
    # shim_lock is built into the EFI core on this GRUB profile (not a .mod).
    # Never pass --disable-shim-lock. Physical policy tests remain mandatory.
    dest = destination_path(destination)
    # All source/policy checks above finish before the output folder is created.
    dest.mkdir(mode=0o700)
    (dest / 'files').mkdir(mode=0o700)
    def copy(source, name, original):
        source = regular(source)
        size = source.stat().st_size
        if not 0 < size <= 536870912:
            fail('Arquivo excessivo/vazio: ' + str(source))
        before = source.read_bytes()
        if len(before) != size:
            fail('Arquivo mudou durante coleta.')
        (dest / 'files' / name).write_bytes(before)
        if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(before).digest():
            fail('Arquivo mudou durante coleta.')
        return {'Path': 'files/' + name, 'OriginalPath': original, 'Hash': hashlib.sha256(before).hexdigest(), 'Bytes': len(before)}
    manifest = {'Format': 'DualBootSteamCollectionV1', 'RootPartitionGuid': root['partuuid'], 'EfiPartitionGuid': efi['partuuid'], 'Version': release['VERSION_ID'], 'Slot': slot, 'Variant': release['VARIANT_ID'], 'KernelArguments': args}
    manifest['Kernel'] = copy(kernel, 'kernel.efi', kernel)
    manifest['Initrds'] = [copy(p, 'initrd-%d.img' % n, p) for n, p in enumerate(initrds)]
    manifest['Config'] = copy(cfgpath, 'original-grub.cfg', '/EFI/steamos/grub.cfg')
    seed = dest / 'seed.cfg'
    seed.write_text("echo 'Este core precisa ser reconstruido e assinado pelo instalador Windows.'\nhalt\n", encoding='ascii')
    output = dest / 'files' / 'core.efi'
    subprocess.run(['grub-mkstandalone', '-O', 'x86_64-efi', '-d', str(modules), '-o', str(output), '--modules=' + ' '.join(selected), '--install-modules=' + ' '.join(selected), '--locales=', '--fonts=', 'boot/grub/grub.cfg=' + str(seed)], check=True)
    data = output.read_bytes()
    manifest['Core'] = {'Path': 'files/core.efi', 'OriginalPath': 'generated:grub-core', 'Hash': hashlib.sha256(data).hexdigest(), 'Bytes': len(data)}
    seed.unlink()
    # Recheck all boot inputs immediately before publishing the manifest.
    for record in [manifest['Kernel'], manifest['Config']] + manifest['Initrds']:
        source = cfgpath if record['OriginalPath'] == '/EFI/steamos/grub.cfg' else regular(record['OriginalPath'])
        if hashlib.sha256(source.read_bytes()).hexdigest() != record['Hash']:
            fail('Boot mudou antes da conclusao; descarte esta coleta.')
    (dest / 'collection.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print('Coleta concluida: ' + str(dest))
    print('Volte ao Windows e importe esta pasta. Nenhum boot/firmware foi alterado.')

def verify(session, expected_root, destination):
    if not re.fullmatch(r'[0-9a-f]{32}', session):
        fail('Sessao invalida.')
    expected_root = str(uuid.UUID(expected_root))
    root = mounted('/')
    if root['partuuid'] != expected_root:
        fail('Rootfs ativo diferente do esperado.')
    cmdline = pathlib.Path('/proc/cmdline').read_text().strip().split()
    sessions = [t for t in cmdline if t.startswith('dualboot.session=')]
    images = [t for t in cmdline if t.startswith('BOOT_IMAGE=')]
    bundle = sessions == ['dualboot.session=' + session] and images == ['BOOT_IMAGE=(memdisk)/boot/kernel.efi']
    secure = pathlib.Path('/sys/firmware/efi/efivars/SecureBoot-8be4df61-93ca-11d2-aa0d-00e098032b8c').read_bytes()
    if len(secure) != 5 or secure[4] not in (0, 1):
        fail('Estado Secure Boot nao identificado.')
    lockdown = pathlib.Path('/sys/kernel/security/lockdown').read_text().strip()
    enforced = pathlib.Path('/sys/module/module/parameters/sig_enforce').read_text().strip()
    dest = destination_path(destination)
    report = ['Format=DualBootLinuxStatusV1', 'SessionId=' + session, 'RootPartitionGuid=' + root['partuuid'],
              'SecureBoot=' + str(secure[4]), 'LockdownIntegrity=' + str(int('[integrity]' in lockdown or '[confidentiality]' in lockdown)),
              'ModuleSignaturesEnforced=' + str(int(enforced in ('Y', '1'))), 'BootedSignedBundle=' + str(int(bundle))]
    dest.mkdir(mode=0o700)
    (dest / 'secureboot-linux.txt').write_text('\n'.join(report) + '\n', encoding='ascii')
    print('Relatorio gerado em ' + str(dest / 'secureboot-linux.txt'))

try:
    if sys.argv[1] == '--verify':
        verify(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        collect(sys.argv[1])
except Exception as error:
    print('Coleta bloqueada: ' + str(error), file=sys.stderr)
    sys.exit(1)
PY
