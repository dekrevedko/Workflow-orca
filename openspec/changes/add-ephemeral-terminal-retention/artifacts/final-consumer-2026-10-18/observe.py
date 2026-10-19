"""External heap observation of the ordinary public-package consumer; no product reflection/friends."""
from pathlib import Path
import argparse, hashlib, json, os, re, subprocess, sys

parser=argparse.ArgumentParser()
parser.add_argument('--lab',required=True)
parser.add_argument('--feed',required=True)
parser.add_argument('--dump-tool',required=True)
parser.add_argument('--out',required=True)
parser.add_argument('--logs',required=True)
args=parser.parse_args()
lab,feed,out,logs=map(lambda s:Path(s).resolve(),(args.lab,args.feed,args.out,args.logs))
logs.mkdir(parents=True,exist_ok=True)
dump=lab/'retention-measurement.dmp'
assert not dump.exists(), 'Never replace an existing dump'
flags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0
def command(argv):
    result=subprocess.run(list(map(str,argv)),text=True,capture_output=True,creationflags=flags)
    if result.returncode: raise RuntimeError(f'{argv}: {result.stdout}\n{result.stderr}')
    return result.stdout
def analyze(commands):
    argv=[args.dump_tool,'analyze',str(dump)]
    for cmd in [*commands,'exit']: argv+=['--command',cmd]
    return command(argv)
def pointer(text,name):
    match=re.search(r'\s+([0-9a-fA-F]{8,16})\s+'+re.escape(name)+r'\s*$',text,re.M)
    assert match,(name,text)
    return match[1]
def dictionary(address):
    data=analyze(['dumpobj '+address])
    def scalar(name):
        match=re.search(r'\s+(-?\d+)\s+'+name+r'\s*$',data,re.M)
        assert match,(name,data)
        return int(match[1])
    entries=pointer(data,'_entries')
    capacity=0
    if int(entries,16):
        array=analyze(['dumpobj '+entries])
        match=re.search(r'Number of elements\s*:?\s*(\d+)',array,re.I)
        assert match,array
        capacity=int(match[1])
    return dict(count=scalar('_count')-scalar('_freeCount'),capacity=capacity)
def heap(mode,phase):
    collect=command([args.dump_tool,'collect','--process-id',str(process.pid),'--type','Heap','--output',str(dump)])
    (logs/f'{mode}-{phase}-collect.log').write_text(collect,encoding='utf-8')
    prefixes=['OrcaCore.Engine.Ephemeral.Execution.WorkflowInstance','RetentionConsumer.ResidentState',
              'RetentionConsumer.ResidentOutput','OrcaCore.Engine.Ephemeral.EphemeralWorkflowDefinitionRegistry',
              'OrcaCore.Engine.Ephemeral.Execution.EphemeralRetentionCoordinator']
    stats=analyze(['dumpheap -stat -type '+p for p in prefixes])
    (logs/f'{mode}-{phase}-heap.log').write_text(stats,encoding='utf-8')
    rows={}
    for match in re.finditer(r'^\s*([0-9a-fA-F]{8,16})\s+([\d,]+)\s+([\d,]+)\s+(.+?)\s*$',stats,re.M):
        rows[match[4]]=dict(methodTable=match[1],count=int(match[2].replace(',','')),bytes=int(match[3].replace(',','')))
    def addresses(name):
        mt=rows[name]['methodTable']
        text=analyze(['dumpheap -mt '+mt+' -short'])
        return re.findall(r'^([0-9a-fA-F]{12,16})\s*$',text,re.M)
    registry='OrcaCore.Engine.Ephemeral.EphemeralWorkflowDefinitionRegistry'
    coordinator='OrcaCore.Engine.Ephemeral.Execution.EphemeralRetentionCoordinator'
    main=addresses(registry)
    assert len(main)==1,main
    fields=analyze(['dumpobj '+main[0]])
    metadata={name:dictionary(pointer(fields,name)) for name in ['instances','starts','registrations']}
    owner=addresses(coordinator)
    assert len(owner)==1,owner
    owner_fields=analyze(['dumpobj '+owner[0]])
    metadata['retentionEntries']=dictionary(pointer(owner_fields,'entries'))
    result=dict(types=rows,metadata=metadata,dumpBytes=dump.stat().st_size)
    assert dump.parent==lab and dump.name=='retention-measurement.dmp'
    dump.unlink() # This one generated dump has already been analyzed; retain compact results only.
    return result

results=dict(method='Diagnostic full GC in consumer, external dotnet-dump Heap and SOS; no engine reflection or friend access',
    lab=str(lab),feed=str(feed),dumpTool=command([args.dump_tool,'--version']).strip(),
    dotnet=command(['dotnet','--info']),packages={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(feed.glob('*.nupkg'))},runs=[])
assert len(results['packages'])==12
for mode in ['unbounded','bounded']:
    process=subprocess.Popen([str(lab/'bin/Release/net10.0/RetentionConsumer.exe'),mode],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,bufsize=1,creationflags=flags)
    try:
        while line:=process.stdout.readline():
            record=json.loads(line)
            record['mode']=mode
            if record['kind']=='checkpoint':
                assert record['pid']==process.pid
                record['heap']=heap(mode,record['phase'])
                expected=min(record['jobs'],16) if mode=='bounded' else record['jobs']
                for name in ['instances','starts']:
                    assert record['heap']['metadata'][name]['count']==expected,(mode,record)
                assert record['heap']['metadata']['registrations']['count']==2
            results['runs'].append(record)
            out.write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
            print(mode,record.get('phase','result'),record.get('heap',{}).get('metadata',{}),flush=True)
            if record['kind']=='checkpoint': process.stdin.write('continue\n'); process.stdin.flush()
        code=process.wait(timeout=30)
        assert code==0,(code,process.stderr.read())
    finally:
        if process.poll() is None: process.kill();process.wait()
print('All public-consumer and external metadata checks passed.',flush=True)
