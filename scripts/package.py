# LGC_MA_Fury_Localization package builder v2.0.0
# Builds release artifacts only; does not run Unity or modify a project.
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description='Build a VPM release ZIP.')
parser.add_argument('--tag', required=True)
args = parser.parse_args()
manifest = json.loads((ROOT / 'package.json').read_text(encoding='utf-8-sig'))
version = manifest['version']
if args.tag != 'v' + version:
    raise SystemExit('Release tag must match package.json version.')
probe = (ROOT / 'Editor/LocalizationTool.cs').read_text(encoding='utf-8-sig')
if f'Version = "{version}"' not in probe:
    raise SystemExit('Window version differs from package.json.')
for path in (ROOT / 'Editor').rglob('*.cs'):
    if f'v{version}' not in path.read_text(encoding='utf-8-sig').splitlines()[0]:
        raise SystemExit('Script version differs: ' + path.name)
for name in ('ma.zh-Hans.json', 'vrcfury.zh-Hans.json'):
    json.loads((ROOT / 'Editor/Localization' / name).read_text(encoding='utf-8-sig'))
asset_name = f"{manifest['name']}-{version}.zip"
if not manifest['url'].endswith('/' + args.tag + '/' + asset_name):
    raise SystemExit('Package download URL differs from release tag or filename.')
output = ROOT / 'dist'
output.mkdir(exist_ok=True)
files = list((ROOT / 'Editor').rglob('*'))
for name in ['Editor.meta', 'package.json', 'README.md', 'CHANGELOG.md', 'LICENSE.md', 'THIRD_PARTY_NOTICES.md']:
    path = ROOT / name
    files.append(path)
    if name != 'Editor.meta':
        files.append(ROOT / (name + '.meta'))
archive_path = output / asset_name
with zipfile.ZipFile(archive_path, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for path in sorted(set(files)):
        if path.is_file():
            info = zipfile.ZipInfo(path.relative_to(ROOT).as_posix(), date_time=(2026, 11, 19, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes())
shutil.copyfile(ROOT / 'package.json', output / 'package.json')
checksum = hashlib.sha256(archive_path.read_bytes()).hexdigest()
(output / 'SHA256SUMS.txt').write_text(f'{checksum}  {asset_name}\n', encoding='utf-8')
print('VPM ZIP: ' + asset_name)
print('SHA-256: ' + checksum)