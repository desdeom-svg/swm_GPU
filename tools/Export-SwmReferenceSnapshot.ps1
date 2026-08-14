param(
    [Parameter(Mandatory = $true)]
    [string]$SwmDll,

    [Parameter(Mandatory = $true)]
    [string]$RecipeBytes,

    [Parameter(Mandatory = $true)]
    [string]$OutputCsv,

    [Parameter(Mandatory = $true)]
    [string]$SummaryJson,

    [Parameter(Mandatory = $true)]
    [string]$HostDirectory
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $HostDirectory

$resolveHandler = [System.ResolveEventHandler]{
    param($sender, $args)
    $simpleName = ([Reflection.AssemblyName]$args.Name).Name + '.dll'
    foreach ($directory in @($HostDirectory, (Split-Path -Parent $SwmDll))) {
        $candidate = Join-Path $directory $simpleName
        if (Test-Path -LiteralPath $candidate) {
            return [Reflection.Assembly]::LoadFrom($candidate)
        }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)

try {
    foreach ($dependency in 'AutoReviewSystem.Data.dll', 'OpenCvSharp.dll') {
        $path = Join-Path $HostDirectory $dependency
        if (Test-Path -LiteralPath $path) { [void][Reflection.Assembly]::LoadFrom($path) }
    }
    [void][Reflection.Assembly]::LoadFrom($SwmDll)

    $parameters = [SWM.Parameters]::new()
    $result = $parameters.GetParam([IO.File]::ReadAllBytes($RecipeBytes))
    $firstValue = if ($result.Count -gt 0 -and $result[0].Length -gt 0) { $result[0][0] } else { $null }

    $type = [SWM.Parameters]
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    $core = $type.GetField('tt', $flags).GetValue($null)
    $setting = $type.GetField('modeSetting', $flags).GetValue($null)
    $triggers = [SWM.Parameters]::TriggerInspetions

    $summary = [ordered]@{
        swm_dll = $SwmDll
        swm_sha256 = (Get-FileHash -LiteralPath $SwmDll -Algorithm SHA256).Hash
        recipe_bytes = $RecipeBytes
        recipe_sha256 = (Get-FileHash -LiteralPath $RecipeBytes -Algorithm SHA256).Hash
        returned_rows = $result.Count
        first_value = $firstValue
        image_count = if ($core) { $core.ScanPlan.Slices | ForEach-Object { $_.Scans.Count } | Measure-Object -Sum | Select-Object -ExpandProperty Sum } else { $null }
        slice_count = if ($core) { $core.ScanPlan.Slices.Count } else { $null }
        active_trigger_count = if ($triggers) { $triggers.Count } else { $null }
        repeat_values = if ($setting) { @($setting.ScanSequence | ForEach-Object { $_.Repeat } | Select-Object -Unique) } else { @() }
        scan_count_distribution = if ($core) { @($core.ScanPlan.Slices | Group-Object { $_.Scans.Count } | Sort-Object { [int]$_.Name } | ForEach-Object { [ordered]@{ scan_count = [int]$_.Name; slice_count = $_.Count } }) } else { @() }
    }

    $rows = [Collections.Generic.List[object]]::new()
    if ($result.Count -gt 1 -and $core) {
        for ($sliceIndex = 0; $sliceIndex -lt $core.ScanPlan.Slices.Count; $sliceIndex++) {
            $slice = $core.ScanPlan.Slices[$sliceIndex]
            $parametersForSlice = $result[$sliceIndex]
            $parameterStride = [int]$parametersForSlice[15]
            $globalStart = [int]$slice.Scans[0].Index
            for ($scanIndex = 0; $scanIndex -lt $slice.Scans.Count; $scanIndex++) {
                $offset = $scanIndex * $parameterStride
                $rows.Add([pscustomobject][ordered]@{
                    GlobalIndex = [int]$slice.Scans[$scanIndex].Index
                    SliceIndex = $sliceIndex
                    ScanIndex = $scanIndex
                    SliceScanCount = $slice.Scans.Count
                    Repeat = $setting.ScanSequence[$sliceIndex].Repeat
                    ScanIndexX = $slice.Scans[$scanIndex].IndexX
                    ScanIndexY = $slice.Scans[$scanIndex].IndexY
                    RepeatX = $slice.Scans[$scanIndex].ReapeatXY.X
                    RepeatY = $slice.Scans[$scanIndex].ReapeatXY.Y
                    AreaX = $slice.Scans[$scanIndex].Area.X
                    AreaY = $slice.Scans[$scanIndex].Area.Y
                    AreaWidth = $slice.Scans[$scanIndex].Area.Width
                    AreaHeight = $slice.Scans[$scanIndex].Area.Height
                    Reference1Local = [int]$parametersForSlice[$offset + 21]
                    Reference2Local = [int]$parametersForSlice[$offset + 22]
                    Reference1Global = $globalStart + [int]$parametersForSlice[$offset + 21]
                    Reference2Global = $globalStart + [int]$parametersForSlice[$offset + 22]
                    InspectMode = [int]$parametersForSlice[$offset + 24]
                    IproiCount = [int]$parametersForSlice[$offset + 20]
                    TriggerActive = $triggers.ContainsKey([int]$slice.Scans[$scanIndex].Index)
                })
            }
        }
    }
    $rows | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $SummaryJson -Encoding UTF8
    $summary | ConvertTo-Json -Depth 6
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolveHandler)
}
