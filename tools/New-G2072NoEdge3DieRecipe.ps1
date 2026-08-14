param(
    [Parameter(Mandatory = $true)]
    [string]$RecipeDirectory,

    [Parameter(Mandatory = $true)]
    [string]$BaseRecipeBytes,

    [Parameter(Mandatory = $true)]
    [string]$OutputRecipeBytes,

    [Parameter(Mandatory = $true)]
    [string]$HostDirectory
)

$ErrorActionPreference = 'Stop'

Set-Location -LiteralPath $HostDirectory
foreach ($assemblyName in 'Newtonsoft.Json.dll', 'ConfigModel.dll', 'AutoReviewSystem.Data.dll') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $HostDirectory $assemblyName))
}

# RecipeManager loads the V2 XML files using the host's actual recipe parser.
$recipeName = Split-Path -Leaf $RecipeDirectory
$v2Recipe = [ConfigModel.Recipes.RecipeManager]::LoadRecipeListEx() |
    Where-Object { $_.RecipeID -eq $recipeName } |
    Select-Object -First 1
if ($null -eq $v2Recipe -or $null -eq $v2Recipe.Config) {
    throw "Cannot load the cloned V2 recipe: $RecipeDirectory"
}

$selectedDies = $v2Recipe.Config.TestPlan.SampleTestPlan
if ($selectedDies.Count -ne 365) {
    throw "Expected 365 selected dies after removing the two 3-die rows; actual: $($selectedDies.Count)"
}

# Start from the exact bytes the host currently gives SWM.  This preserves all
# camera/inspection fields and changes only the planned die set and derived path.
$deserialize = [AutoReviewSystem.Data.DataConverter].GetMethods() |
    Where-Object { $_.Name -eq 'ToObject' -and $_.IsGenericMethodDefinition } |
    Select-Object -First 1
$serialize = [AutoReviewSystem.Data.DataConverter].GetMethods() |
    Where-Object { $_.Name -eq 'ToByteArray' -and $_.IsGenericMethodDefinition } |
    Select-Object -First 1

$camera = $deserialize.MakeGenericMethod([AutoReviewSystem.Data.CameraParameters]).Invoke(
    $null,
    @(, [IO.File]::ReadAllBytes($BaseRecipeBytes)))
$recipe = $camera.Recipe
$recipe.Wafer._SampleTestPlan = $selectedDies
$recipe.Wafer.CreateDieMap()

# This is the same path-calculation API used by ARSClientController.MakePath.
$setting = [AutoReviewSystem.Data.SurfaceAOISetting]$recipe.ModeSetting
$setting.CalcSettingWithPattern_New($recipe, 0, 0, $true)
if (-not $setting.IsValid -or $setting.ScanPositions.Count -ne 4985) {
    throw "Path calculation failed: IsValid=$($setting.IsValid), ScanCount=$($setting.ScanPositions.Count)"
}

$recipe.ImageCount = $setting.ScanPositions.Count
$bytes = $serialize.MakeGenericMethod([AutoReviewSystem.Data.CameraParameters]).Invoke($null, @($camera))
[IO.File]::WriteAllBytes($OutputRecipeBytes, $bytes)

"SelectedDieCount=$($selectedDies.Count)"
"ImageCount=$($recipe.ImageCount)"
"SliceCount=$($setting.ScanSequence.Count)"
"Output=$OutputRecipeBytes"
