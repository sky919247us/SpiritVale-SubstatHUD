$ErrorActionPreference = 'Stop'
Write-Host ''
Write-Host '================================================' -ForegroundColor Cyan
Write-Host '  SpiritVale 詞條品質快篩 HUD - 移除' -ForegroundColor Cyan
Write-Host '================================================' -ForegroundColor Cyan
Write-Host ''

function Find-Game {
    $steam = $null
    try { $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath } catch {}
    if (-not $steam) { try { $steam = (Get-ItemProperty 'HKLM:\SOFTWARE\Wow6432Node\Valve\Steam' -ErrorAction Stop).InstallPath } catch {} }
    $libs = New-Object System.Collections.ArrayList
    if ($steam) {
        [void]$libs.Add($steam)
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"(.+?)"')) {
                [void]$libs.Add($m.Groups[1].Value.Replace('\\', '\'))
            }
        }
    }
    foreach ($l in $libs) {
        $p = Join-Path $l 'steamapps\common\SpiritVale'
        if (Test-Path (Join-Path $p 'SpiritVale.exe')) { return $p }
    }
    return $null
}

$game = Find-Game
if ($game) {
    Write-Host ('自動偵測到遊戲位置：' + $game) -ForegroundColor Green
} else {
    Write-Host '找不到 SpiritVale 安裝位置。' -ForegroundColor Yellow
    Write-Host '請把遊戲資料夾路徑貼上（裡面有 SpiritVale.exe）：'
    $game = (Read-Host '路徑').Trim('"').Trim()
}

if (-not (Test-Path (Join-Path $game 'SpiritVale.exe'))) {
    Write-Host '找不到 SpiritVale.exe，取消。' -ForegroundColor Red
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}
if (Get-Process 'SpiritVale' -ErrorAction SilentlyContinue) {
    Write-Host '請先完全關閉遊戲再移除。' -ForegroundColor Red
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}

$bep     = Join-Path $game 'BepInEx'
$removed = $false

$dir = Join-Path $bep 'plugins\SpiritValeSubstatHUD'
if (Test-Path $dir) {
    Remove-Item $dir -Recurse -Force
    Write-Host '已刪除 BepInEx\plugins\SpiritValeSubstatHUD' -ForegroundColor Gray
    $removed = $true
}

$cfg = Join-Path $bep 'config\local.spiritvale.substathud.cfg'
if (Test-Path $cfg) {
    Remove-Item $cfg -Force
    Write-Host '已刪除 BepInEx\config\local.spiritvale.substathud.cfg' -ForegroundColor Gray
    $removed = $true
}

Write-Host ''
if ($removed) {
    Write-Host '詞條快篩 HUD 已完全移除。' -ForegroundColor Green
} else {
    Write-Host '沒有偵測到本 Mod，可能已經移除過了。' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '--- 其他 Mod 現況檢查 ---' -ForegroundColor Cyan
$zh = Join-Path $bep 'plugins\SpiritValeTranslate\SpiritValeTranslate.dll'
if (Test-Path $zh) {
    Write-Host '  繁體中文包：完好（未受影響）' -ForegroundColor Green
} else {
    Write-Host '  繁體中文包：未安裝' -ForegroundColor Gray
}
$others = @(Get-ChildItem (Join-Path $bep 'plugins') -Recurse -Filter *.dll -ErrorAction SilentlyContinue)
Write-Host ('  plugins 內現存插件數：' + $others.Count) -ForegroundColor Gray

Write-Host ''
Write-Host '本 Mod 從未修改遊戲本體或其他 Mod 的任何檔案，' -ForegroundColor Gray
Write-Host '移除後即完全回到安裝前的狀態。' -ForegroundColor Gray
Write-Host ''
Read-Host '按 Enter 關閉'
