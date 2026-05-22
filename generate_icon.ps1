# Builds the PowerAim multi-resolution .ico from a user-provided source PNG.
# Run via: powershell -ExecutionPolicy Bypass -File ./generate_icon.ps1 [-Source <path>]

param(
    [string]$Source = "C:\Users\User\Downloads\poweraim.png"
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) {
    Write-Error "Source image not found: $Source"
    exit 1
}

function Resize-ToSize {
    param([System.Drawing.Bitmap]$src, [int]$size)
    $bmp = New-Object System.Drawing.Bitmap -ArgumentList $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality= [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Preserve aspect ratio with transparent padding (if the source is non-square)
    $srcRatio = $src.Width / [double]$src.Height
    if ([Math]::Abs($srcRatio - 1.0) -lt 0.001) {
        $g.DrawImage($src, 0, 0, $size, $size)
    }
    else {
        if ($srcRatio -gt 1) {
            $w = $size
            $h = [int]([Math]::Round($size / $srcRatio))
            $x = 0
            $y = [int](($size - $h) / 2)
        }
        else {
            $h = $size
            $w = [int]([Math]::Round($size * $srcRatio))
            $x = [int](($size - $w) / 2)
            $y = 0
        }
        $g.DrawImage($src, $x, $y, $w, $h)
    }
    $g.Dispose()
    return $bmp
}

function Save-Ico {
    param([System.Drawing.Bitmap[]]$images, [string]$path)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter -ArgumentList $ms

    $count = $images.Count
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$count)

    $pngs = @()
    foreach ($img in $images) {
        $pms = New-Object System.IO.MemoryStream
        $img.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += ,$pms.ToArray()
        $pms.Dispose()
    }

    $offset = 6 + $count * 16
    for ($i = 0; $i -lt $count; $i++) {
        $img = $images[$i]
        $w = $img.Width
        $h = $img.Height
        $bw.Write([byte]($(if ($w -ge 256) { 0 } else { $w })))
        $bw.Write([byte]($(if ($h -ge 256) { 0 } else { $h })))
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$pngs[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }

    foreach ($png in $pngs) { $bw.Write($png) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Load source as a Bitmap (note: open via FileStream so the file isn't locked)
$srcBytes = [System.IO.File]::ReadAllBytes($Source)
$srcStream = New-Object System.IO.MemoryStream(,$srcBytes)
$srcBitmap = [System.Drawing.Bitmap]::FromStream($srcStream)
Write-Host ("Source: {0}  ({1}x{2})" -f $Source, $srcBitmap.Width, $srcBitmap.Height)

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += (Resize-ToSize -src $srcBitmap -size $s) }

$icoPath1 = Join-Path $scriptDir "PowerAim/PowerAim.ico"
$icoPath2 = Join-Path $scriptDir "Launcher/PowerAim.ico"
$pngPath  = Join-Path $scriptDir "readme_assets/PowerAim-icon-256.png"

Save-Ico -images $bitmaps -path $icoPath1
Save-Ico -images $bitmaps -path $icoPath2
$bitmaps[0].Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($b in $bitmaps) { $b.Dispose() }
$srcBitmap.Dispose()
$srcStream.Dispose()

Write-Host "Wrote $icoPath1"
Write-Host "Wrote $icoPath2"
Write-Host "Wrote $pngPath"
