$ErrorActionPreference = 'Stop'
$core = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$asmPath = 'C:\Program Files (x86)\Steam\steamapps\common\SpiritVale\BepInEx\interop\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmPath)
$types = $asm.MainModule.GetTypes()
Write-Output ('total types: ' + @($types).Count)

$fieldPat  = 'showingSubstatRange|substatConfig'
$methodPat = '^(GetSubstatRange|GetSubstatRangeText|GetSubstats|GetSubstatConfig|GetMaxSubstats|ToDescription|DrawTooltip|UpdateToolTip)$'

foreach ($t in $types) {
    $hit = New-Object System.Collections.ArrayList
    foreach ($f in $t.Fields) {
        if ($f.Name -match $fieldPat) { [void]$hit.Add('F:' + $f.Name) }
    }
    foreach ($p in $t.Properties) {
        if ($p.Name -match $fieldPat) { [void]$hit.Add('P:' + $p.Name) }
    }
    foreach ($m in $t.Methods) {
        if ($m.Name -match $methodPat) { [void]$hit.Add('M:' + $m.Name) }
    }
    if ($hit.Count -gt 0) {
        Write-Output ($t.FullName + '  ==>  ' + ($hit -join ' | '))
    }
}
