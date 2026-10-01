[CmdletBinding()]
param(
    [string]$Profile = 'minikube',
    [string]$MinikubeExecutable = 'minikube',
    [string]$DockerNodeName
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$revision = (& git -C $repo rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify repository revision.' }
# Include time and randomness because the working tree can differ from HEAD.
$tag = "$revision-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,6))"
$image = "sentinel-api:$tag"
if ($DockerNodeName) {
    & docker exec $DockerNodeName test -S /run/containerd/containerd.sock
    if ($LASTEXITCODE -ne 0) { throw 'The specified Docker node must already run containerd.' }
} else {
    & $MinikubeExecutable -p $Profile status
    if ($LASTEXITCODE -ne 0) { throw 'Existing Minikube cluster must be running.' }
}
& docker build --file (Join-Path $repo 'src/Sentinel.Api/Dockerfile') --tag $image $repo
if ($LASTEXITCODE -ne 0) { throw 'Image build failed.' }
if ($DockerNodeName) {
    # Single-node Docker-driver Minikube fallback when its CLI is not on PATH.
    $archive = Join-Path ([IO.Path]::GetTempPath()) ("sentinel-" + [Guid]::NewGuid().ToString('N') + '.tar')
    # containerd's systemd PrivateTmp hides /tmp from the transfer service.
    $nodeArchive = "/var/lib/$([IO.Path]::GetFileName($archive))"
    try {
        & docker save --output $archive $image
        if ($LASTEXITCODE -ne 0) { throw 'Image export failed.' }
        & docker cp $archive "${DockerNodeName}:$nodeArchive"
        if ($LASTEXITCODE -ne 0) { throw 'Image transfer failed.' }
        & docker exec $DockerNodeName ctr --namespace k8s.io images import $nodeArchive
        if ($LASTEXITCODE -ne 0) { throw 'Image import failed.' }
    } finally {
        & docker exec $DockerNodeName rm -f $nodeArchive
        if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
    }
} else {
    & $MinikubeExecutable -p $Profile image load $image
    if ($LASTEXITCODE -ne 0) { throw 'Image load failed.' }
}
$inputs = @{ api_image = $image }
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '../local/image.auto.tfvars.json'), ($inputs | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
Write-Host "Loaded $image; wrote local/image.auto.tfvars.json."
