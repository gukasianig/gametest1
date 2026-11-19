param([switch]$AllowPartial)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location, [Drawing.Rectangle].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
public static class UpgradeImageExport
{
    public static Rectangle Bounds(Bitmap image)
    {
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            if (image.GetPixel(x, y).A <= 8) continue;
            left = Math.Min(left, x); right = Math.Max(right, x);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new Exception("Empty source");
        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }
    public static int[] AlphaCounts(Bitmap image)
    {
        int clear = 0, visible = 0;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            byte a = image.GetPixel(x, y).A;
            if (a == 0) clear++;
            if (a > 128) visible++;
        }
        return new[] { clear, visible };
    }
}
'@
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'style-e-jobs.json') -Raw | ConvertFrom-Json
$rawDir = Join-Path $projectRoot 'docs/art/Upgrades/StyleE'
$targetDir = Join-Path $projectRoot 'Assets/Resources/UI/Upgrades'
$backupDir = Join-Path $projectRoot 'Logs/upgrade-icons-before-e'
foreach ($dir in @($rawDir, $targetDir, $backupDir)) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
$pending = @($manifest.jobs | Where-Object { !$_.source })
if ($pending.Count -gt 0 -and !$AllowPartial) { throw "Missing generated sources: $($pending.name -join ', ')" }
$jobs = @($manifest.jobs | Where-Object { $_.source })
foreach ($job in $jobs)
{
    if ($job.name -notmatch '^[A-Za-z]+$') { throw 'Invalid icon name' }
    $raw = Join-Path $rawDir ($job.name + '.png')
    $target = Join-Path $targetDir ($job.name + '.png')
    $backup = Join-Path $backupDir ($job.name + '.png')
    if ((Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $target -Destination $backup }
    $sourcePath = $job.source
    if (![IO.Path]::IsPathRooted($sourcePath)) { $sourcePath = Join-Path $projectRoot $sourcePath }
    if ([IO.Path]::GetFullPath($sourcePath) -ne [IO.Path]::GetFullPath($raw)) { Copy-Item -LiteralPath $sourcePath -Destination $raw -Force }
    $source = [Drawing.Bitmap]::FromFile($raw)
    $output = New-Object Drawing.Bitmap(256, 256, ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $graphics = [Drawing.Graphics]::FromImage($output)
    try
    {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        # Export only: normalize transparent padding without altering the generated artwork.
        $bounds = [UpgradeImageExport]::Bounds($source)
        $scale = 208.0 / [Math]::Max($bounds.Width, $bounds.Height)
        $width = [int][Math]::Round($bounds.Width * $scale)
        $height = [int][Math]::Round($bounds.Height * $scale)
        $destination = New-Object Drawing.Rectangle(([int]((256 - $width) / 2)), ([int]((256 - $height) / 2)), $width, $height)
        $graphics.DrawImage($source, $destination, $bounds, [Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()
        $output.Save($target, [Drawing.Imaging.ImageFormat]::Png)
        $counts = [UpgradeImageExport]::AlphaCounts($output)
        $transparent = $counts[0]
        $visible = $counts[1]
        if ($transparent -lt 10000 -or $visible -lt 1500) { throw "Invalid alpha/content: $($job.name) transparent=$transparent visible=$visible" }
        Write-Output "$($job.name): 256x256 RGBA, transparent=$transparent visible=$visible"
    }
    finally { $graphics.Dispose(); $source.Dispose(); $output.Dispose() }
}
# Two sizes on the same sheet expose loss of legibility at actual UI scale.
$columns = 7
$cellWidth = 190
$cellHeight = 170
$rows = [int][Math]::Ceiling($jobs.Count / [double]$columns)
$sheet = New-Object Drawing.Bitmap(($columns * $cellWidth), ($rows * $cellHeight))
$g = [Drawing.Graphics]::FromImage($sheet)
$font = New-Object Drawing.Font('Arial', 10)
try
{
    $g.Clear([Drawing.Color]::FromArgb(25, 29, 32))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for ($i = 0; $i -lt $jobs.Count; $i++)
    {
        $x = ($i % $columns) * $cellWidth
        $y = [Math]::Floor($i / $columns) * $cellHeight
        $icon = [Drawing.Bitmap]::FromFile((Join-Path $targetDir ($jobs[$i].name + '.png')))
        try
        {
            $g.DrawImage($icon, [int]($x + 4), [int]($y + 8), 112, 112)
            $g.DrawImage($icon, [int]($x + 118), [int]($y + 50), 64, 64)
            $g.DrawString($jobs[$i].name, $font, [Drawing.Brushes]::White, [single]($x + 5), [single]($y + 132))
        }
        finally { $icon.Dispose() }
    }
    $sheet.Save((Join-Path $rawDir 'preview.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally { $g.Dispose(); $font.Dispose(); $sheet.Dispose() }
