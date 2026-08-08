[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$isAdministrator = Test-IsAdministrator
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

if ($PSVersionTable.PSEdition -eq 'Core') {
    $launchParameters = @{
        FilePath = $windowsPowerShell
        ArgumentList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
        Wait = $true
        PassThru = $true
    }
    if (-not $isAdministrator) {
        $launchParameters.Verb = 'RunAs'
    }

    $windowsPowerShellProcess = Start-Process @launchParameters
    exit $windowsPowerShellProcess.ExitCode
}

if (-not $isAdministrator) {
    $elevatedProcess = Start-Process `
        -FilePath $windowsPowerShell `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath)) `
        -Verb RunAs `
        -Wait `
        -PassThru
    exit $elevatedProcess.ExitCode
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectArtifactRoot = Join-Path $projectRoot 'artifacts\msix'
$releaseArtifactRoot = $PSScriptRoot

if (Test-Path -LiteralPath (Join-Path $projectArtifactRoot 'CreateYourTile-x64.msix')) {
    $artifactRoot = $projectArtifactRoot
}
elseif (Test-Path -LiteralPath (Join-Path $releaseArtifactRoot 'CreateYourTile-x64.msix')) {
    $artifactRoot = $releaseArtifactRoot
}
else {
    throw 'Package artifacts are missing. Build the package first or keep this script beside the downloaded MSIX and certificate.'
}

$packagePath = Join-Path $artifactRoot 'CreateYourTile-x64.msix'
$certificatePath = Join-Path $artifactRoot 'CreateYourTile-Dev.cer'

if (-not (Test-Path -LiteralPath $packagePath) -or -not (Test-Path -LiteralPath $certificatePath)) {
    throw 'The MSIX package or its signing certificate is missing.'
}

$certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certificatePath)
$machineTrustPath = 'Cert:\LocalMachine\TrustedPeople\{0}' -f $certificate.Thumbprint
if (-not (Test-Path -LiteralPath $machineTrustPath)) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
}

$dependencyRoot = Join-Path $artifactRoot 'Dependencies\x64'
$dependencyPaths = @()
if (Test-Path -LiteralPath $dependencyRoot) {
    $dependencyPaths = @(Get-ChildItem -LiteralPath $dependencyRoot -File -Filter '*.appx' |
        Select-Object -ExpandProperty FullName)
}

$installParameters = @{
    Path = $packagePath
    ForceApplicationShutdown = $true
    ForceUpdateFromAnyVersion = $true
}
if ($dependencyPaths.Count -gt 0) {
    $installParameters.DependencyPath = $dependencyPaths
}
Add-AppxPackage @installParameters
Write-Host 'Installation complete. Open CreateYourTile! from the Start menu.'
