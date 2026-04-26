Add-Type -AssemblyName System.Drawing

$pngPath = 'O:\App\Obsidian\KokonoeAssistant\Logo\Logo.png'
$outPng  = 'O:\App\Obsidian\KokonoeAssistant\Logo\Logo_icon.png'
$outIco  = 'O:\App\Obsidian\KokonoeAssistant\Logo\Logo_icon.ico'

$src = [System.Drawing.Image]::FromFile($pngPath)
Write-Host ("Source: " + $src.Width + "x" + $src.Height)

# Crop: top square 1024x1024 (face + ears)
$cropSize = $src.Width
$cropRect = [System.Drawing.Rectangle]::new(0, 0, $cropSize, $cropSize)

$cropped = [System.Drawing.Bitmap]::new($cropSize, $cropSize)
$g = [System.Drawing.Graphics]::FromImage($cropped)
$g.DrawImage($src, [System.Drawing.Rectangle]::new(0,0,$cropSize,$cropSize), $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$src.Dispose()

# Save 512x512 PNG for WPF window Icon
$icon512 = [System.Drawing.Bitmap]::new(512, 512)
$g2 = [System.Drawing.Graphics]::FromImage($icon512)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.DrawImage($cropped, 0, 0, 512, 512)
$g2.Dispose()
$icon512.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
$icon512.Dispose()
Write-Host "Saved 512x512 PNG"

# Build ICO with multiple sizes
$sizes = @(256, 128, 64, 48, 32, 16)
$imgData = @()
foreach ($s in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s)
    $g3 = [System.Drawing.Graphics]::FromImage($bmp)
    $g3.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g3.DrawImage($cropped, 0, 0, $s, $s)
    $g3.Dispose()
    $ms2 = New-Object System.IO.MemoryStream
    $bmp.Save($ms2, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgData += ,$ms2.ToArray()
    $bmp.Dispose()
    $ms2.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

# ICO header
$writer.Write([int16]0)
$writer.Write([int16]1)
$writer.Write([int16]$sizes.Count)

# Calculate data offset: header(6) + directory entries(16 each)
$dataOffset = 6 + $sizes.Count * 16
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $w = if ($s -eq 256) { 0 } else { $s }
    $h = if ($s -eq 256) { 0 } else { $s }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int32]$imgData[$i].Length)
    $writer.Write([int32]$dataOffset)
    $dataOffset += $imgData[$i].Length
}

foreach ($d in $imgData) { $writer.Write($d) }
$writer.Flush()

[System.IO.File]::WriteAllBytes($outIco, $ms.ToArray())
Write-Host ("ICO saved: " + $ms.Length + " bytes, " + $sizes.Count + " sizes")
$ms.Dispose()
$cropped.Dispose()
