"""Path-layout regression tests for tools/setup.py.

Run with: python3 -m unittest tools/test_setup.py
"""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import setup  # noqa: E402


def make_game(root, relative):
    managed = root / relative
    managed.mkdir(parents=True)
    (managed / "assembly_valheim.dll").touch()


class GameLayoutTests(unittest.TestCase):
    def test_windows_and_linux_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory) / "Valheim"
            make_game(game, Path("valheim_Data") / "Managed")
            self.assertEqual(setup.game_layout(game), Path("valheim_Data") / "Managed")
            self.assertEqual(setup.normalize_game_dir(game), game.resolve())

    def test_macos_layout_and_legacy_resources_path(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory) / "Valheim"
            relative = Path("valheim.app") / "Contents" / "Resources" / "Data" / "Managed"
            make_game(game, relative)
            legacy_resources = game / "valheim.app" / "Contents" / "Resources"
            self.assertEqual(setup.game_layout(game), relative)
            self.assertTrue(setup.is_macos_game(game))
            self.assertEqual(setup.normalize_game_dir(legacy_resources), game.resolve())

    def test_legacy_macos_loader_is_backed_up_and_player_files_are_kept(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory) / "Valheim"
            resources = game / "valheim.app" / "Contents" / "Resources"
            make_game(game, Path("valheim.app") / "Contents" / "Resources" / "Data" / "Managed")
            legacy = resources / "BepInEx"
            (legacy / "config").mkdir(parents=True)
            (legacy / "plugins" / "OldMod").mkdir(parents=True)
            (legacy / "config" / "old.cfg").write_text("old setting")
            (legacy / "plugins" / "OldMod" / "OldMod.dll").touch()
            (resources / "start_game_bepinex.sh").touch()

            setup.migrate_legacy_macos_install(game)

            self.assertTrue((game / "BepInEx" / "config" / "old.cfg").is_file())
            self.assertTrue((game / "BepInEx" / "plugins" / "OldMod" / "OldMod.dll").is_file())
            backups = list(game.glob("BepInEx-macos-legacy-backup-*"))
            self.assertEqual(len(backups), 1)
            self.assertTrue((backups[0] / "BepInEx").is_dir())
            self.assertTrue((backups[0] / "start_game_bepinex.sh").is_file())


if __name__ == "__main__":
    unittest.main()
