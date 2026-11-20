param(
    [Parameter(Mandatory = $true)][string]$SwmPath,
    [Parameter(Mandatory = $true)][string]$DataPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName WindowsBase
[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $DataPath).Path)
[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $SwmPath).Path)

function Assert-Equal($Name, $Actual, $Expected) {
    if (($Actual -join ',') -ne ($Expected -join ',')) {
        $errors.Add("$Name expected [$($Expected -join ',')], got [$($Actual -join ',')]")
    }
}

$errors = [Collections.Generic.List[string]]::new()
$recipe = [AutoReviewSystem.Data.Recipe]::new(
    [AutoReviewSystem.Data.RunMode]::SurfaceAOI, [AutoReviewSystem.Data.InspectionMode]::WSTIC)
$recipe.Wafer = [AutoReviewSystem.Data.Wafer]::new()
$recipe.Wafer.Diameter = 100
$recipe.Wafer.DieWidth = 1
$recipe.Wafer.DieHeight = 2
$recipe.Wafer.ScribeLaneX = 0
$recipe.Wafer.ScribeLaneY = 0.01
$recipe.Wafer.FovMap = [Collections.Generic.List[AutoReviewSystem.Data.Fov]]::new()
$recipe.FieldType = [AutoReviewSystem.Data.FieldView]::new()
$recipe.FieldType.FieldOfViewWidth = 2000
$recipe.FieldType.FieldOfViewHeight = 2000
$recipe.ImageCount = 4

$settings = [AutoReviewSystem.Data.SurfaceAOISetting]::new()
$sequence = [AutoReviewSystem.Data.SurfaceAOISetting+Sequence]::new(4, 1)
$settings.ScanPositions = New-Object 'AutoReviewSystem.Data.RealPoint[]' 4
for ($image = 0; $image -lt 4; $image++) {
    $x = 3 * $image
    $recipe.Wafer._SampleTestPlan.Add([Drawing.Point]::new($x, 0))
    $recipe.Wafer._SampleTestPlan.Add([Drawing.Point]::new(($x + 1), 0))
    $sequence.FOV[$image] = [AutoReviewSystem.Data.RealRectangle]::new((1000 * $x), 0, 2000, 2000)
    $settings.ScanPositions[$image] = [AutoReviewSystem.Data.RealPoint]::new($x, 2)
    $recipe.Wafer.FovMap.Add([AutoReviewSystem.Data.Fov]::new())
}
$settings.ScanSequence = @($sequence)
$recipe.ModeSetting = $settings
$camera = [AutoReviewSystem.Data.CameraParameters]::new()
$camera.Width = 200
$camera.Height = 200
$camera.Recipe = $recipe

$recipe.ProbeMarkRect.Add([Drawing.Rectangle]::new(10, 20, 5, 6))
$recipe.ProbeMarkRect.Add([Drawing.Rectangle]::new(40, 60, 7, 8))
$recipe.ProbeMarkRect.Add([Drawing.Rectangle]::new(90, 190, 20, 20))
$recipe.ProbeMarkRect.Add([Drawing.Rectangle]::new(100, 190, 20, 10))
$recipe.InspectionParameter.padContractSize = 2
$recipe.InspectionParameter.marginT = 7

$prop = [SWM.Parameters].GetProperty('PadRois', [Reflection.BindingFlags]'Instance,NonPublic')
[SWM.Parameters]::cameraParameters = $camera
$rects = $prop.GetValue([SWM.Parameters]::new())
Assert-Equal 'Die-clipped PAD rectangles' @($rects | ForEach-Object {
    "$($_.X)/$($_.Y)/$($_.Width)/$($_.Height)"
}) @('10/20/5/6', '40/60/7/8', '90/190/10/10')

foreach ($mode in 0, 1, 2, 3, 4, 5) {
    foreach ($padMode in 0, 1, 2, 3, -1) {
        $recipe.InspectionParameter.BrightBlackJudge = $mode
        $recipe.InspectionParameter.IsEnablePadErase = $padMode
        $stream = [IO.MemoryStream]::new()
        try {
            # The fixture is constructed in this process. Never deserialize an external file here.
            [Runtime.Serialization.Formatters.Binary.BinaryFormatter]::new().Serialize($stream, $camera)
            $rows = [SWM.Parameters]::new().GetParam($stream.ToArray())
        } finally {
            $stream.Dispose()
        }
        if ($rows.Count -ne 1 -or $rows[0].Length -eq 1) {
            throw "Fixture failed to reach GetParam packing for route $mode, PAD mode $padMode. Result: $($rows[0][0])"
        }

        $row = $rows[0]
        $stride = if ($mode -lt 3) { 5 } else { 4 }
        $roiSize = if ($mode -lt 3) { 21 } else { 18 }
        $recordSize = if ($mode -lt 3) { 419 } else { 374 }
        $label = "route $mode PAD mode $padMode"
        $header = @($row[0], $row[9], $row[13], $row[14], $row[15], $row[16])
        Assert-Equal "$label header" $header @($mode, $padMode, 2, 7, $recordSize, $roiSize)
        Assert-Equal "$label total array size" $row.Length (20 + 4 * $recordSize)

        for ($image = 0; $image -lt 4; $image++) {
            $record = $image * [int]$row[15]
            $layout = [int]$row[16]
            if ($row[$record + 20] -lt 2) { throw "$label image $image did not contain two generated IPROIs." }
            for ($roi = 0; $roi -lt 2; $roi++) {
                $offset = $record + 25 + $roi * $layout
                $x = 100 * $roi
                Assert-Equal "$label image $image ROI $roi box" $row[$offset..($offset + 5)] @($x, 0, 100, 200, 0, 3)
                $expected = @(@(($x + 10), 20, 5, 6), @(($x + 40), 60, 7, 8), @(($x + 90), 190, 10, 10))
                for ($pad = 0; $pad -lt 3; $pad++) {
                    $start = $offset + 6 + $pad * $stride
                    Assert-Equal "$label image $image ROI $roi PAD $pad" $row[$start..($start + 3)] $expected[$pad]
                    if ($stride -eq 5) {
                        $flag = if ($padMode -eq 0 -or $padMode -eq -1) { 1 }
                                elseif ($padMode -eq 1) { 0 }
                                elseif ($pad -eq 2 -and $roi -eq 1 -and $image -lt 3) { 1 }
                                else { 0 }
                        Assert-Equal "$label image $image ROI $roi PAD $pad legacy flag" $row[$start + 4] $flag
                    }
                }
            }
        }
    }
}

if ($errors.Count -ne 0) {
    $errors | Select-Object -First 14 | ForEach-Object { [Console]::Error.WriteLine($_) }
    throw "$($errors.Count) PAD assertions failed."
}
'PASS: 6 routes x 5 integer modes x 4 images x 2 IPROIs. Die clipping, PAD coordinates, legacy flags and array boundaries verified.'
