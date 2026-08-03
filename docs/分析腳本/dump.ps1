param([string[]]$Names)
$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asmPath = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmPath)
$types = $asm.MainModule.GetTypes()

foreach ($n in $Names) {
    foreach ($t in $types) {
        if ($t.Name -ne $n) { continue }
        Write-Output ('===== TYPE ' + $t.FullName + '  base=' + $t.BaseType.Name + ' =====')
        foreach ($f in $t.Fields) {
            if ($f.Name -like 'Native*' -or $f.Name -like 'Method*') { continue }
            Write-Output ('  FIELD  ' + $f.FieldType.Name + '  ' + $f.Name)
        }
        foreach ($p in $t.Properties) {
            Write-Output ('  PROP   ' + $p.PropertyType.Name + '  ' + $p.Name)
        }
        foreach ($m in $t.Methods) {
            if ($m.Name -like '.ctor' -or $m.Name -like '.cctor') { continue }
            $pl = New-Object System.Collections.ArrayList
            foreach ($pp in $m.Parameters) {
                $pn = $pp.ParameterType.Name
                if ($pp.ParameterType.IsByReference) { $pn = 'out/ref ' + $pn }
                [void]$pl.Add($pn + ' ' + $pp.Name)
            }
            Write-Output ('  METHOD ' + $m.ReturnType.Name + '  ' + $m.Name + '(' + ($pl -join ', ') + ')')
        }
        Write-Output ''
    }
}
