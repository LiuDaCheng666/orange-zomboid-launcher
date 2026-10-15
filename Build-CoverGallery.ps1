param([Parameter(Mandatory=$true)][string]$OutputHtml)
$ErrorActionPreference='Stop'
$template=Join-Path $PSScriptRoot 'Orange3DTool\Web\index.html'
$output=[IO.Path]::GetFullPath($OutputHtml)
if($output -eq [IO.Path]::GetFullPath($template)){throw 'Choose a staged output HTML; the source template must remain small.'}
$html=[IO.File]::ReadAllText($template)
$marker='<!-- HOME_SCENE_DATA -->'
if(([regex]::Matches($html,[regex]::Escape($marker))).Count -ne 1){throw 'Expected exactly one gallery insertion marker.'}
$entries=foreach($id in @('crossroads','riverside','safehouse')){
 $bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot "docs\assets\cover-candidates\$id.png"))
 '"'+$id+'":"data:image/png;base64,'+[Convert]::ToBase64String($bytes)+'"'
}
$data='<script id="home-scene-data" type="application/json">{'+($entries -join ',')+'}</script>'
$result=$html.Replace($marker,$data)
if([Text.Encoding]::UTF8.GetByteCount($result) -gt 67108864){throw 'Gallery exceeds the signed single-file size limit.'}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
[IO.File]::WriteAllText($output,$result,[Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $output | Select-Object FullName,Length
