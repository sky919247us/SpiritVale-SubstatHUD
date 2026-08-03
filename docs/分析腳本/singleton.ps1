$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asmPath = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmPath)
$types = $asm.MainModule.GetTypes()

Write-Output 'GameServerRuntime static members:'
foreach ($t in $types) {
    if ($t.Name -ne 'GameServerRuntime') { continue }
    Write-Output ('  base = ' + $t.BaseType.FullName)
    foreach ($f in $t.Fields) {
        if ($f.Name -like 'Native*') { continue }
        if (-not $f.IsStatic) { continue }
        Write-Output ('  STATIC FIELD ' + $f.FieldType.Name + ' ' + $f.Name)
    }
    foreach ($p in $t.Properties) {
        Write-Output ('  PROP ' + $p.PropertyType.Name + ' ' + $p.Name)
    }
    foreach ($m in $t.Methods) {
        if (-not $m.IsStatic) { continue }
        if ($m.Name -like '.*') { continue }
        $pl = New-Object System.Collections.ArrayList
        foreach ($pp in $m.Parameters) { [void]$pl.Add($pp.ParameterType.Name) }
        Write-Output ('  STATIC METHOD ' + $m.ReturnType.Name + ' ' + $m.Name + '(' + ($pl -join ', ') + ')')
    }
}

Write-Output ''
Write-Output 'Types with static Instance/Singleton returning GameServerRuntime:'
foreach ($t in $types) {
    foreach ($p in $t.Properties) {
        if ($p.PropertyType.Name -eq 'GameServerRuntime') {
            Write-Output ('  ' + $t.FullName + ' :: PROP ' + $p.Name)
        }
    }
    foreach ($f in $t.Fields) {
        if ($f.Name -like 'Native*') { continue }
        if ($f.FieldType.Name -eq 'GameServerRuntime') {
            $s = 'inst'
            if ($f.IsStatic) { $s = 'STATIC' }
            Write-Output ('  ' + $t.FullName + ' :: FIELD(' + $s + ') ' + $f.Name)
        }
    }
}
