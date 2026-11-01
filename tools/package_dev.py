"""Package only mod files, never private game references or test outputs."""
from pathlib import Path
import zipfile
root=Path(__file__).resolve().parents[1]
dll=root/'1.7/Assemblies/ColonyTaskAuditor.dll'
assert dll.read_bytes()[:2]==b'MZ', 'Build the DLL first'
files=[root/'About/About.xml',dll,*sorted((root/'Languages').rglob('*.xml')),root/'README.md']
(root/'dist').mkdir(exist_ok=True)
with zipfile.ZipFile(root/'dist/ColonyTaskAuditor-development.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in files:z.write(p,'ColonyTaskAuditor/'+p.relative_to(root).as_posix())
print('Packaged development mod:',len(files),'files')
