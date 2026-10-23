const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {spawnSync,execFileSync}=require('node:child_process');
const root=path.resolve(__dirname,'..'),project=path.join(root,'Orange3DTool');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const baseline=execFileSync('git',['show','a1b47c3:Orange3DTool/Program.cs'],{cwd:root,encoding:'utf8'});
const originalNames=execFileSync('git',['ls-tree','--name-only','a1b47c3:Orange3DTool'],{cwd:root,encoding:'utf8'}).trim().split(/\r?\n/).filter(n=>n.endsWith('.cs'));
const originalTests=originalNames.filter(n=>n.endsWith('Tests.cs'));
const productionNames=originalNames.filter(n=>!n.endsWith('Tests.cs')&&!['LauncherTestPaths.cs','CoreReleaseTool.cs'].includes(n));
const q=s=>"'"+s.replaceAll("'","''")+"'";
function ps(command){return spawnSync('pwsh.exe',['-NoProfile','-NonInteractive','-Command',"$ErrorActionPreference='Stop'; "+command],{encoding:'utf8'});}
function sources(dir=project,diagnostics=false){
 const helper=path.join(root,'build/ProductionSources.ps1');
 // Before isolation, exercise the actual build's source expression, not a missing-file assertion.
 const command=fs.existsSync(helper)?". "+q(helper)+"; @("+(diagnostics?'Get-OzlDiagnosticSources':'Get-OzlProductionSources')+" -ProjectDirectory "+q(dir)+") | ConvertTo-Json -Compress":'$project='+q(dir)+'; '+read('Build-CoreRelease.ps1').split(/\r?\n/).find(l=>l.startsWith('$sources='))+'; $sources | ConvertTo-Json -Compress';
 const r=ps(command);assert.equal(r.status,0,r.stderr);return JSON.parse(r.stdout);
}
function suiteLine(s){return s.split(/\r?\n/).find(l=>l.includes('if(args.Contains("--self-test"))'));}
test('baseline captures 84 original test files and 56 distinct invoked test suites',()=>{
 assert.equal(originalTests.length,84);
 assert.equal(new Set([...suiteLine(baseline).matchAll(/\b(\w+Tests)\.\w+\(/g)].map(m=>m[1])).size,56);
});
test('actual production input excludes tests and is exactly the explicit 92 original files',()=>{
 const actual=sources();assert.equal(actual.filter(p=>p.endsWith('Tests.cs')).length,0,'production glob includes original 84 test files');
 assert.deepEqual(actual.map(p=>path.basename(p)).sort(),productionNames.sort());assert.equal(actual.length,92);
 assert.equal(new Set(actual.map(p=>p.toLowerCase())).size,actual.length);assert.ok(actual.every(path.isAbsolute));
});
test('production entry does not dispatch diagnostic suites',()=>{
 assert.equal(read('Orange3DTool/Program.cs').includes('--self-test")) { try'),false,'Program still dispatches original 56 suites');
});
test('resolver excludes unknown source and fails closed for missing listed source',()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'ozl-sources-'));
 try{
 for(const n of originalNames)fs.writeFileSync(path.join(dir,n),'');
 fs.writeFileSync(path.join(dir,'UnknownProduction.cs'),'');
 assert.deepEqual(sources(dir).map(p=>path.basename(p)).sort(),productionNames.sort());
 fs.unlinkSync(path.join(dir,'Program.cs'));
 const r=ps('. '+q(path.join(root,'build/ProductionSources.ps1'))+'; Get-OzlProductionSources -ProjectDirectory '+q(dir));
 assert.notEqual(r.status,0);assert.match(r.stderr,/Missing production source.*Program.cs/);
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});
test('diagnostic membership adds original tests, fixture paths and two diagnostic files exactly once',()=>{
 const actual=sources(project,true).map(p=>path.basename(p));
 const expected=[...productionNames,...originalTests,'LauncherTestPaths.cs','DiagnosticProgram.cs','DiagnosticSelfTests.cs'];
 assert.equal(actual.length,expected.length);assert.equal(actual.length,179);assert.equal(new Set(actual).size,actual.length);
 assert.deepEqual(actual.sort(),expected.sort());
});
test('dispatcher order, embedded Test class, workers and ordinary UI remain intact',()=>{
 const program=read('Orange3DTool/Program.cs'),diag=read('Orange3DTool/DiagnosticProgram.cs'),self=read('Orange3DTool/DiagnosticSelfTests.cs');
 assert.equal(suiteLine(diag),suiteLine(baseline));
 assert.ok(self.includes(baseline.slice(baseline.indexOf(' static class Test {'),baseline.lastIndexOf('\n}')).trimEnd()));
 const workers=s=>s.slice(s.indexOf('   if(args.Length==4&&args[0]=="--steam-service-worker")'),s.indexOf('   if(args.Length>=2&&args[0]=="--check-workshop")'));
 assert.ok(program.includes(workers(baseline)));
 assert.match(program,/\[STAThread\] internal static int Main\(string\[\] args\)/);
 assert.match(diag,/\[STAThread\] (?:public |internal )?static int Main\(string\[\] args\)/);
 assert.match(diag,/return Program.Main\(args\)/);assert.match(diag,/LauncherRuntime.Initialize\(\)/);assert.match(diag,/SecurityProtocolType.Tls12/);
 assert.match(program,/return ModernBootstrap.Run\(args\)/);
 for(const name of ['Links','Engine','MainForm'])assert.ok(program.includes('class '+name));
 assert.ok(program.includes('public static bool PreviewMode;'));
 assert.equal(program.slice(program.indexOf(' static class Links'),program.lastIndexOf('\n}')).trimEnd(),baseline.slice(baseline.indexOf(' static class Links'),baseline.indexOf(' static class Test {')).trimEnd());
 assert.match(diag,/Application.EnableVisualStyles\(\); Application.SetCompatibleTextRenderingDefault\(false\);/);
 assert.match(diag,/Program.PreviewMode=true/);
 for(const s of [diag,self]){assert.match(s,/#if OZL_DIAGNOSTICS/);assert.match(s,/#endif/);}
 assert.match(program,/#if OZL_PRODUCTION && OZL_DIAGNOSTICS\s+#error Production and diagnostic symbols are mutually exclusive/);
});
test('all eleven diagnostic flags are rejected before UI and remain available in diagnostics',()=>{
 const p=read('Orange3DTool/Program.cs'),d=read('Orange3DTool/DiagnosticProgram.cs');
 const flags=['--buddy-crash','--buddy-steam-crash','--check-workshop','--subscribe-workshop','--verify-bundle','--verify-cache','--prepare-dlss','--fetch-workshop','--self-test','--preview','--preview-help'];
 for(const f of flags){assert.ok(p.includes('"'+f+'"'));assert.ok(d.includes('"'+f+'"'));}
 assert.ok(p.indexOf('return 2;')<p.indexOf('Application.EnableVisualStyles()'));
 assert.match(p,/args.Any\(arg=>diagnosticFlags.Contains\(arg\)\)/);
});
test('PowerShell AST confirms every compiler invocation uses resolver and matching symbols and entries',()=>{
 const files=['Build-CoreRelease.ps1','Build-Setup.ps1','Build-Tests.ps1','Orange3DTool/Build-Modern.ps1'];
 for(const f of files){
 const r=ps('$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile('+q(path.join(root,f))+',[ref]$tokens,[ref]$errors); if($errors.Count){throw ($errors | Out-String)}; @($ast.FindAll({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.Extent.Text -match "/target:"},$true) | ForEach-Object {$_.Extent.Text}) | ConvertTo-Json -Compress');
 assert.equal(r.status,0,r.stderr);let commands=JSON.parse(r.stdout);if(!Array.isArray(commands))commands=[commands];assert.ok(commands.length);
 const s=read(f);assert.match(s,/\. \(Join-Path \$PSScriptRoot '(?:\.\.\\)?build\\ProductionSources.ps1'\)/);assert.doesNotMatch(s,/Get-ChildItem.*-Filter '\*\.cs'/);
 for(const c of commands){assert.ok(c.includes('/define:'+(f==='Build-Tests.ps1'?'OZL_DIAGNOSTICS':'OZL_PRODUCTION')));assert.match(c,/\$(?:sources|toolSources)\b/);assert.match(c,/\/main:/);}
 assert.match(s,f==='Build-Tests.ps1'?/Get-OzlDiagnosticSources -ProjectDirectory \$project/:/Get-OzlProductionSources -ProjectDirectory \$(?:project|PSScriptRoot)/);
 }
 assert.match(read('Build-Tests.ps1'),/\$Entry='PZ3D.DiagnosticProgram'/);assert.match(read('Build-Tests.ps1'),/"\/main:\$Entry"/);
 assert.match(read('Build-CoreRelease.ps1'),/\$toolSources=@\(\$sources\)\+@\(Join-Path \$project 'CoreReleaseTool.cs'\)/);
});

