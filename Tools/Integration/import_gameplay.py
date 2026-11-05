"""Copy the audited gameplay dependency closure, preserving source GUIDs."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
AUDIT = Path('C:/Users/Faiz/Documents/TacticalOutpostAudits/20261020_211802')
SOURCE = AUDIT / 'FriendRepo'
REPORT = ROOT / 'Docs/Integration'


def destination(path):
    if path.startswith('Assets/Outpost/'):
        return path.replace('Assets/Outpost/', 'Assets/TacticalOutpost/Gameplay/', 1)
    return path.replace('Assets/', 'Assets/ThirdParty/Gameplay/', 1)


def main():
    assets = json.loads((AUDIT / 'GameplayDependencies.json').read_text())['assets']
    existing = {}
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', meta.read_text(errors='replace'), re.M)
        if match:
            existing[match[1]] = meta
    copies = []
    for path in assets:
        source = SOURCE / path
        target = ROOT / destination(path)
        meta = Path(str(source) + '.meta')
        assert source.is_file() and meta.is_file(), path
        assert not source.read_bytes().startswith(b'version https://git-lfs.github.com/spec/'), path
        assert not target.exists() and not Path(str(target) + '.meta').exists(), str(target)
        guid = re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
        assert guid not in existing, (path, str(existing.get(guid)))
        copies.append((source, target, guid))
    REPORT.mkdir(parents=True, exist_ok=True)
    records = []
    for source, target, guid in copies:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        shutil.copy2(str(source) + '.meta', str(target) + '.meta')
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        assert hashlib.sha256(target.read_bytes()).hexdigest() == digest
        records.append({'source': source.relative_to(SOURCE).as_posix(),
                        'destination': target.relative_to(ROOT).as_posix(),
                        'guid': guid, 'sha256': digest})
    shutil.copy2(AUDIT / 'PlayerSceneOverrides.json', REPORT / 'PlayerSceneOverrides.json')
    (REPORT / 'ImportedAssets.json').write_text(json.dumps(records, indent=2))
    print(f'Imported {len(records)} assets with original GUIDs, no overwrites.')


if __name__ == '__main__':
    main()
