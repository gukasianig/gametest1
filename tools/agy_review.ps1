param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [Parameter(Mandatory = $true)][string]$Subject,
    [string]$Notes = ""
)
# Antigravity (agy) как критик картинок: читает PNG в папке $Dir, печатает до 8 правок. Режим plan = только чтение.
# Пример: powershell -File tools/agy_review.ps1 -Dir docs/art/Items/Turret -Subject Turret
$agy = Join-Path $env:LOCALAPPDATA "agy\bin\agy.exe"
if (-not (Test-Path $agy)) { Write-Error "agy не установлен: $agy"; exit 1 }
$full = (Resolve-Path $Dir).Path
$shots = Get-ChildItem $full -Filter *.png | Select-Object -ExpandProperty Name
if (-not $shots) { Write-Error "Нет PNG в $full"; exit 1 }
$list = ($shots -join ", ")
$prompt = "Ты арт-критик low-poly игрового предмета ""$Subject"" для top-down выживалки на ПК. Прочитай картинки в папке: $list. Оцени пропорции, силуэт, читаемость с большой дистанции, детали, палитру. $Notes Ответь строго на русском, максимум 8 пунктов в формате: ""что не так -> конкретная правка (размер/форма/цвет)"". Без вступления и без похвалы. Ничего не создавай и не меняй."
Push-Location $full
try { & $agy --mode plan -p $prompt } finally { Pop-Location }
