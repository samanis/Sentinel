[CmdletBinding()]
param([string]$Context = 'minikube')
$ErrorActionPreference = 'Stop'

# Terraform owns the namespace, but deliberately never owns or reads this Secret.
& kubectl --context $Context get namespace sentinel-local -o name
if ($LASTEXITCODE -ne 0) { throw 'Apply the Terraform namespace bootstrap first.' }
& kubectl --context $Context -n sentinel-local get secret sentinel-database -o name --ignore-not-found
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the database Secret.' }
$existing = & kubectl --context $Context -n sentinel-local get secret sentinel-database -o name --ignore-not-found
if ($existing) { Write-Host 'Existing database Secret preserved.'; return }

# Random hex is safe in an Npgsql connection string. No password in arguments,
# temporary files, console output, Terraform inputs, plans, or state.
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$password = -join ($bytes | ForEach-Object { $_.ToString('x2') })
$secret = @{
    apiVersion = 'v1'
    kind = 'Secret'
    metadata = @{ name = 'sentinel-database'; namespace = 'sentinel-local' }
    type = 'Opaque'
    stringData = @{
        POSTGRES_PASSWORD = $password
        ConnectionStrings__Sentinel = "Host=postgres;Port=5432;Database=sentinel;Username=sentinel;Password=$password"
    }
}
$secret | ConvertTo-Json -Depth 6 -Compress | & kubectl --context $Context create -f -
if ($LASTEXITCODE -ne 0) { throw 'Secret creation failed.' }
$password = $null
$secret = $null
