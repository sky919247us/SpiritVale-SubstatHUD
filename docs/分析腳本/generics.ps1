$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll')
$types = $asm.MainModule.GetTypes()

Write-Output '=== EquipSubstatRuntime (full generic types) ==='
foreach ($t in $types) {
    if ($t.Name -ne 'EquipSubstatRuntime') { continue }
    foreach ($p in $t.Properties) { Write-Output ('  PROP ' + $p.PropertyType.FullName + '  ' + $p.Name) }
    foreach ($m in $t.Methods) {
        if ($m.Name -like 'get_*' -or $m.Name -like 'set_*') { continue }
        Write-Output ('  METHOD ' + $m.ReturnType.FullName + ' ' + $m.Name)
    }
}

Write-Output ''
Write-Output '=== EquipSubstatConfig ==='
foreach ($t in $types) {
    if ($t.Name -ne 'EquipSubstatConfig') { continue }
    foreach ($f in $t.Fields) {
        if ($f.Name -like 'Native*') { continue }
        Write-Output ('  FIELD ' + $f.FieldType.FullName + '  ' + $f.Name)
    }
    foreach ($p in $t.Properties) { Write-Output ('  PROP ' + $p.PropertyType.FullName + '  ' + $p.Name) }
}

Write-Output ''
Write-Output '=== Formula: substat-related method visibility ==='
foreach ($t in $types) {
    if ($t.Name -ne 'Formula') { continue }
    foreach ($m in $t.Methods) {
        if ($m.Name -notmatch 'Substat|Scaled') { continue }
        $vis = 'private'
        if ($m.IsPublic) { $vis = 'PUBLIC' }
        $pl = New-Object System.Collections.ArrayList
        foreach ($pp in $m.Parameters) { [void]$pl.Add($pp.ParameterType.FullName + ' ' + $pp.Name) }
        Write-Output ('  ' + $vis + ' ' + $m.ReturnType.Name + ' ' + $m.Name + '(' + ($pl -join ', ') + ')')
    }
    foreach ($p in $t.Properties) {
        if ($p.Name -match 'Substat') { Write-Output ('  PROP ' + $p.PropertyType.FullName + ' ' + $p.Name) }
    }
}

Write-Output ''
Write-Output '=== StatData / ScaledValue ==='
foreach ($t in $types) {
    if ($t.Name -ne 'ScaledValue') { continue }
    foreach ($p in $t.Properties) { Write-Output ('  ScaledValue.PROP ' + $p.PropertyType.FullName + '  ' + $p.Name) }
}
