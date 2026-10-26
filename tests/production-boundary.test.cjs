const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {spawnSync,execFileSync}=require('node:child_process');
const root=path.resolve(__dirname,'..'),project=path.join(root,'Orange3DTool');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const baseline=execFileSync('git',['show','a1b47c3:Orange3DTool/Program.cs'],{cwd:root,encoding:'utf8'});
const originalNames=execFileSync('git',['ls-tree','--name-only','a1b47c3:Orange3DTool'],{cwd:root,encoding:'utf8'}).trim().split(/\r?\n/).filter(n=>n.endsWith('.cs'));
const originalTests=originalNames.filter(n=>n.endsWith('Tests.cs'));
const approvedCommit='2dcfe4c625dc5cb679dd4a86f1a9dd78d3029296';
const approvedProgram=execFileSync('git',['show',approvedCommit+':Orange3DTool/Program.cs'],{cwd:root,encoding:'utf8'});
const approvedNames=execFileSync('git',['ls-tree','--name-only',approvedCommit+':Orange3DTool'],{cwd:root,encoding:'utf8'}).trim().split(/\r?\n/);
const approvedResolver=execFileSync('git',['show',approvedCommit+':build/ProductionSources.ps1'],{cwd:root,encoding:'utf8'});
const repairTests=['MemoryFileWriteTests.cs','LauncherMultiplayerCacheTests.cs','LauncherMultiplayerDataTests.cs'];
const featureSources=['GameStartupComponent.cs','GameStartupHost.cs','GameStartupModels.cs','GameStartupMonitor.cs','GameStartupPresentation.cs','GameStartupPresenter.cs','GameStartupWindow.cs','GameLogModels.cs','GameLogAnalyzer.cs','GameLogAnalysisService.cs','GameLogAnalysisController.cs','GameLogAnalysisHost.cs'];
const featureTests=['GameStartupComponentTests.cs','GameStartupBridgeTests.cs','GameStartupMonitorTests.cs','GameStartupPresentationTests.cs','GameStartupPresenterTests.cs','GameStartupSuiteTests.cs','GameStartupWindowTests.cs','GameLogAnalyzerTests.cs','GameLogAnalysisServiceTests.cs','GameLogCorpusTests.cs','NativeWorkflowRepairTests.cs'];
// Reviewed additions already present in the approved commit, kept separate from
// this local library preview. Expected membership never comes from the live resolver.
const approvedReleaseSources=['FeedbackService.cs','FeedbackUpload.cs','FeedbackController.cs','StartupOptionsController.cs'];
const approvedReleaseTests=['FeedbackDeadlineTests.cs','FeedbackTests.cs','ForceStopRecoveryTests.cs','GameStartupControlsTests.cs','LaunchByteComparisonTests.cs','LaunchPathValidationTests.cs','LaunchPendingTests.cs','NativeLaunchLatencyTests.cs','ProfileLatencyTests.cs','ServerTranslationBoundsTests.cs'];
const approvedProductionNames=originalNames.filter(n=>!n.endsWith('Tests.cs')&&!['LauncherTestPaths.cs','CoreReleaseTool.cs'].includes(n)).concat(featureSources,approvedReleaseSources);
const approvedTests=originalTests.concat(repairTests,featureTests,approvedReleaseTests);
const librarySources=['LibraryController.cs','LibraryHost.cs','RecommendedServers.cs','SaveBackups.cs','SafehouseTransferFormat.cs','SafehouseTransfer.cs','JavaComponents.cs','VersionLibrary.cs','SteamVersionWorkflow.cs'];
const libraryTests=['JavaComponentsTests.cs','LauncherGameVersionTests.cs','LibraryIntegrationTests.cs','RecommendedServersTests.cs','SafehouseTransferFormatTests.cs','SafehouseTransferTests.cs','SaveBackupsTests.cs','SteamVersionWorkflowTests.cs','VersionLaunchTests.cs','VersionLibrarySuiteTests.cs','VersionLibraryTests.cs'];
const productionNames=approvedProductionNames.concat(librarySources);
const diagnosticNames=productionNames.concat(approvedTests,libraryTests,['LauncherTestPaths.cs','DiagnosticProgram.cs','DiagnosticSelfTests.cs']);
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
 const pinnedProduction=[...approvedResolver.split('function Get-OzlVelopackSources')[0].matchAll(/'([^']+\.cs)'/g)].map(m=>m[1]);
 assert.equal(approvedProductionNames.length,108);assert.equal(approvedTests.length,108);
 assert.deepEqual([...approvedProductionNames].sort(),pinnedProduction.sort());
 assert.deepEqual([...approvedTests].sort(),approvedNames.filter(n=>n.endsWith('Tests.cs')&&n!=='DiagnosticSelfTests.cs').sort());
});
test('actual production input excludes tests and is the pinned approved membership plus explicit library files',()=>{
 const actual=sources();assert.equal(actual.filter(p=>p.endsWith('Tests.cs')).length,0,'production glob includes original 84 test files');
 assert.deepEqual(actual.map(p=>path.basename(p)).sort(),[...productionNames].sort());assert.equal(actual.length,117);
 assert.equal(new Set(actual.map(p=>p.toLowerCase())).size,actual.length);assert.ok(actual.every(path.isAbsolute));
});
test('production entry does not dispatch diagnostic suites',()=>{
 assert.equal(read('Orange3DTool/Program.cs').includes('--self-test")) { try'),false,'Program still dispatches original 56 suites');
});
test('resolver excludes unknown source and fails closed for missing listed source',()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'ozl-sources-'));
 try{
 for(const n of new Set([...originalNames,...productionNames]))fs.writeFileSync(path.join(dir,n),'');
 fs.writeFileSync(path.join(dir,'UnknownProduction.cs'),'');
 assert.deepEqual(sources(dir).map(p=>path.basename(p)).sort(),[...productionNames].sort());
 fs.unlinkSync(path.join(dir,'Program.cs'));
 const r=ps('. '+q(path.join(root,'build/ProductionSources.ps1'))+'; Get-OzlProductionSources -ProjectDirectory '+q(dir));
 assert.notEqual(r.status,0);assert.match(r.stderr,/Missing production source.*Program.cs/);
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});
test('diagnostic membership adds pinned approved and library tests, fixture paths and diagnostic files exactly once',()=>{
 const actual=sources(project,true).map(p=>path.basename(p));
 assert.equal(actual.length,diagnosticNames.length);assert.equal(actual.length,239);assert.equal(new Set(actual).size,actual.length);
 assert.deepEqual(actual.sort(),[...diagnosticNames].sort());
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
 // Keep the original isolation checks while freezing ordinary UI against the
 // reviewed approved commit; later Engine behavior has its own focused tests.
 assert.equal(program.slice(program.indexOf(' static class Links'),program.indexOf(' sealed class Engine')),baseline.slice(baseline.indexOf(' static class Links'),baseline.indexOf(' sealed class Engine')));
 assert.equal(program.slice(program.indexOf(' sealed partial class MainForm'),program.lastIndexOf('\n}')).trimEnd(),approvedProgram.slice(approvedProgram.indexOf(' sealed partial class MainForm'),approvedProgram.lastIndexOf('\n}')).trimEnd());
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


function astTexts(file,predicate){
 const r=ps('$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile('+q(path.join(root,file))+',[ref]$tokens,[ref]$errors);if($errors.Count){throw ($errors | Out-String)}; @($ast.FindAll({param($n) '+predicate+'},$true) | ForEach-Object {$_.Extent.Text}) | ConvertTo-Json -Compress');
 assert.equal(r.status,0,r.stderr);if(!r.stdout.trim())return [];const value=JSON.parse(r.stdout);return Array.isArray(value)?value:[value];
}
test('existing CoreReleaseTool selector adds exactly one source only for that entry',()=>{
 const statements=astTexts('Build-Tests.ps1','$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.VariablePath.UserPath -eq "sources" -and $n.Right.Extent.Text -match "Get-OzlDiagnosticSources"');
 const selectors=astTexts('Build-Tests.ps1','$n -is [Management.Automation.Language.IfStatementAst] -and $n.Extent.Text -match "CoreReleaseTool.cs"');
 for(const entry of ['PZ3D.DiagnosticProgram','PZ3D.CoreReleaseTool','PZ3D.CorePackageFixtureTests']){
 const r=ps('. '+q(path.join(root,'build/ProductionSources.ps1'))+'; $project='+q(project)+';$Entry='+q(entry)+';'+statements.concat(selectors).join(';')+'; $sources | ConvertTo-Json -Compress');
 assert.equal(r.status,0,r.stderr);const names=JSON.parse(r.stdout).map(p=>path.basename(p));
 assert.equal(names.filter(n=>n==='CoreReleaseTool.cs').length,entry==='PZ3D.CoreReleaseTool'?1:0);
 assert.equal(names.length,entry==='PZ3D.CoreReleaseTool'?240:239);assert.equal(new Set(names).size,names.length);
 }
});
test('direct package and native fixture compilers use diagnostic resolver and symbol',()=>{
 for(const [file,count] of [['Test-CorePackageBuild.ps1',2],['Test-CoreNativeUpdate.ps1',3]]){
 const commands=astTexts(file,'$n -is [Management.Automation.Language.CommandAst] -and $n.Extent.Text -match "/target:"');
 assert.equal(commands.length,count);
 for(const c of commands){assert.match(c,/\/define:OZL_DIAGNOSTICS/);assert.match(c,/\$(?:sources|currentFiles)\b/);assert.doesNotMatch(c,/Get-ChildItem/);}
 const script=read(file);assert.match(script,/\. \(Join-Path \$PSScriptRoot 'build\\ProductionSources.ps1'\)/);
 const assignments=astTexts(file,'$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.VariablePath.UserPath -eq "sources"');
 assert.equal(assignments.length,1);
 const r=ps('. '+q(path.join(root,'build/ProductionSources.ps1'))+';$project='+q(project)+';'+assignments[0]+';$sources | ConvertTo-Json -Compress');
 assert.equal(r.status,0,r.stderr);assert.deepEqual(JSON.parse(r.stdout).sort(),sources(project,true).sort());
 }
});
test('negative native fixture replaces only PlaytimeTracker in diagnostic sources',()=>{
 const assignments=astTexts('Test-CoreNativeUpdate.ps1','$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.VariablePath.UserPath -eq "currentFiles"');
 const r=ps('. '+q(path.join(root,'build/ProductionSources.ps1'))+';$project='+q(project)+';$sources=@(Get-OzlDiagnosticSources -ProjectDirectory $project);'+assignments[0]+';$currentFiles | ConvertTo-Json -Compress');
 assert.equal(r.status,0,r.stderr);assert.deepEqual(JSON.parse(r.stdout).sort(),sources(project,true).filter(p=>path.basename(p)!=='PlaytimeTracker.cs').sort());
 const commands=astTexts('Test-CoreNativeUpdate.ps1','$n -is [Management.Automation.Language.CommandAst] -and $n.Extent.Text -match "/target:" -and $n.Extent.Text -match "currentFiles"');
 assert.equal(commands.length,1);assert.match(commands[0],/\$currentFiles \$oldFile/);
});
test('legacy sandbox copy manifest includes source resolver and dependency authentication support',()=>{
 const loops=astTexts('Test-LegacySetupBuild.ps1','$n -is [Management.Automation.Language.ForEachStatementAst] -and $n.Extent.Text -match "Build-Setup.ps1"');
 assert.equal(loops.length,1);
 const r=ps('function Copy-Input([string]$Relative){$Relative};'+loops[0]+'');
 assert.equal(r.status,0,r.stderr);const names=r.stdout.trim().split(/\r?\n/);
 for(const name of ['build\\ProductionSources.ps1','build\\DependencyIntegrity.ps1','build\\dependencies.lock.json']){
 assert.ok(names.includes(name),'sandbox omits '+name);assert.ok(fs.statSync(path.join(root,name)).isFile());
 }
});

