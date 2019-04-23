##
#  Builds the source code and generates nuget packages. You can optionally just build the source code by opening individual solutions in Visual Studio.
##

param
(
    # Configuration to build.
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = "Release",

    # Platform to build for. 
    [ValidateSet('clean', 'rebuild')]
    [string]$Target = "rebuild",

    # Platform to build for. 
    [ValidateSet('Windows', 'Linux')]
    [string]$Platform = "Windows",

    # msbuild verbosity level.
    [ValidateSet('quiet','minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal',

    # path to msbuild
    [string]$MSBuildFullPath
)

$ErrorActionPreference = "Stop"

if ($Target -eq "rebuild") {
    $restore = "-r"
    $buildTarget = "clean;rebuild"
} elseif ($Target -eq "clean") {
    $buildTarget = "clean"
}

if ($Platform -eq "Linux") {
    $RuntimeIdentifier = "ubuntu.16.04-x64"
    $UpdateServiceFabricManifestEnabledProperty = "/property:UpdateServiceFabricManifestEnabled=false"
}
elseif ($Platform -eq "Windows") {
    $RuntimeIdentifier = "win7-x64"
}

if($MSBuildFullPath -ne "")
{
    if (!(Test-Path $MSBuildFullPath))
    {
        throw "Unable to find MSBuild at the specified path, run the script again with correct path to msbuild."
    }
}

# msbuild path not provided, find msbuild for VS2017
if($MSBuildFullPath -eq "")
{
    if (Test-Path "env:\ProgramFiles(x86)")
    {
        $progFilesPath =  ${env:ProgramFiles(x86)}
    }
    elseif (Test-Path "env:\ProgramFiles")
    {
        $progFilesPath =  ${env:ProgramFiles}
    }

    $VS2017InstallPath = join-path $progFilesPath "Microsoft Visual Studio\2017"
    $versions = 'Community', 'Professional', 'Enterprise'

    foreach ($version in $versions)
    {
        $VS2017VersionPath = join-path $VS2017InstallPath $version
        $MSBuildFullPath = join-path $VS2017VersionPath "MSBuild\15.0\Bin\MSBuild.exe"

        if (Test-Path $MSBuildFullPath)
        {
            break
        }
    }

    if (!(Test-Path $MSBuildFullPath))
    {
        Write-Host "Visual Studio 2017 installation not found in ProgramFiles, trying to find install path from registry."
        if(Test-Path -Path HKLM:\SOFTWARE\WOW6432Node)
        {
            $VS2017VersionPath = Get-ItemProperty (Get-ItemProperty -Path HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7 -Name "15.0")."15.0"
        }
        else
        {
            $VS2017VersionPath = Get-ItemProperty (Get-ItemProperty -Path HKLM:\SOFTWARE\Microsoft\VisualStudio\SxS\VS7 -Name "15.0")."15.0"
        }

        $MSBuildFullPath = join-path $VS2017VersionPath "MSBuild\15.0\Bin\MSBuild.exe"
    }
}

if (!(Test-Path $MSBuildFullPath))
{
    throw "Unable to find MSBuild installed on this machine. Please install Visual Studio 2017 or if its installed at non-default location, provide the full ppath to msbuild using -MSBuildFullPath parameter."
}


Write-Output "Using msbuild from $msbuildFullPath"

$msbuildArgs = @(
    "/nr:false", 
    "/nologo", 
    "$restore"
    "/t:$buildTarget", 
    "/verbosity:$verbosity",  
    "/property:RuntimeIdentifier=$RuntimeIdentifier", 
    $UpdateServiceFabricManifestEnabledProperty,
    "/property:RequestedVerbosity=$verbosity", 
    "/property:Configuration=$configuration",
    $args)
& $msbuildFullPath $msbuildArgs



if ($Target -eq "rebuild") {
    $buildTarget = "package"

    $msbuildArgs = @(
        "/nr:false", 
        "/nologo", 
        "$restore"
        "/t:$buildTarget", 
        "/verbosity:$verbosity",  
        "/property:RuntimeIdentifier=$RuntimeIdentifier", 
        $UpdateServiceFabricManifestEnabledProperty,
        "/property:RequestedVerbosity=$verbosity", 
        "/property:Configuration=$configuration",
        $args)
    & $msbuildFullPath AutoscaleManager/AutoscaleManager.sfproj $msbuildArgs

    if ($Platform -eq "Linux") {
        Copy-Item AutoscaleManager\pkg\$configuration\NodeManagerPkg\Code\NodeManager AutoscaleManager\pkg\$configuration\NodeManagerPkg\Code\NodeManager.exe
    }
} 

