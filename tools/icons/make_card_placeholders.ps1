# Временные иконки новых карточек (19.11.2026), собранные из готовых иконок стиля E: перекраска и сочетания.
# Настоящие иконки генерируются по tools/icons/style-e-cards-2026-11-19.json и заменяют эти файлы (GUID сохраняется).
# Запуск: powershell -ExecutionPolicy Bypass -File tools/icons/make_card_placeholders.ps1 [-Force]
param([switch]$Force)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dir = Join-Path $root 'Assets/Resources/UI/Upgrades'
$metaTemplate = Join-Path $dir 'Damage.png.meta'

function Load([string]$name) { return [Drawing.Bitmap]::FromFile((Join-Path $dir ($name + '.png'))) }

# Смешивает цвета с оттенком (s = сила 0..1), сохраняя светотень и прозрачность.
function Tinted([Drawing.Bitmap]$src, [double]$r, [double]$g, [double]$b, [double]$s)
{
    $l = @(0.3, 0.59, 0.11)
    $rows = New-Object 'single[][]' 5
    for ($i = 0; $i -lt 3; $i++)
    {
        $row = New-Object 'single[]' 5
        $row[0] = [single]($s * $l[$i] * $r * 1.25 + $(if ($i -eq 0) { 1 - $s } else { 0 }))
        $row[1] = [single]($s * $l[$i] * $g * 1.25 + $(if ($i -eq 1) { 1 - $s } else { 0 }))
        $row[2] = [single]($s * $l[$i] * $b * 1.25 + $(if ($i -eq 2) { 1 - $s } else { 0 }))
        $rows[$i] = $row
    }
    $rows[3] = [single[]]@(0, 0, 0, 1, 0)
    $rows[4] = [single[]]@(0, 0, 0, 0, 1)
    $attr = New-Object Drawing.Imaging.ImageAttributes
    $attr.SetColorMatrix((New-Object Drawing.Imaging.ColorMatrix(,$rows)))
    $out = New-Object Drawing.Bitmap(256, 256, ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g2 = [Drawing.Graphics]::FromImage($out)
    $g2.DrawImage($src, (New-Object Drawing.Rectangle(0, 0, 256, 256)), 0, 0, $src.Width, $src.Height, [Drawing.GraphicsUnit]::Pixel, $attr)
    $g2.Dispose()
    return $out
}

# Рисует картинку с центром в (cx, cy), масштабом, поворотом и прозрачностью.
function Put([Drawing.Graphics]$g, [Drawing.Image]$img, [double]$cx, [double]$cy, [double]$scale, [double]$angle = 0, [double]$alpha = 1)
{
    $state = $g.Save()
    $g.TranslateTransform([single]$cx, [single]$cy)
    $g.RotateTransform([single]$angle)
    $size = 256 * $scale
    $attr = New-Object Drawing.Imaging.ImageAttributes
    $cm = New-Object Drawing.Imaging.ColorMatrix
    $cm.Matrix33 = [single]$alpha
    $attr.SetColorMatrix($cm)
    $rect = New-Object Drawing.Rectangle([int](-$size / 2), [int](-$size / 2), [int]$size, [int]$size)
    $g.DrawImage($img, $rect, 0, 0, $img.Width, $img.Height, [Drawing.GraphicsUnit]::Pixel, $attr)
    $g.Restore($state)
}

function Rings([Drawing.Graphics]$g, [Drawing.Color]$color, [double[]]$radii, [double]$width)
{
    foreach ($r in $radii)
    {
        $dark = New-Object Drawing.Pen([Drawing.Color]::FromArgb(150, 20, 14, 8), [single]($width + 4))
        $pen = New-Object Drawing.Pen($color, [single]$width)
        $box = New-Object Drawing.RectangleF([single](128 - $r), [single](128 - $r * 0.62 + 18), [single]($r * 2), [single]($r * 1.24))
        $g.DrawEllipse($dark, $box)
        $g.DrawEllipse($pen, $box)
    }
}

function Bolt([Drawing.Graphics]$g)
{
    $pts = [Drawing.PointF[]]@(
        (New-Object Drawing.PointF(168, 26)), (New-Object Drawing.PointF(118, 122)), (New-Object Drawing.PointF(150, 122)),
        (New-Object Drawing.PointF(116, 226)), (New-Object Drawing.PointF(200, 98)), (New-Object Drawing.PointF(164, 98)),
        (New-Object Drawing.PointF(196, 26)))
    $g.FillPolygon((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255, 255, 236, 140))), $pts)
    $g.DrawPolygon((New-Object Drawing.Pen([Drawing.Color]::FromArgb(255, 40, 26, 10), 5)), $pts)
}

