$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$png=Join-Path $PSScriptRoot 'Assets/orange-app.png'
$ico=Join-Path $PSScriptRoot 'Assets/orange-app.ico'
$source=[Drawing.Image]::FromFile($png)
$sizes=@(16,32,48,64,128,256)
$frames=@()
try {
 foreach($size in $sizes){
  $bitmap=New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  $graphics.Clear([Drawing.Color]::Transparent)
  $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
  $graphics.DrawImage($source,0,0,$size,$size)
  $memory=New-Object IO.MemoryStream
  $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
  $frames+=,@{Size=$size;Bytes=$memory.ToArray()}
  $memory.Dispose();$graphics.Dispose();$bitmap.Dispose()
 }
 $output=[IO.File]::Create($ico)
 $writer=New-Object IO.BinaryWriter($output)
 try {
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
  $offset=6+16*$frames.Count
  foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
  foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
 } finally {$writer.Dispose();$output.Dispose()}
} finally {$source.Dispose()}
Write-Output "Multi-size PNG ICO saved: $ico"
