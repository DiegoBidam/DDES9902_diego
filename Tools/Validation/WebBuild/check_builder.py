#!/usr/bin/env python3
"""Checks the editor build helper with an existing .NET SDK and API shims only."""
from pathlib import Path
import argparse, json, os, re, shutil, subprocess, tempfile

root = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', default=shutil.which('dotnet'))
options = parser.parse_args()
assert options.dotnet, 'Pass --dotnet /path/to/existing/dotnet; nothing is installed.'
dotnet = Path(options.dotnet).resolve()
version = lambda p: tuple(int(x) for x in re.findall(r'\d+', p.name)[:3])
csc = max((dotnet.parent/'sdk').glob('*/Roslyn/bincore/csc.dll'), key=lambda p: version(p.parents[2]))
pack = max((dotnet.parent/'packs/Microsoft.NETCore.App.Ref').iterdir(), key=version)
reference = next((pack/'ref').iterdir())
with tempfile.TemporaryDirectory(prefix='bartender-builder-') as temporary:
    folder = Path(temporary)
    assembly = folder/'Checks.dll'
    arguments = ['/nologo', '/target:exe', '/langversion:9.0', '/nullable:disable', '/nostdlib+', '/define:UNITY_EDITOR', f'/out:{assembly}']
    arguments += [f'/reference:{p}' for p in reference.glob('*.dll')]
    arguments += [str(root/'Tools/WebBuild/BartenderWebBuild.cs'), str(Path(__file__).with_name('BuilderApiShims.cs'))]
    response = folder/'compile.rsp'; response.write_text('\n'.join(arguments))
    environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    subprocess.run([str(dotnet), str(csc), '@'+str(response)], check=True, cwd=folder, env=environment)
    assembly.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': reference.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': pack.name}}}))
    subprocess.run([str(dotnet), str(assembly), '-a2WebOutput', str(folder/'WebOutput')], check=True, cwd=folder, env=environment)
print('PASS: build settings/title restored on success and failure. API shims only; no Unity build executed.')
