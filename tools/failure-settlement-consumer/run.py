"""Fresh public-package durable control; never qualifies native ephemeral support."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

def run(repository, feed, work, version):
    repository, feed, work = (path.resolve() for path in (repository, feed, work))
    if work.exists(): raise ValueError("Consumer work directory must be new")
    if work.is_relative_to(repository): raise ValueError("Consumer must be outside the checkout")
    work.mkdir()
    (work / '.issue16-consumer-owner').write_text('Issue 16 disposable public-package consumer\n',encoding='utf-8')
    for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'):
        (work/name).write_text('<Project />\n',encoding='utf-8',newline='\n')
    shutil.copyfile(repository/'global.json',work/'global.json')
    shutil.copyfile(Path(__file__).with_name('Program.cs'),work/'Program.cs')
    direct=['OrcaCore','OrcaCore.Durable.Hosting','OrcaCore.Providers.InMemory']
    refs=''.join(f'<PackageReference Include="{name}" Version="{version}" />' for name in direct)
    (work/'Consumer.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup>'+refs+'</ItemGroup></Project>\n',encoding='utf-8',newline='\n')
    config=ET.Element('configuration');sources=ET.SubElement(config,'packageSources');ET.SubElement(sources,'clear')
    ET.SubElement(sources,'add',key='candidate',value=str(feed));ET.SubElement(sources,'add',key='nuget.org',value='https://api.nuget.org/v3/index.json')
    mapping=ET.SubElement(config,'packageSourceMapping')
    for key,patterns in [('candidate',['OrcaCore','OrcaCore.*']),('nuget.org',['*'])]:
        source=ET.SubElement(mapping,'packageSource',key=key)
        for pattern in patterns: ET.SubElement(source,'package',pattern=pattern)
    ET.SubElement(ET.SubElement(config,'fallbackPackageFolders'),'clear')
    ET.ElementTree(config).write(work/'NuGet.Config',encoding='utf-8',xml_declaration=True)
    cache=work/'packages';env={**os.environ,'NUGET_PACKAGES':str(cache),'NUGET_FALLBACK_PACKAGES':''}
    commands=[['dotnet','restore','Consumer.csproj','--configfile','NuGet.Config','--packages',str(cache),'-p:RestoreFallbackFolders='],
              ['dotnet','run','--project','Consumer.csproj','-c','Release','--no-restore']]
    for index,command in enumerate(commands):
        with (work/f'command-{index}.log').open('wb') as log:
            subprocess.run(command,cwd=work,env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=180)
    assets=json.loads((work/'obj/project.assets.json').read_text(encoding='utf-8'))
    packages={}
    for identity,row in assets['libraries'].items():
        name,actual_version=identity.split('/')
        if not name.startswith('OrcaCore'):continue
        if actual_version!=version: raise ValueError('Wrong product package version')
        package=feed/f'{name}.{version}.nupkg';digest=base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
        cached=cache/row['path']/(name.lower()+'.'+version+'.nupkg.sha512')
        if cached.read_text().strip()!=digest or row['sha512'].removeprefix('sha512-')!=digest: raise ValueError('Restored package does not match the candidate feed')
        packages[name]=dict(version=version,sha512=digest,sha256=hashlib.sha256(package.read_bytes()).hexdigest())
    expected={'OrcaCore','OrcaCore.Core','OrcaCore.Durable.Hosting','OrcaCore.Engine.Durable','OrcaCore.Provider.Abstractions','OrcaCore.Providers.InMemory','OrcaCore.Runtime.Protocol'}
    if set(packages)!=expected: raise ValueError('Unexpected selected durable closure')
    output=(work/'command-1.log').read_text(encoding='utf-8')
    if 'PASS durable-public-settlement:' not in output: raise ValueError('Missing actual consumer marker')
    report=dict(outcome='Passed',directReferences=direct,productPackages=packages,commands=commands,
                freshRestore=True,fallbackFoldersDisabled=True,projectReferences=False,nativeQualification=False,
                sourceSha256=hashlib.sha256((work/'Program.cs').read_bytes()).hexdigest())
    (work/'result.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(output.strip())

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('repository','feed','work'):parser.add_argument('--'+name,required=True,type=Path)
    parser.add_argument('--version',default='0.0.0-phase0')
    args=parser.parse_args();run(args.repository,args.feed,args.work,args.version)
