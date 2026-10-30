"""Synthetic collector policy tests; no Linux boot/EFI paths are written."""
import pathlib
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = pathlib.Path(__file__).with_name('collect-secureboot-steamos.sh').read_text(encoding='utf-8')
SOURCE = SCRIPT.split("<<'PY'\n", 1)[1].rsplit('\nPY', 1)[0]
SOURCE = SOURCE.split('\ntry:\n    if sys.argv[1]', 1)[0]
NS = {}
exec(compile(SOURCE, 'collector-embedded.py', 'exec'), NS)


class CollectorTests(unittest.TestCase):
    def test_python_syntax(self):
        compile(SOURCE, 'collector-embedded.py', 'exec')

    def test_oobe_rejected_before_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = pathlib.Path(tmp) / 'collection'
            with patch.object(pathlib.Path, 'read_text', return_value='ID=steamos\nVARIANT_ID=steamdeck-oobe\nVERSION_ID=3.9.5\n'):
                with self.assertRaisesRegex(RuntimeError, 'oobe'):
                    NS['collect'](out)
            self.assertFalse(out.exists())

    def verify_case(self, session_in_kernel, secure=1, enforced='Y', expected_root=None):
        root = '11111111-1111-1111-1111-111111111111'
        session = '0123456789abcdef0123456789abcdef'
        original_read = pathlib.Path.read_text

        def read_text(path, *args, **kwargs):
            if str(path).replace('\\', '/') == '/proc/cmdline':
                return 'BOOT_IMAGE=(memdisk)/boot/kernel.efi dualboot.session=' + session_in_kernel
            if str(path).replace('\\', '/') == '/sys/kernel/security/lockdown':
                return 'none [integrity] confidentiality'
            if str(path).replace('\\', '/') == '/sys/module/module/parameters/sig_enforce':
                return enforced
            return original_read(path, *args, **kwargs)

        with tempfile.TemporaryDirectory() as tmp:
            out = pathlib.Path(tmp) / 'report'
            with patch.dict(NS, mounted=lambda target: {'partuuid': root}, run=lambda *args: '{"filesystems":[{"source":"tmpfs"}]}'), patch.object(pathlib.Path, 'read_text', read_text), patch.object(pathlib.Path, 'read_bytes', return_value=bytes([7, 0, 0, 0, secure])):
                NS['verify'](session, expected_root or root, out)
            return (out / 'secureboot-linux.txt').read_text()

    def test_current_session(self):
        report = self.verify_case('0123456789abcdef0123456789abcdef')
        self.assertIn('BootedSignedBundle=1\n', report)
        self.assertIn('SecureBoot=1\n', report)
        self.assertIn('ModuleSignaturesEnforced=1\n', report)

    def test_old_session(self):
        report = self.verify_case('ffffffffffffffffffffffffffffffff')
        self.assertIn('BootedSignedBundle=0\n', report)

    def test_disabled_policies(self):
        report = self.verify_case('0123456789abcdef0123456789abcdef', secure=0, enforced='N')
        self.assertIn('SecureBoot=0\n', report)
        self.assertIn('ModuleSignaturesEnforced=0\n', report)

    def test_wrong_root(self):
        with self.assertRaisesRegex(RuntimeError, 'diferente'):
            self.verify_case('0123456789abcdef0123456789abcdef', expected_root='22222222-2222-2222-2222-222222222222')

    def test_esp_destination_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = pathlib.Path(tmp) / 'collection'
            def run(*args):
                if args[0] == 'findmnt':
                    return '{"filesystems":[{"source":"/dev/sda1"}]}'
                return '{"blockdevices":[{"parttype":"c12a7328-f81f-11d2-ba4b-00a0c93ec93b","partlabel":"external"}]}'
            with patch.dict(NS, run=run):
                with self.assertRaisesRegex(RuntimeError, 'EFI/ESP'):
                    NS['destination_path'](out)
            self.assertFalse(out.exists())


if __name__ == '__main__':
    unittest.main()
