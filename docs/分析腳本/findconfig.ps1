$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asmPath = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmPath)
$types = $asm.MainModule.GetTypes()

Write-Output '===== 回傳 EquipConfig 的方法 ====='
foreach ($t in $types) {
    foreach ($m in $t.Methods) {
        if ($m.ReturnType.Name -eq 'EquipConfig') {
            $pl = New-Object System.Collections.ArrayList
            foreach ($pp in $m.Parameters) { [void]$pl.Add($pp.ParameterType.Name + ' ' + $pp.Name) }
            $st = ''
            if ($m.IsStatic) { $st = 'static ' }
            Write-Output ('  ' + $t.FullName + ' :: ' + $st + $m.Name + '(' + ($pl -join ', ') + ')')
        }
    }
}

Write-Output ''
Write-Output '===== 名稱含 Config 的資料庫/單例型別 ====='
foreach ($t in $types) {
    if ($t.Name -match '^(ConfigManager|GameConfig|ConfigDatabase|Database|GameData|DataManager|Configs)$') {
        Write-Output ('  TYPE ' + $t.FullName)
    }
}
