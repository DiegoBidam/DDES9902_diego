from pathlib import Path
import subprocess,tempfile,zipfile,shlex
repo=Path(__file__).resolve().parents[3];source=(repo/'Tools/WebBuild/Build Web on Mac.command').read_text()
assert 'cleanup_log_tail' not in source
assert source.index('if [ "${1:-}" = --package-existing ]')<source.index('project=${1:-}')<source.index('"$unity" -batchmode')
temporary=tempfile.TemporaryDirectory(prefix='bartender-release-check-');base=Path(temporary.name)
ditto=base/'mock-ditto';ditto.write_text('#!/bin/bash\nset -eu\n/bin/cp -a "$1/." "$2/"\n');ditto.chmod(0o755)
opened=base/'opened.txt';opener=base/'mock-open';opener.write_text('#!/bin/bash\nprintf "%s\\n" "$*" >> '+shlex.quote(str(opened))+'\n');opener.chmod(0o755)
a=source.index('package_web_build() {');b=source.index('\nif [ "${1:-}" = --package-existing ]',a)
function=source[a:b].replace('/usr/bin/ditto',shlex.quote(str(ditto))).replace('/usr/bin/open',shlex.quote(str(opener)))
def fixture(name,valid=True,missing=None,errors=0):
 root=base/name;build=root/'Bartender_Web';(build/'Build').mkdir(parents=True)
 (root/'Unity-WebBuild.log').write_text(('A2_WEB_BUILD_SUCCEEDED: '+str(build)+'\n') if valid else 'TypeDB: duplicate assembly\n')
 (build/'A2-build-result.txt').write_text('Result: Succeeded\nErrors: '+str(errors)+'\n')
 (build/'index.html').write_text('<html>fixture</html>')
 for f in ('game.loader.js','game.wasm','game.data','game.framework.js'):
  if f!=missing:(build/'Build'/f).write_text('fixture '+f)
 return root
valid=fixture('successful run with spaces');desktop=base/'Desktop with spaces'
for attempt in range(2):
 r=subprocess.run(['bash','-eu','-o','pipefail','-c',function+'\npackage_web_build "$1" "$2"','test',str(valid),str(desktop)],capture_output=True,text=True)
 assert r.returncode==0,(r.stdout,r.stderr)
 assert 'WEB BUILD SUCCEEDED' in r.stdout
zips=list(desktop.glob('*.zip'));assert len(zips)==2
for p in zips:
 with zipfile.ZipFile(p) as z:
  assert z.testzip() is None and 'index.html' in z.namelist()
  assert z.read('Build/game.wasm')==b'fixture game.wasm'
for name,root in [('failed log',fixture('failed',valid=False)),('missing wasm',fixture('missing',missing='game.wasm')),('reported errors',fixture('errors',errors=1))]:
 out=base/(name+' output')
 r=subprocess.run(['bash','-eu','-o','pipefail','-c',function+'\npackage_web_build "$1" "$2"','test',str(root),str(out)],capture_output=True,text=True)
 assert r.returncode!=0 and not out.exists(),(name,r.returncode,r.stdout,r.stderr)
 print('PASS: refuses archive for',name)
# Exercise actual full-script dispatch; only macOS's two filesystem/UI commands and uname are stubbed.
script=source.replace('/usr/bin/ditto',shlex.quote(str(ditto))).replace('/usr/bin/open',shlex.quote(str(opener)))
script=script.replace('if [ "$(uname -s)" != Darwin ]; then','if [ "Darwin" != Darwin ]; then',1)
copy=base/'launcher-test.sh';copy.write_text(script)
# Route only the default Desktop location to a fixture path. This does not change HOME.
script=script.replace('desktop=${2:-"$HOME/Desktop"}','desktop=${2:-'+shlex.quote(str(base/'dispatch-desktop'))+'}')
copy.write_text(script)
r=subprocess.run(['bash',str(copy),'--package-existing',str(valid)],capture_output=True,text=True)
assert r.returncode==0 and 'WEB BUILD SUCCEEDED' in r.stdout,(r.stdout,r.stderr)
assert len(list((base/'dispatch-desktop').glob('*.zip')))==1
print('PASS: two real valid ZIPs with root index.html, no merging/overwrite, spaced paths, and full-script recovery dispatch without Unity or Git calls.')
print('Fixture only: macOS ditto/open stubbed; no Unity compilation or browser play executed.')

# Real local Git verifies final-version selection without network or Unity.
cache=base/'source-cache';cache.mkdir()
def git(*args):
 r=subprocess.run(['git','-C',str(cache),*args],capture_output=True,text=True,check=True);return r.stdout.strip()
git('init','-b','main');git('config','user.name','Release fixture');git('config','user.email','fixture@example.invalid')
(cache/'file').write_text('one');git('add','.');git('commit','-qm','first');first=git('rev-parse','HEAD')
(cache/'file').write_text('two');git('commit','-qam','second');main=git('rev-parse','HEAD')
git('checkout','-qb','other',first);(cache/'file').write_text('unreviewed');git('commit','-qam','other');other=git('rev-parse','HEAD');git('checkout','-q','main')
a=source.index('select_source_revision() {');b=source.index('\npackage_web_build() {',a);selector=source[a:b]
for requested,wanted in [('',main),(first,first),(main,main)]:
 r=subprocess.run(['bash','-eu','-o','pipefail','-c',selector+'\nselect_source_revision "$1" "$2" "$3"','test',str(cache),main,requested],capture_output=True,text=True)
 assert r.returncode==0 and r.stdout.strip()==wanted,(requested,r.stdout,r.stderr)
for requested in ['short','0'*40,other]:
 r=subprocess.run(['bash','-eu','-o','pipefail','-c',selector+'\nselect_source_revision "$1" "$2" "$3"','test',str(cache),main,requested],capture_output=True,text=True)
 assert r.returncode!=0,(requested,r.stdout,r.stderr)
# A public manifest identifies the game revision without copying original Mac paths.
(valid/'build-info.txt').write_text('Source revision: '+first+'\nOriginal project: /Users/private-fixture/project\nUnity: 6000.6.2f1\n')
out=base/'release desktop'
r=subprocess.run(['bash','-eu','-o','pipefail','-c',function+'\npackage_web_build "$1" "$2"','test',str(valid),str(out)],capture_output=True,text=True)
assert r.returncode==0,(r.stdout,r.stderr)
archive=next(out.glob('*.zip'))
with zipfile.ZipFile(archive) as zipped:
 manifest=zipped.read('Bartender-release.txt').decode();assert first in manifest and 'private-fixture' not in manifest
 assert 'index.html' in zipped.namelist() and zipped.testzip() is None
import hashlib
checksum=Path(str(archive)+'.sha256');assert checksum.read_text().split()[0]==hashlib.sha256(archive.read_bytes()).hexdigest()
assert '게임 소스 버전: '+first in r.stdout
print('PASS: exact main-ancestor selection, rejection of invalid/unreviewed revisions, public source manifest and real ZIP SHA256.')
