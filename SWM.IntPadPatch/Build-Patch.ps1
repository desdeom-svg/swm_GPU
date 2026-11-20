param(
    [Parameter(Mandatory = $true)][string]$InputAssembly,
    [Parameter(Mandatory = $true)][string]$OutputAssembly,
    [Parameter(Mandatory = $true)][string]$CecilPath
)

$ErrorActionPreference = 'Stop'
$inputFile = (Resolve-Path -LiteralPath $InputAssembly).Path
$outputFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputAssembly)
$cecil = (Resolve-Path -LiteralPath $CecilPath).Path
[void][Reflection.Assembly]::LoadFrom($cecil)
Add-Type -Path (Join-Path $PSScriptRoot 'IntPadPatch.cs') -ReferencedAssemblies @($cecil, 'System.Drawing.dll')
[IntPadPatch]::Apply($inputFile, $outputFile)
Get-FileHash -LiteralPath $outputFile
