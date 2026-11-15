$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$adb = 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe'
$dotnet = 'C:/Program Files/dotnet/dotnet.exe'

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable exited with code $LASTEXITCODE."
    }
}

$devices = @(Invoke-Checked $adb @('devices') | ForEach-Object {
    if ($_ -match '^(\S+)\s+device\s*$' -and $Matches[1] -notlike 'emulator-*') {
        $Matches[1]
    }
})
if ($devices.Count -ne 1) {
    throw 'Connect exactly one authorized Android phone with USB debugging enabled.'
}

$serial = $devices[0]
$started = [DateTime]::UtcNow
Invoke-Checked $dotnet @('build', 'src/LimosDispatch.Mobile/LimosDispatch.Mobile.csproj',
    '-t:Rebuild', '-f', 'net10.0-android', '-c', 'Debug', '-m:1',
    '-p:MobileTargetFrameworks=net10.0-android', '-p:AndroidPackageFormats=apk', '-p:EmbedAssembliesIntoApk=true')

$appId = 'com.limosdispatch.mobile'
$apk = Get-Item -LiteralPath "src/LimosDispatch.Mobile/bin/Debug/net10.0-android/$appId-Signed.apk"
if ($apk.LastWriteTimeUtc -lt $started) {
    throw 'The signed APK was not regenerated during this build.'
}
Invoke-Checked $adb @('-s', $serial, 'install', '--no-incremental', '-r', $apk.FullName)
Invoke-Checked $adb @('-s', $serial, 'shell', 'monkey', '-p', $appId, '-c', 'android.intent.category.LAUNCHER', '1')
