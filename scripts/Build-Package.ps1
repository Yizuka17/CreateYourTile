[CmdletBinding()]
param(
    [switch]$Install,
    [ValidateRange(1, 30)]
    [int]$CertificateValidityYears = 10,
    [ValidateNotNullOrEmpty()]
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\msix'))
$buildRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\package-build'))
$packagePath = Join-Path $artifactRoot 'CreateYourTile-x64.msix'
$certificatePath = Join-Path $artifactRoot 'CreateYourTile-Dev.cer'
$dependencyOutputRoot = Join-Path $artifactRoot 'Dependencies\x64'

foreach ($path in @($artifactRoot, $buildRoot)) {
    $expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
    if (-not $path.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package output escaped the artifact directory: $path"
    }
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$publisher = 'CN=CreateYourTile.Dev'
$minimumCertificateLifetime = [TimeSpan]::FromDays(($CertificateValidityYears * 365) - 30)
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $publisher -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30) -and
        ($_.NotAfter - $_.NotBefore) -ge $minimumCertificateLifetime
    } |
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
        -NotAfter (Get-Date).AddYears($CertificateValidityYears) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
}

$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vsWhere)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}
$msBuild = & $vsWhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msBuild) {
    throw 'Visual Studio MSBuild was not found. Install the Universal Windows Platform and C++ workloads.'
}
$packageProject = Join-Path $projectRoot 'CreateYourTile.Package\CreateYourTile.Package.wapproj'
$appxPackageDirectory = $buildRoot.TrimEnd('\') + '\'

$buildOutput = & $msBuild $packageProject `
    /restore `
    /t:Build `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:AppxBundle=Never `
    /p:AppxPackageSigningEnabled=false `
    "/p:AppxPackageDir=$appxPackageDirectory" `
    /m `
    /v:minimal 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "UWP/MSIX build failed.`n$($buildOutput -join "`n")"
}

$builtPackage = Get-ChildItem -LiteralPath $buildRoot -Recurse -File -Filter '*.msix' |
    Where-Object { $_.DirectoryName -notmatch '[\\/]Dependencies([\\/]|$)' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $builtPackage) {
    throw 'The Windows Application Packaging Project did not produce an MSIX package.'
}
Copy-Item -LiteralPath $builtPackage.FullName -Destination $packagePath -Force

$dependencySourceRoot = Join-Path $builtPackage.Directory.FullName 'Dependencies\x64'
if (Test-Path -LiteralPath $dependencySourceRoot) {
    New-Item -ItemType Directory -Path $dependencyOutputRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $dependencySourceRoot '*') -Destination $dependencyOutputRoot -Force
}

$sdkToolsRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
$sdkPackage = Get-ChildItem -LiteralPath $sdkToolsRoot -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $sdkPackage) {
    throw 'Microsoft.Windows.SDK.BuildTools is not available in the NuGet cache.'
}
$signTool = Get-ChildItem -LiteralPath (Join-Path $sdkPackage.FullName 'bin') -Recurse -File -Filter 'signtool.exe' |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Select-Object -First 1
if (-not $signTool) {
    throw 'signtool.exe was not found.'
}

Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null
$signOutput = & $signTool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /s My `
    /tr $TimestampUrl /td SHA256 $packagePath 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "MSIX signing failed.`n$($signOutput -join "`n")"
}
Write-Host "UWP MSIX signed with a $CertificateValidityYears-year certificate and an RFC 3161 timestamp."

$null = & $signTool.FullName verify /pa $packagePath 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host 'MSIX signature and trust chain verified.'
}
else {
    Write-Warning 'The signature is present, but its self-signed development certificate is not trusted yet. Run Install-Package.ps1 to trust it and install the app.'
}
$global:LASTEXITCODE = 0

if ($Install) {
    & (Join-Path $projectRoot 'scripts\Install-Package.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'MSIX installation failed.'
    }
}

Write-Host "Package: $packagePath"
Write-Host "Certificate: $certificatePath"
Write-Host "Dependencies: $dependencyOutputRoot"
