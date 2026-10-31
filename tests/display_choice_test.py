"""Test output routing and safe installation without Linux service/hardware access."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'extras/steamos/display-choice'
spec = importlib.util.spec_from_file_location('telas', SOURCE / 'telas.py')
app = importlib.util.module_from_spec(spec)
spec.loader.exec_module(app)


class ExecCaptured(Exception):
    pass


class Arguments(unittest.TestCase):
    def test_session_arguments_keep_rendering_options(self):
        # Synthetic representative arguments; no personal session dump required.
        arguments = ['--hdr-enabled', '--adaptive-sync', '-W', '1920', '-H', '1080', '-O', '*,eDP-1']
        for choice, preference in (('monitor', 'DP-1,HDMI-A-1,*'), ('tv', 'HDMI-A-1,DP-1,*')):
            self.assertEqual(app.output_arguments(arguments, choice), arguments[:-2] + ['-O', preference])

    def test_child_command_arguments_are_never_rewritten(self):
        args = ['--hdr-enabled', '-O', 'old', '--', 'game', '-O', 'child', '--prefer-output=child']
        self.assertEqual(app.output_arguments(args, 'tv'),
                         ['--hdr-enabled', '-O', 'HDMI-A-1,DP-1,*', '--', 'game', '-O', 'child', '--prefer-output=child'])

    def test_duplicate_and_equals_forms_are_replaced(self):
        args = ['-Oold', '--prefer-output=old', '--prefer-output', 'old', '-w', '1280']
        self.assertEqual(app.output_arguments(args, 'monitor'), ['-w', '1280', '-O', 'DP-1,HDMI-A-1,*'])

    def test_automatic_and_invalid_choices_leave_original_arguments(self):
        for value in ('auto', 'other'):
            self.assertEqual(app.output_arguments(['-O', '*,eDP-1'], value), ['-O', '*,eDP-1'])

    def test_missing_output_value_is_not_applied(self):
        with self.assertRaises(ValueError):
            app.output_arguments(['-O', '--', 'game'], 'monitor')

    def test_original_path_restored_before_real_gamescope(self):
        with tempfile.TemporaryDirectory() as folder:
            paths = app.locations(folder)
            app.save_choice('tv', paths)
            with patch.object(app, 'locations', return_value=paths), \
                 patch.dict(os.environ, {'DUALBOOT_TELAS_ORIGINAL_PATH': '/usr/bin:/bin', 'PATH': '/shim:/usr/bin'}), \
                 patch.object(app.os, 'execv', side_effect=ExecCaptured) as execute:
                with self.assertRaises(ExecCaptured):
                    app.launch_gamescope(['-O', '*,eDP-1'])
                self.assertEqual(os.environ['PATH'], '/usr/bin:/bin')
                self.assertNotIn('DUALBOOT_TELAS_ORIGINAL_PATH', os.environ)
                execute.assert_called_once_with('/usr/bin/gamescope', ['/usr/bin/gamescope', '-O', 'HDMI-A-1,DP-1,*'])

    def test_updated_session_uses_original_without_path_interception(self):
        with patch.object(app, 'supported_session', return_value=False), \
             patch.dict(os.environ, {'PATH': '/original'}), \
             patch.object(app.os, 'execv', side_effect=ExecCaptured) as execute:
            with self.assertRaises(ExecCaptured):
                app.launch_session()
            self.assertEqual(os.environ['PATH'], '/original')
            execute.assert_called_once_with(str(app.SESSION), [str(app.SESSION)])

    def test_supported_session_preserves_original_launcher_and_prepends_shim(self):
        with tempfile.TemporaryDirectory() as folder:
            paths = app.locations(folder)
            paths['shim'].parent.mkdir(parents=True)
            paths['shim'].write_text('# test shim')
            with patch.object(app, 'locations', return_value=paths), \
                 patch.object(app, 'supported_session', return_value=True), \
                 patch.object(app.os, 'access', return_value=True), \
                 patch.dict(os.environ, {'PATH': '/usr/local/bin:/usr/bin'}), \
                 patch.object(app.os, 'execv', side_effect=ExecCaptured) as execute:
                with self.assertRaises(ExecCaptured):
                    app.launch_session()
                self.assertEqual(os.environ['DUALBOOT_TELAS_ORIGINAL_PATH'], '/usr/local/bin:/usr/bin')
                self.assertEqual(os.environ['PATH'].split(os.pathsep)[0], str(paths['shim'].parent))
                execute.assert_called_once_with(str(app.SESSION), [str(app.SESSION)])


class Installation(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.paths = app.locations(Path(self.temp.name) / 'home')
        self.run_count = 0
        self.fail_reload = False

        def run(command):
            if 'daemon-reload' in command:
                if self.fail_reload:
                    self.fail_reload = False
                    raise subprocess.CalledProcessError(1, command)
                return subprocess.CompletedProcess(command, 0, '', '')
            self.run_count += 1
            path = str(app.SESSION) if self.run_count == 1 else str(self.paths['code']) + ' --session'
            return subprocess.CompletedProcess(command, 0, path, '')

        for patcher in (
            patch.object(app, 'locations', return_value=self.paths),
            patch.object(app, 'supported_session', return_value=True),
            patch.object(app, 'REAL_GAMESCOPE', str(SOURCE / 'telas.py')),
            patch.object(app, 'choose_dialog', return_value='monitor'),
            patch.object(app, 'notify'),
            patch.object(app, 'run', side_effect=run),
        ):
            patcher.start()
            self.addCleanup(patcher.stop)
        self.source = Path(self.temp.name) / 'package'
        self.source.mkdir()
        manifest = {'sha256': {}}
        for name in ('telas.py', 'gamescope-shim'):
            content = (SOURCE / name).read_bytes()
            (self.source / name).write_bytes(content)
            manifest['sha256'][name] = hashlib.sha256(content).hexdigest()
        (self.source / 'pacote.json').write_text(json.dumps(manifest))

    def test_install_and_uninstall_change_only_user_hook(self):
        app.install(self.source)
        self.assertEqual(app.read_choice(self.paths['choice']), 'monitor')
        self.assertEqual(self.paths['unit'].read_bytes(), app.unit_content(self.paths))
        self.assertTrue(self.paths['desktop'].exists())
        app.uninstall()
        self.assertFalse(self.paths['unit'].exists())
        self.assertFalse(self.paths['desktop'].exists())

    def test_reload_failure_restores_previous_files(self):
        self.fail_reload = True
        with self.assertRaises(subprocess.CalledProcessError):
            app.install(self.source)
        self.assertFalse(self.paths['unit'].exists())
        self.assertFalse(self.paths['code'].exists())
        self.assertFalse(self.paths['choice'].exists())

    def test_foreign_override_is_preserved(self):
        self.paths['unit'].parent.mkdir(parents=True)
        self.paths['unit'].write_text('[Service]\nExecStart=/other/session\n')
        with self.assertRaisesRegex(RuntimeError, 'nao reconhecido'):
            app.install(self.source)
        self.assertIn('/other/session', self.paths['unit'].read_text())
        self.assertFalse(self.paths['code'].exists())

    def test_damaged_package_is_rejected_before_writes(self):
        (self.source / 'gamescope-shim').write_bytes(b'changed')
        with self.assertRaisesRegex(RuntimeError, 'pacote foi alterado'):
            app.install(self.source)
        self.assertFalse(self.paths['unit'].exists())

    def test_cancel_does_not_install(self):
        with patch.object(app, 'choose_dialog', return_value=None):
            app.install(self.source)
        self.assertFalse(self.paths['unit'].exists())


if __name__ == '__main__':
    unittest.main(verbosity=2)
