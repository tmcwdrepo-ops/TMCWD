param([string]$FlutterExecutable = 'flutter')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (Test-Path -LiteralPath (Join-Path $projectRoot 'android')) {
    throw 'Android already exists. Use flutter pub get; this script does not overwrite it.'
}
Get-Command $FlutterExecutable -ErrorAction Stop | Out-Null
$templateRoot = Join-Path $projectRoot '.android-template'
if (Test-Path -LiteralPath $templateRoot) {
    throw 'An earlier template exists. Inspect .android-template before retrying.'
}
& $FlutterExecutable --no-version-check create --platforms=android --org ph.tmcwd --project-name tmcwd_meter_reader_mobile --no-pub $templateRoot
if ($LASTEXITCODE -ne 0) { throw 'Flutter Android generation failed.' }
Copy-Item -LiteralPath (Join-Path $templateRoot 'android') -Destination $projectRoot -Recurse
Copy-Item -LiteralPath (Join-Path $templateRoot '.metadata') -Destination $projectRoot
$manifestPath = Join-Path $projectRoot 'android/app/src/main/AndroidManifest.xml'
$manifest = Get-Content -LiteralPath $manifestPath -Raw
$manifest = $manifest.Replace('<application', '<application android:allowBackup="false"')
$manifest = $manifest.Replace('<application', '<uses-permission android:name="android.permission.INTERNET"/><application')
$manifest = $manifest.Replace('android:label="tmcwd_meter_reader_mobile"', 'android:label="TMCWD Meter Reader"')
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding utf8
$gradlePath = Join-Path $projectRoot 'android/app/build.gradle.kts'
$gradle = Get-Content -LiteralPath $gradlePath -Raw
$gradle = $gradle.Replace('minSdk = flutter.minSdkVersion', 'minSdk = maxOf(flutter.minSdkVersion, 23)')
Set-Content -LiteralPath $gradlePath -Value $gradle -Encoding utf8
Push-Location $projectRoot
try {
    & $FlutterExecutable --no-version-check pub get
    if ($LASTEXITCODE -ne 0) { throw 'Dependency resolution failed.' }
} finally {
    Pop-Location
}
