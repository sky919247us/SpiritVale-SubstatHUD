$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll')
$types = $asm.MainModule.GetTypes()

Write-Output '=== Hotkey enum values ==='
foreach ($t in $types) {
    if ($t.Name -ne 'Hotkey') { continue }
    foreach ($f in $t.Fields) {
        if ($f.Name -eq 'value__') { continue }
        Write-Output ('  ' + $f.Name + ' = ' + $f.Constant)
    }
}

Write-Output ''
Write-Output '=== Types declaring GetKey / GetKeyDown ==='
foreach ($t in $types) {
    foreach ($m in $t.Methods) {
        if ($m.Name -notmatch '^(GetKey|GetKeyDown|GetKeyUp|GetKeyDisplayName)$') { continue }
        $pl = New-Object System.Collections.ArrayList
        foreach ($pp in $m.Parameters) { [void]$pl.Add($pp.ParameterType.Name + ' ' + $pp.Name) }
        $st = ''
        if ($m.IsStatic) { $st = 'static ' }
        Write-Output ('  ' + $t.FullName + ' :: ' + $st + $m.ReturnType.Name + ' ' + $m.Name + '(' + ($pl -join ', ') + ')')
    }
}

Write-Output ''
Write-Output '=== UIItemPopup / UIInventoryItem compare-related members ==='
foreach ($t in $types) {
    if ($t.Name -notmatch '^(UIItemPopup|UIPopup)$') { continue }
    Write-Output ('  -- ' + $t.FullName)
    foreach ($m in $t.Methods) {
        if ($m.Name -like 'get_*' -or $m.Name -like 'set_*') { continue }
        $pl = New-Object System.Collections.ArrayList
        foreach ($pp in $m.Parameters) { [void]$pl.Add($pp.ParameterType.Name + ' ' + $pp.Name) }
        Write-Output ('     ' + $m.ReturnType.Name + ' ' + $m.Name + '(' + ($pl -join ', ') + ')')
    }
}
