"""Package only plugin dependencies; never redistribute Dalamud or game assemblies."""
import hashlib,json,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
out=ROOT/'src/JiZhang/bin/Release'
artifacts=ROOT/'artifacts';artifacts.mkdir(exist_ok=True)
manifest=json.loads((out/'JiZhang.json').read_text(encoding='utf-8'))
files=['JiZhang.dll','JiZhang.Core.dll','JiZhang.json','JiZhang.deps.json','images/icon.png','Microsoft.Data.Sqlite.dll','SQLitePCLRaw.batteries_v2.dll','SQLitePCLRaw.core.dll','SQLitePCLRaw.provider.e_sqlite3.dll','runtimes/win-x64/native/e_sqlite3.dll']
archive=artifacts/'JiZhang.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for name in files:
  z.write(out/name,name)
 # Root copy also supports native loaders using the plugin directory.
 z.write(out/'runtimes/win-x64/native/e_sqlite3.dll','e_sqlite3.dll')
 z.write(ROOT/'LICENSE','LICENSE')
 z.write(ROOT/'docs/AI_USAGE.md','AI_USAGE.md')
sha=hashlib.sha256(archive.read_bytes()).hexdigest()
(artifacts/'SHA256SUMS').write_text(sha+'  JiZhang.zip\n',encoding='utf-8')
version=manifest['AssemblyVersion']
url=f'https://github.com/wynnforthework/FFXIV-JiZhang/releases/download/v{version}/JiZhang.zip'
manifest.update(IsHide=False,IsTestingExclusive=False,DownloadLinkInstall=url,DownloadLinkUpdate=url,DownloadLinkTesting=url,LastUpdate=0)
(artifacts/'repo.json').write_text(json.dumps([manifest],ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
with zipfile.ZipFile(archive) as z:
 assert not z.testzip();assert all(name in z.namelist() for name in files)
 assert not any(Path(x).name in ('Dalamud.dll','FFXIVClientStructs.dll','Lumina.dll') for x in z.namelist())
print(f'Packaged {archive.name}, version {version}, SHA256 {sha}')
