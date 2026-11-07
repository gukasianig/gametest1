param([Parameter(Mandatory=$true)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'docs/art/WorkshopTree/ASSET_MANIFEST.json') | ConvertFrom-Json
$destination = Join-Path $taskRoot 'Assets/Resources/UI/Tree'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($asset in $manifest.assets) {
    $source = [Drawing.Bitmap]::new((Join-Path $SourceRoot $asset.sourceFile))
    $target = [Drawing.Bitmap]::new($asset.width, $asset.height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($target)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        if ($asset.transparent) {
            $bounds = [Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height)
            $locked = $source.LockBits($bounds, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $bytes = [byte[]]::new([Math]::Abs($locked.Stride) * $source.Height)
                [Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $bytes, 0, $bytes.Length)
                $left = $source.Width; $top = $source.Height; $right = 0; $bottom = 0
                for ($y = 0; $y -lt $source.Height; $y++) {
                    for ($x = 0; $x -lt $source.Width; $x++) {
                        if ($bytes[$y * $locked.Stride + $x * 4 + 3] -gt 8) {
                            $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
                            $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
                        }
                    }
                }
            } finally { $source.UnlockBits($locked) }
            if ($right -le $left) { throw "Empty icon: $($asset.id)" }
            $left = [Math]::Max(0, $left - 8); $top = [Math]::Max(0, $top - 8)
            $right = [Math]::Min($source.Width - 1, $right + 8); $bottom = [Math]::Min($source.Height - 1, $bottom + 8)
            $crop = [Drawing.Rectangle]::new($left, $top, $right - $left + 1, $bottom - $top + 1)
            $ratio = [Math]::Min(204.0 / $crop.Width, 204.0 / $crop.Height)
            $drawWidth = [int][Math]::Round($crop.Width * $ratio); $drawHeight = [int][Math]::Round($crop.Height * $ratio)
            $draw = [Drawing.Rectangle]::new([int](($asset.width - $drawWidth) / 2), [int](($asset.height - $drawHeight) / 2), $drawWidth, $drawHeight)
            $graphics.DrawImage($source, $draw, $crop, [Drawing.GraphicsUnit]::Pixel)
        } else {
            $graphics.DrawImage($source, 0, 0, $asset.width, $asset.height)
        }
        $target.Save((Join-Path $destination ($asset.id + '.png')), [Drawing.Imaging.ImageFormat]::Png)
        if ($asset.transparent -and $target.GetPixel(0, 0).A -ne 0) { throw "Opaque corner: $($asset.id)" }
        Write-Output "$($asset.id): $($asset.width)x$($asset.height), transparent=$($asset.transparent)"
    } finally { $graphics.Dispose(); $target.Dispose(); $source.Dispose() }
}
