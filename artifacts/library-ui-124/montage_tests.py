"""Run the real montage script against isolated captures, never user data."""
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


class MontageCoverageTests(unittest.TestCase):
    def test_eighteen_captures_keep_the_full_sixth_row_for_both_themes(self):
        # Break caught: a fixed five-row canvas silently clips captures 15–17.
        source = Path(__file__).resolve().parent / 'montage.py'
        fixtures = source.parents[1] / 'TestFixtures'
        fixtures.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='montage-', dir=fixtures) as root:
            root = Path(root)
            shutil.copyfile(source, root / 'montage.py')
            (root / 'visual').mkdir()
            for theme in ('dark', 'light'):
                for n in range(18):
                    Image.new('RGB', (40, 40), (n * 10, 50, 100)).save(
                        root / 'visual' / f'{theme}-{n:02}.png')
            subprocess.run([sys.executable, str(root / 'montage.py')], check=True)
            for theme in ('dark', 'light'):
                with self.subTest(theme=theme), Image.open(root / f'{theme}-visual-sheet.png') as sheet:
                    self.assertEqual(sheet.size, (1500, 1980), 'all 18 captures need six complete rows')
                    for x, expected in ((5, (150, 50, 100)), (505, (160, 50, 100)), (1005, (170, 50, 100))):
                        self.assertEqual(sheet.getpixel((x, 1680)), expected)


if __name__ == '__main__':
    unittest.main()
