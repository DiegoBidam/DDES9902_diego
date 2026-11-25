#!/usr/bin/env python3
"""Source/isolated C# checks. Never starts, downloads or installs Unity/.NET."""
from pathlib import Path
import argparse, hashlib, json, os, re, shutil, subprocess, sys, tempfile
root = Path(__file__).resolve().parents[3]
args = argparse.ArgumentParser()
args.add_argument('--dotnet', default=shutil.which('dotnet'))
args.add_argument('--base', default='12a0ec7454feedd1f890bd90838e5cbcbd42f4ae')
options = args.parse_args()
changed = subprocess.check_output(['git', 'diff', '--name-only', options.base, '--', 'Assets', 'Packages', 'ProjectSettings'], cwd=root, text=True).splitlines()
allowed = {'Assets/Script/' + name for name in ['Bar intro.cs', 'BarPlayerMovement.cs', 'BeerGlassPour.cs', 'EntranceGuide.cs', 'UniformInteraction.cs']}
assert set(changed) <= allowed, changed
sources = sorted((root/'Assets/Script').glob('*.cs'))
assert '11df39673429a452dbbcbc5da55020d6' not in (root/'Assets/How to be a bartender.unity').read_text(), 'Unused tutorial UI unexpectedly added to main scene'
for source in (root/'Assets').rglob('*.cs'):
    assert not re.search(r'\bOnGUI\s*\(|\bGUI(?:Layout|Utility)?\s*\.|WorldToScreenPoint|RenderMode\.Screen', source.read_text()), source
for source in sources:
    before = subprocess.check_output(['git', 'show', f'{options.base}:{source.relative_to(root)}'], cwd=root).decode()
    after = source.read_text()
    fields = lambda s: re.findall(r'^    public (?:Camera|GameObject|Transform|AudioClip|AudioSource|float|Vector3) \w+(?:\s*=(?!>)\s*[^;\n]+)?;', s, re.M)
    assert fields(before) == fields(after), ('Inspector fields changed', source)
    payloads = lambda s: [hashlib.sha256(x.encode()).hexdigest() for x in re.findall(r'"[^"\n]{10000,}"', s)]
    assert payloads(before) == payloads(after), ('Embedded media changed', source)
print('PASS: only five existing runtime scripts changed in Assets; serialized Inspector fields and embedded media preserved; no screen UI/projected labels.', flush=True)
subprocess.run([sys.executable, str(Path(__file__).with_name('geometry.py'))], check=True)
assert options.dotnet, 'Use --dotnet /path/to/existing/dotnet (this verifier never installs it).'
dotnet = Path(options.dotnet).resolve()
version = lambda p: tuple(int(x) for x in re.findall(r'\d+', p.name)[:3])
csc = max((dotnet.parent/'sdk').glob('*/Roslyn/bincore/csc.dll'), key=lambda p: version(p.parents[2]))
pack = max((dotnet.parent/'packs/Microsoft.NETCore.App.Ref').iterdir(), key=version)
reference = next((pack/'ref').iterdir())
environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
with tempfile.TemporaryDirectory(prefix='bartender-spatial-') as temporary:
    output = Path(temporary)
    common = ['/nologo', '/langversion:9.0', '/nullable:disable', '/nostdlib+']
    common += [f'/reference:{p}' for p in reference.glob('*.dll')]
    common += [f'"{p}"' for p in sources] + [f'"{Path(__file__).with_name("UnityApiShims.cs")}"']
    for name, symbols, executable in [('Checks', '', True), ('WebChecks', 'UNITY_WEBGL,ENABLE_INPUT_SYSTEM', True)]:
        arguments = common + [f'/target:{"exe" if executable else "library"}', f'/out:{output/name}.dll']
        if symbols: arguments.append('/define:'+symbols)
        if executable: arguments.append(f'"{Path(__file__).with_name("Checks.cs")}"')
        response = output/(name+'.rsp'); response.write_text('\n'.join(arguments))
        subprocess.run([str(dotnet), str(csc), '@'+str(response)], check=True, cwd=root, env=environment)
        print('PASS: C# API-shim compilation for '+name+' (not a Unity compile).', flush=True)
        (output/(name+'.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions': {'tfm': reference.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': pack.name}}}))
        subprocess.run([str(dotnet), str(output/(name+'.dll'))], check=True, cwd=root, env=environment)
print('Unity rendering, real physics, Mac compilation, Web build and headset comfort still need testing in Unity.', flush=True)
