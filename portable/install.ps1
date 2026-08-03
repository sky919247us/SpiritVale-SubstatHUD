$ErrorActionPreference = 'Stop'
Write-Host ''
Write-Host '================================================' -ForegroundColor Cyan
Write-Host '  SpiritVale 詞條品質快篩 HUD - 安裝' -ForegroundColor Cyan
Write-Host '================================================' -ForegroundColor Cyan
Write-Host ''

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll  = Join-Path $root 'SpiritValeSubstatHUD.dll'

if (-not (Test-Path $dll)) {
    Write-Host '找不到 SpiritValeSubstatHUD.dll。' -ForegroundColor Red
    Write-Host '請確認壓縮檔已「完整解壓縮」，且所有檔案放在同一個資料夾內。' -ForegroundColor Yellow
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}

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
    Write-Host '請把遊戲資料夾路徑貼上（Steam 右鍵遊戲 > 管理 > 瀏覽本機檔案）：'
    $game = (Read-Host '路徑').Trim('"').Trim()
}

if (-not (Test-Path (Join-Path $game 'SpiritVale.exe'))) {
    Write-Host '這個路徑裡沒有 SpiritVale.exe，安裝取消。' -ForegroundColor Red
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}
if (Get-Process 'SpiritVale' -ErrorAction SilentlyContinue) {
    Write-Host '偵測到遊戲正在執行中！請「完全關閉遊戲」後再執行一次。' -ForegroundColor Red
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}

$bep = Join-Path $game 'BepInEx'
if (-not (Test-Path (Join-Path $bep 'core\BepInEx.Unity.IL2CPP.dll'))) {
    Write-Host ''
    Write-Host '這個遊戲目錄沒有安裝 BepInEx，無法安裝本 Mod。' -ForegroundColor Red
    Write-Host ''
    Write-Host '本 Mod 需要 BepInEx 6.x（IL2CPP 版）才能運作。' -ForegroundColor Yellow
    Write-Host '若你有安裝「SpiritVale 繁體中文包」，安裝它即會一併裝好 BepInEx。' -ForegroundColor Yellow
    Write-Host '或自行前往 https://github.com/BepInEx/BepInEx 取得。' -ForegroundColor Yellow
    Write-Host ''; Read-Host '按 Enter 關閉'; return
}

$dst = Join-Path $bep 'plugins\SpiritValeSubstatHUD'
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Force -Path $dst | Out-Null }
Copy-Item $dll $dst -Force

Write-Host ''
Write-Host '安裝完成！' -ForegroundColor Green
Write-Host ('  已安裝到：' + $dst) -ForegroundColor Gray
Write-Host ''
Write-Host '啟動遊戲即生效：' -ForegroundColor Cyan
Write-Host '  ・背包與倉庫的裝備／神器名稱前會出現 ★ 記號' -ForegroundColor Gray
Write-Host '    ★★★ 金色 = 每條詞條都頂到上限' -ForegroundColor Gray
Write-Host '    ★★  橘色 = 75% 以上' -ForegroundColor Gray
Write-Host '    ★   紫色 = 50% 以上' -ForegroundColor Gray
Write-Host '  ・道具說明開頭會多一行品質總評' -ForegroundColor Gray
Write-Host ''
Write-Host '想調整標記門檻，遊戲跑過一次後編輯：' -ForegroundColor Cyan
Write-Host ('  ' + (Join-Path $bep 'config\local.spiritvale.substathud.cfg')) -ForegroundColor Gray
Write-Host ''
Write-Host '若要移除：雙擊「一鍵移除.bat」。' -ForegroundColor Gray
Write-Host '本 Mod 不會修改遊戲本體或其他 Mod 的任何檔案，移除即完全還原。' -ForegroundColor Gray
Write-Host ''
Read-Host '按 Enter 關閉'
