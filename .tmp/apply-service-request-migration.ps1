$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $workspace '.tmp/service-migration-artifacts/bin'
$migrationTool = Join-Path $artifactRoot 'ServiceRequestMigration/debug/ServiceRequestMigration.dll'
$services = @(
    @{ Name = 'TMCWD.Data'; Port = 5178; Project = 'TMCWD/TMCWD.Data' },
    @{ Name = 'TMCWD.Application'; Port = 5054; Project = 'TMCWD/TMCWD.Application' }
)
foreach ($service in $services) {
    $service.Binary = Join-Path $artifactRoot ($service.Name + '/debug/' + $service.Name + '.exe')
    if (!(Test-Path -LiteralPath $service.Binary)) { throw "Missing built service: $($service.Name)" }
    $owners = @(Get-NetTCPConnection -State Listen -LocalPort $service.Port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
    $service.Existing = @()
    foreach ($owner in $owners) {
        $process = Get-Process -Id $owner
        if ($process.ProcessName -ne $service.Name) { throw "Port $($service.Port) belongs to a different application." }
        $existingPath = [System.IO.Path]::GetFullPath($process.Path)
        if (!$existingPath.StartsWith($workspace + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The running service is outside the workspace.'
        }
        $service.Existing += @{ Id = $owner; Binary = $existingPath }
    }
}
function Start-LocalService($service, $binary) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://0.0.0.0:' + $service.Port
    $outPath = Join-Path $workspace ('.tmp/service-migration-' + $service.Name + '.out.log')
    $errPath = Join-Path $workspace ('.tmp/service-migration-' + $service.Name + '.err.log')
    Start-Process -FilePath $binary -WorkingDirectory (Join-Path $workspace $service.Project) -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath | Out-Null
}
foreach ($service in $services) {
    foreach ($existing in $service.Existing) { Stop-Process -Id $existing.Id -Force }
}
try {
    & dotnet $migrationTool apply $workspace
    if ($LASTEXITCODE -ne 0) { throw 'Service request migration failed.' }
} catch {
    foreach ($service in $services) {
        foreach ($existing in $service.Existing) { Start-LocalService $service $existing.Binary }
    }
    throw
}
foreach ($service in $services) { Start-LocalService $service $service.Binary }
foreach ($service in $services) {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $path = if ($service.Name -eq 'TMCWD.Data') { '/api/Dashboard/ServiceRequestsByDescription?description=LEAK' } else { '/' }
            $response = Invoke-WebRequest -Uri ('http://127.0.0.1:' + $service.Port + $path) -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$ready) { throw "Service did not become ready: $($service.Name). Check .tmp/service-migration logs." }
    Write-Output "$($service.Name) is running on port $($service.Port)."
}
