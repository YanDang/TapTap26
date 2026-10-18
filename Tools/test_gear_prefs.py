"""Exercise actual raw Windows registry backup on an isolated disposable namespace."""
import unittest
import uuid
import winreg
import run_tap_selftest as supervisor


class GearPrefsProtection(unittest.TestCase):
    def test_unknown_gear_keys_types_bytes_and_absence(self):
        old_path = supervisor.REG_PATH
        path = 'Software\\HermesIntegrationTests\\' + uuid.uuid4().hex
        supervisor.REG_PATH = path
        try:
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, path) as key:
                winreg.SetValueEx(key, 'GJ_GEAR_UNKNOWN_BINARY', 0, winreg.REG_BINARY, b'\x00\xff\x10\x00')
                winreg.SetValueEx(key, 'GJ_GEAR_UNKNOWN_INT', 0, winreg.REG_DWORD, 123)
                winreg.SetValueEx(key, 'GJ_GEAR_STATE', 0, winreg.REG_SZ, '原样 Unicode\n  {}')
                winreg.SetValueEx(key, 'UNRELATED', 0, winreg.REG_SZ, 'untouched')
            before = supervisor.capture()
            self.assertEqual(len(before), 3, 'all unknown GJ_GEAR_* keys must be captured')
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, path, 0, winreg.KEY_ALL_ACCESS) as key:
                winreg.SetValueEx(key, 'GJ_GEAR_STATE', 0, winreg.REG_SZ, 'modified')
                winreg.SetValueEx(key, 'GJ_GEAR_WAS_ABSENT', 0, winreg.REG_SZ, 'new')
                winreg.DeleteValue(key, 'GJ_GEAR_UNKNOWN_BINARY')
            supervisor.restore(before)
            self.assertEqual(before, supervisor.capture())
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, path) as key:
                self.assertEqual(winreg.QueryValueEx(key, 'UNRELATED')[0], 'untouched')
                with self.assertRaises(FileNotFoundError):
                    winreg.QueryValueEx(key, 'GJ_GEAR_WAS_ABSENT')
        finally:
            supervisor.REG_PATH = old_path
            winreg.DeleteKey(winreg.HKEY_CURRENT_USER, path)


if __name__ == '__main__':
    unittest.main()