function Save([string]$name, [scriptblock]$draw)
{
    $target = Join-Path $dir ($name + '.png')
    if ((Test-Path -LiteralPath $target) -and !$Force) { Write-Output "skip $name (exists)"; return }
    $bmp = New-Object Drawing.Bitmap(256, 256, ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([Drawing.Color]::Transparent)
    & $draw $g
    $g.Dispose()
    $bmp.Save($target, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $meta = $target + '.meta'
    if (!(Test-Path -LiteralPath $meta))
    {
        $guid = [guid]::NewGuid().ToString('N')
        $text = [IO.File]::ReadAllText($metaTemplate) -replace 'guid: [0-9a-f]{32}', ('guid: ' + $guid)
        [IO.File]::WriteAllText($meta, $text)
    }
    Write-Output "made $name"
}

$src = @{}
foreach ($n in 'DashDistance','DashCooldown','Armor','AttackSpeed','ChestXp','EliteDamage','ExplosiveBullets','CritPower','MineCount',
    'MaxHealth','Luck','ExperienceGain','CursedHaste','Pierce','CritChance','ExtraPellets','GrenadeRadius') { $src[$n] = Load $n }
$cyan = Tinted $src.DashDistance 0.5 0.9 1 0.8

Save 'Dodge' { param($g) Put $g $cyan 92 128 0.8 0 0.25; Put $g $cyan 112 128 0.8 0 0.45; Put $g $src.DashDistance 142 128 0.8 }
Save 'ProjectileGuard' { param($g) Put $g $src.Armor 146 140 0.72; Put $g $src.AttackSpeed 74 78 0.5 35 }
Save 'DashShield' { param($g) Put $g $src.DashCooldown 116 116 0.74; Put $g $src.Armor 180 180 0.42 }
Save 'Trophy' { param($g) Put $g $src.ChestXp 124 146 0.72; Put $g $src.EliteDamage 176 70 0.42 }
Save 'BurnRounds' { param($g) Put $g (Tinted $src.ExplosiveBullets 1 0.35 0.08 0.6) 128 128 0.84 }
Save 'PoisonRounds' { param($g) Put $g (Tinted $src.ExplosiveBullets 0.35 1 0.25 0.65) 128 128 0.84 }
Save 'FrostRounds' { param($g) Put $g (Tinted $src.ExplosiveBullets 0.45 0.85 1 0.7) 128 128 0.84 }
Save 'ShockRounds' { param($g) Put $g (Tinted $src.ExplosiveBullets 0.5 0.6 1 0.6) 112 140 0.78; Bolt $g }
Save 'SplitShot' { param($g) Put $g $src.CritPower 96 140 0.62 -20; Put $g $src.AttackSpeed 178 74 0.36 -38; Put $g $src.AttackSpeed 200 128 0.36 0; Put $g $src.AttackSpeed 178 182 0.36 38 }
Save 'Retaliate' { param($g) Rings $g ([Drawing.Color]::FromArgb(230, 255, 150, 40)) @(104, 84) 6; Put $g $src.Armor 128 120 0.66 }
Save 'PulseAura' { param($g) Rings $g ([Drawing.Color]::FromArgb(230, 90, 220, 255)) @(108, 86, 64) 6; Put $g $src.MineCount 128 126 0.56 }
Save 'ChargedShot' { param($g) Put $g (Tinted $src.CritPower 0.5 0.9 1 0.55) 128 128 0.9 }
Save 'LastChance' { param($g) Rings $g ([Drawing.Color]::FromArgb(240, 255, 214, 90)) @(70) 8; Put $g (Tinted $src.MaxHealth 1 0.85 0.4 0.3) 128 140 0.74 }
Save 'Gamble' { param($g) Put $g (Tinted $src.Luck 0.95 0.15 0.2 0.75) 128 128 0.86 }
Save 'TimeGreed' { param($g) Put $g (Tinted $src.ExperienceGain 0.95 0.2 0.25 0.7) 118 128 0.8; Put $g $src.CursedHaste 186 180 0.4 }
Save 'CritPierce' { param($g) Put $g $src.Pierce 140 140 0.76; Put $g $src.CritChance 70 70 0.42 }
Save 'BurningShot' { param($g) Put $g (Tinted $src.ExtraPellets 1 0.4 0.1 0.55) 128 128 0.86 }
Save 'BlastDash' { param($g) Put $g $src.DashDistance 110 110 0.72; Put $g $src.GrenadeRadius 178 182 0.46 }
foreach ($b in $src.Values) { $b.Dispose() }
