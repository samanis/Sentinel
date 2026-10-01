[CmdletBinding()]
param(
    [string]$Context = 'minikube',
    [int]$Port = 15156,
    [switch]$TestDatabaseRecovery
)
$ErrorActionPreference = 'Stop'
$namespace = 'sentinel-local'
function Invoke-Kubectl {
    param([string[]]$Arguments)
    $result = & kubectl --context $Context -n $namespace @Arguments
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: $($Arguments -join ' ')" }
    return $result
}
function Get-HttpStatus {
    param([string]$Path)
    try {
        return [int](Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$Port$Path" -TimeoutSec 5).StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}
Invoke-Kubectl @('rollout', 'status', 'deployment/postgres', '--timeout=180s')
Invoke-Kubectl @('rollout', 'status', 'deployment/sentinel-api', '--timeout=180s')
$api = (Invoke-Kubectl @('get', 'deployment', 'sentinel-api', '-o', 'json')) | ConvertFrom-Json
$container = $api.spec.template.spec.containers[0]
if ($container.livenessProbe.httpGet.path -ne '/health' -or
    $container.readinessProbe.httpGet.path -ne '/health/ready' -or
    $container.startupProbe.httpGet.path -ne '/health') { throw 'Incorrect API probes.' }
$pvc = (Invoke-Kubectl @('get', 'pvc', 'postgres-data', '-o', 'json')) | ConvertFrom-Json
if ($pvc.status.phase -ne 'Bound') { throw 'PostgreSQL storage is not bound.' }
$forward = Start-Process -FilePath (Get-Command kubectl).Source -ArgumentList @(
    '--context', $Context, '-n', $namespace, 'port-forward', 'service/sentinel-api', "${Port}:8080"
) -WindowStyle Hidden -PassThru
$databaseStopped = $false
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($forward.HasExited) { throw 'Port-forward exited; check for a port conflict.' }
        if ((Get-HttpStatus '/') -eq 200) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    foreach ($path in @('/', '/health', '/health/ready')) {
        $status = Get-HttpStatus $path
        if ($status -ne 200) { throw "$path returned $status" }
        Write-Host "$path returned 200"
    }
    $body = @{
        title = 'Minikube deployment verification'
        service = 'sentinel-deployment-smoke'
        startedAt = [DateTimeOffset]::UtcNow.ToString('o')
        severity = 'Low'
    } | ConvertTo-Json
    $incident = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/api/incidents/" -ContentType application/json -Body $body
    $read = Invoke-RestMethod "http://127.0.0.1:$Port/api/incidents/$($incident.id)"
    if ($read.id -ne $incident.id -or $read.title -ne 'Minikube deployment verification') {
        throw 'Incident persistence verification failed.'
    }
    Write-Host "Created and read incident $($incident.id)"
    if ($TestDatabaseRecovery) {
        $before = (Invoke-Kubectl @('get', 'pods', '-l', 'app.kubernetes.io/name=sentinel-api', '-o', 'json')) | ConvertFrom-Json
        $restartCount = $before.items[0].status.containerStatuses[0].restartCount
        $databaseStopped = $true
        Invoke-Kubectl @('scale', 'deployment/postgres', '--replicas=0')
        Invoke-Kubectl @('wait', '--for=delete', 'pod', '-l', 'app.kubernetes.io/name=postgres', '--timeout=90s')
        if ((Get-HttpStatus '/health') -ne 200) { throw 'Liveness depends on database availability.' }
        if ((Get-HttpStatus '/health/ready') -ne 503) { throw 'Readiness did not reject database outage.' }
        Invoke-Kubectl @('wait', '--for=condition=Ready=false', 'pod', '-l', 'app.kubernetes.io/name=sentinel-api', '--timeout=60s')
        Invoke-Kubectl @('scale', 'deployment/postgres', '--replicas=1')
        $databaseStopped = $false
        Invoke-Kubectl @('rollout', 'status', 'deployment/postgres', '--timeout=180s')
        Invoke-Kubectl @('wait', '--for=condition=Ready', 'pod', '-l', 'app.kubernetes.io/name=sentinel-api', '--timeout=90s')
        $read = Invoke-RestMethod "http://127.0.0.1:$Port/api/incidents/$($incident.id)"
        if ($read.id -ne $incident.id) { throw 'Incident did not survive PostgreSQL replacement.' }
        $after = (Invoke-Kubectl @('get', 'pods', '-l', 'app.kubernetes.io/name=sentinel-api', '-o', 'json')) | ConvertFrom-Json
        if ($after.items[0].status.containerStatuses[0].restartCount -ne $restartCount) {
            throw 'API restarted during database outage.'
        }
        Write-Host 'Database recovery passed: readiness 503, liveness 200, no API restart, incident persisted.'
    }
    Invoke-Kubectl @('get', 'deployments,pods,services,pvc')
} finally {
    if ($databaseStopped) {
        Invoke-Kubectl @('scale', 'deployment/postgres', '--replicas=1')
        Invoke-Kubectl @('rollout', 'status', 'deployment/postgres', '--timeout=180s')
    }
    if (!$forward.HasExited) { Stop-Process -Id $forward.Id }
}
