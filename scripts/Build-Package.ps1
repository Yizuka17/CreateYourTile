[CmdletBinding()]
param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\msix'))
$stagingRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'staging'))
$packagePath = Join-Path $artifactRoot 'CreateYourTile-x64.msix'
$certificatePath = Join-Path $artifactRoot 'CreateYourTile-Dev.cer'

if (-not $stagingRoot.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Staging path escaped the artifact directory: $stagingRoot"
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

dotnet publish (Join-Path $projectRoot 'CreateYourTile.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $stagingRoot
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath (Join-Path $projectRoot 'Package\Package.appxmanifest') `
    -Destination (Join-Path $stagingRoot 'AppxManifest.xml') -Force

$assetDirectory = Join-Path $stagingRoot 'Assets'
$assetProcess = Start-Process `
    -FilePath (Join-Path $stagingRoot 'CreateYourTile.exe') `
    -ArgumentList "--generate-package-assets=$assetDirectory" `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($assetProcess.ExitCode -ne 0) { throw 'Package asset generation failed.' }

$sdkToolsRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
$sdkPackage = Get-ChildItem -LiteralPath $sdkToolsRoot -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $sdkPackage) { throw 'Microsoft.Windows.SDK.BuildTools is not available in the NuGet cache.' }

$architectureToolDirectories = Get-ChildItem -LiteralPath (Join-Path $sdkPackage.FullName 'bin') -Directory |
    ForEach-Object { Join-Path $_.FullName 'x64' } |
    Where-Object { Test-Path -LiteralPath (Join-Path $_ 'makeappx.exe') }
$toolDirectory = $architectureToolDirectories | Select-Object -First 1
if (-not $toolDirectory) { throw 'makeappx.exe was not found.' }

$makeAppx = Join-Path $toolDirectory 'makeappx.exe'
$signTool = Join-Path $toolDirectory 'signtool.exe'

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$makeOutput = & $makeAppx pack /d $stagingRoot /p $packagePath /o 2>&1
if ($LASTEXITCODE -ne 0) { throw "MSIX packaging failed.`n$($makeOutput -join "`n")" }
Write-Host 'MSIX package created.'

$publisher = 'CN=CreateYourTile.Dev'
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $publisher `
        -FriendlyName 'CreateYourTile development signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
}

Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null
$signOutput = & $signTool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $packagePath 2>&1
if ($LASTEXITCODE -ne 0) { throw "MSIX signing failed.`n$($signOutput -join "`n")" }
Write-Host 'MSIX package signed.'

$null = & $signTool verify /pa $packagePath 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host 'MSIX signature and trust chain verified.'
}
else {
    Write-Warning 'The signature is present, but its self-signed development certificate is not trusted yet. Run Install-Package.ps1 to trust it for the current user and install the app.'
}

if ($Install) {
    & (Join-Path $projectRoot 'scripts\Install-Package.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'MSIX installation failed.' }
    Write-Host 'Installed: CreateYourTile!'
}

Write-Host "Package: $packagePath"
Write-Host "Certificate: $certificatePath"
