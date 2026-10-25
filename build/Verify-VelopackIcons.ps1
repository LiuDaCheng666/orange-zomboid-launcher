function Test-OzlVelopackIcons {
 param([string]$ReleaseDirectory,[string]$Icon,[string]$Version)
 Add-Type -AssemblyName System.Drawing
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $expected=[Drawing.Icon]::ExtractAssociatedIcon([IO.Path]::GetFullPath($Icon))
 $expectedBitmap=$expected.ToBitmap()
 $pixelHash={param($bitmap) $stream=[IO.MemoryStream]::new();$sha=[Security.Cryptography.SHA256]::Create();try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);[BitConverter]::ToString($sha.ComputeHash($stream.ToArray())).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}}
 try{$expectedHash=& $pixelHash $expectedBitmap}finally{$expectedBitmap.Dispose();$expected.Dispose()}
 $scratch=Join-Path ([IO.Path]::GetFullPath($ReleaseDirectory)) ('.icon-check-'+[Guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($scratch)|Out-Null
 $results=@();$failures=@()
 $check={param($path,$label)
  $actual=[Drawing.Icon]::ExtractAssociatedIcon($path)
  try{$bitmap=$actual.ToBitmap();try{$hash=& $pixelHash $bitmap}finally{$bitmap.Dispose()}}finally{$actual.Dispose()}
  [pscustomobject]@{entry=$label;sha256=$hash;expectedSha256=$expectedHash;matches=($hash -ceq $expectedHash)}
 }
 try{
  $results+=& $check ([IO.Path]::GetFullPath((Join-Path $ReleaseDirectory 'OrangeZomboidLauncher-win-Setup.exe'))) 'setup-wrapper'
  $wrapper=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $ReleaseDirectory 'OrangeZomboidLauncher-win-Setup.exe')))
  $officialStream=$wrapper.GetManifestResourceStream('Setup.Official.exe')
  if($null -eq $officialStream){throw 'Final setup wrapper is missing its official installer resource'}
  $officialSetup=Join-Path $scratch 'official-setup.exe'
  $officialOutput=[IO.File]::Create($officialSetup)
  try{$officialStream.CopyTo($officialOutput)}finally{$officialOutput.Dispose();$officialStream.Dispose()}
  $results+=& $check $officialSetup 'embedded-official-setup'
  foreach($item in @(
   @{name='OrangeZomboidLauncher-win-Portable.zip';entries=@('Update.exe','橙子启动器.exe','current/橙子启动器.exe')},
   @{name="OrangeZomboidLauncher-$Version-full.nupkg";entries=@('lib/app/Squirrel.exe','lib/app/橙子启动器_ExecutionStub.exe','lib/app/橙子启动器.exe')}
  )){
   $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory $item.name))
   try{
    foreach($name in $item.entries){
     $entryMatches=@($archive.Entries|Where-Object {$_.FullName -ceq $name})
     if($entryMatches.Count -ne 1){throw "Missing or duplicate icon-check entry: $name"}
     $path=Join-Path $scratch ([Guid]::NewGuid().ToString('N')+'.exe')
     $iconInput=$entryMatches[0].Open();$iconOutput=[IO.File]::Create($path)
     try{$iconInput.CopyTo($iconOutput)}finally{$iconOutput.Dispose();$iconInput.Dispose()}
     $results+=& $check $path ($item.name+':'+$name)
    }
   }finally{$archive.Dispose()}
  }
  $failures=@($results|Where-Object {-not $_.matches})
  if($failures.Count){throw ('Packaged executable uses an unexpected icon: '+(($failures|ForEach-Object {$_.entry}) -join ', '))}
  $results
 }finally{
  # Only direct children created by this invocation are removed; never recurse.
  foreach($file in [IO.Directory]::GetFiles($scratch)){[IO.File]::Delete($file)}
  [IO.Directory]::Delete($scratch)
 }
}
