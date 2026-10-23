# Verify retained native records and raw/Git/current-harness bindings; no writes.
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

ARTIFACTS = Path(__file__).resolve().parent
ROOT = ARTIFACTS.parents[3]
sys.path.insert(0, str(ROOT / 'tools/ephemeral-native-assessment'))
from gate import validate_assessment, error_identity


def sha(data): return hashlib.sha256(data).hexdigest()
def git(*args): return subprocess.check_output(['git', *args], cwd=ROOT)
def require(condition, reason):
    if not condition: raise ValueError(reason)


catalog = json.loads((ARTIFACTS / 'task-2-native-evidence-catalog-2026-10-21.json').read_text(encoding='utf-8'))
reports = []
for osname, receipt in catalog['nativeReportFiles'].items():
    raw = (ARTIFACTS / receipt['path']).read_bytes()
    require(sha(raw) == receipt['sha256'], 'plain native report drift')
    report = json.loads(raw)
    require(report['host']['os'] == osname, 'wrong native report identity')
    reports.append(report)
validate_assessment(reports)
source = catalog['nativeSourceCommit']
require(git('cat-file', '-t', source).strip() == b'commit', 'native source is not a Git commit')
for report in reports:
    require(report['sourceSha'] == source, 'native source drift')
    for name, expected in report['harnessFiles'].items():
        relative = 'tools/ephemeral-native-assessment/' + name
        require(sha((ROOT / relative).read_bytes()) == expected, 'current harness drift: ' + name)
        require(sha(git('show', source + ':' + relative)) == expected, 'committed harness drift: ' + name)
product = ['src', 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'OrcaCore.slnx', 'docs/specs/17-public-authoring-contract.cs']
require(not git('diff', '--name-only', catalog['issueHead'], source, '--', *product), 'native product source drift')
require(not git('diff', '--name-only', catalog['issueHead'], '--', *product), 'current product source drift')
require(not git('ls-files', '--others', '--exclude-standard', '--', *product), 'untracked product source')
entries = 0
for record in catalog['archives']:
    path = ARTIFACTS / record['archive']
    require(path.stat().st_size == record['bytes'] and sha(path.read_bytes()) == record['sha256'], 'raw archive drift')
    with zipfile.ZipFile(path) as archive:
        expected = {item['path']: item for item in record['entries']}
        require(len(archive.namelist()) == len(expected) and set(archive.namelist()) == set(expected), 'raw entry inventory drift')
        for name, item in expected.items():
            raw = archive.read(name)
            require(len(raw) == item['bytes'] and sha(raw) == item['sha256'], 'raw entry drift: ' + name)
            entries += 1
        if record['sourceCommit'] == source:
            for osname, receipt in catalog['nativeReportFiles'].items():
                require(sha(archive.read(osname + '/report.json')) == receipt['rawSha256'], 'raw final record drift')
                require(archive.read(osname + '/report.json').replace(b'\r\n', b'\n') == (ARTIFACTS / receipt['path']).read_bytes(), 'raw/plain LF transport disagreement')
print(f'Both native records, current/committed harness and {entries} retained raw entries match; no product source change.')

# Interpretations are appended; no original discovery record is rewritten.
ledger = json.loads((ARTIFACTS / 'task-2-discovery-finding-dispositions-2026-10-21.json').read_text(encoding='utf-8'))
warning_rows = runtime_rows = 0
for run in ledger['runs']:
    require(run['qualificationCredit'] is False, 'old discovery used as final qualification')
    tag, _, osname = run['run'].partition('/')
    archive_record = next(x for x in catalog['archives'] if x['archive'] == tag + '-raw.zip')
    with zipfile.ZipFile(ARTIFACTS / archive_record['archive']) as archive:
        original = json.loads(archive.read((osname + '/' if osname else '') + 'report.json'))
    require(run['sourceSha'] == original['sourceSha'] and run['host'] == original['host'], 'discovery source/host drift')
    require([c['flavor'] for c in run['cells']] == [c['flavor'] for c in original['cells']], 'discovery cell inventory drift')
    for cell, old in zip(run['cells'], original['cells']):
        require([(d['identity'], d['message'], d['count']) for d in cell['diagnostics']] == [(d['identity'], d['message'], d['count']) for d in old['diagnostics']], 'discovery warning inventory drift')
        for finding in cell['diagnostics']:
            require(finding['disposition'] == 'Unsupported' and finding['count'] == finding['expectedCount'] and finding['rationale'] and finding['proof'] and finding['emittingAssembly'] and finding['member'], 'unclassified discovery warning')
            warning_rows += 1
        failed = [c for c in old['cases'] if c['outcome'] != 'Passed']
        require([c['id'] for c in failed] == [f['id'] for f in cell['runtimeFindings']], 'missing discovery runtime finding')
        for case, finding in zip(failed, cell['runtimeFindings']):
            require(finding['identity'] == error_identity(case) and finding['exceptions'] == case['exceptions'], 'discovery exception bytes/identity drift')
            require(finding['rationale'] and finding['proof'] and finding['finalCompatibility'] == 'AssessedUnsupported', 'discovery result hidden')
            if finding['disposition'] == 'Corrected':
                require(finding['scope'] == 'HarnessCauseReporting' and (case['exceptions'][0]['type'] == 'System.TimeoutException' or case['exceptions'][0]['message'].startswith('ASSERTION:')), 'invalid harness correction')
            else:
                require(finding['disposition'] == 'Unsupported' and finding['scope'] == 'DeploymentBehavior', 'unclassified discovery runtime finding')
            runtime_rows += 1
print(f'All {warning_rows} discovery warning rows and {runtime_rows} discovery runtime findings retain exact original identities and individual dispositions.')
