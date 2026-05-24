[CmdletBinding()]
param(
    [string]$HostName = "82.70.47.203",
    [string]$UserName = "ubuntu",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\oracle.key",
    [string]$RemoteAppDir = "/opt/stacks/bitstore",
    [string]$PublicUrl = "https://bitstore.mrcheng.se/",
    [switch]$UploadCompose,
    [switch]$SkipBuildCheck
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
}

function Invoke-Native {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    & $Command @Arguments
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        throw "$Command failed with exit code $exitCode."
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path (Join-Path (Join-Path $scriptRoot "..") "..")
$keyFullPath = Resolve-Path $KeyPath
$remote = "${UserName}@${HostName}"
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$tempRoot = $env:TEMP
if ([string]::IsNullOrWhiteSpace($tempRoot)) {
    $tempRoot = $env:TMPDIR
}
if ([string]::IsNullOrWhiteSpace($tempRoot)) {
    $tempRoot = [System.IO.Path]::GetTempPath()
}
$archiveName = "bitstore-src-$stamp.tar"
$archivePath = Join-Path $tempRoot $archiveName
$packageRoot = Join-Path $tempRoot "bitstore-src-$stamp"

try {
    Invoke-Step "Validate local project" {
        if (-not (Test-Path (Join-Path $projectRoot "BitStoreWeb.Net9.csproj"))) {
            throw "Could not find BitStoreWeb.Net9.csproj under $projectRoot."
        }

        if (-not (Test-Path $keyFullPath)) {
            throw "SSH key not found: $keyFullPath"
        }

        if (-not $SkipBuildCheck) {
            Invoke-Native "dotnet" @("build", "BitStoreWeb.Net9.csproj", "-o", "bin_verify")
        }
    }

    Invoke-Step "Package source" {
        if (Test-Path $packageRoot) {
            Remove-Item -Recurse -Force $packageRoot
        }

        New-Item -ItemType Directory -Path $packageRoot | Out-Null
        Push-Location $projectRoot
        try {
            $files = @(
                git ls-files
                git ls-files --others --exclude-standard
            ) | Where-Object {
                $_ -and
                $_ -notmatch '^(bin|bin_verify|obj|node_modules|wwwroot/dist)/' -and
                $_ -notmatch '(^|/)\.env$' -and
                $_ -notmatch '\.env\.local$' -and
                $_ -notmatch '\.log$' -and
                $_ -notin @("bitstore.db", "bitstore.db-shm", "bitstore.db-wal", "deploy-secrets.tmp")
            } | Sort-Object -Unique

            foreach ($file in $files) {
                $sourcePath = Join-Path $projectRoot $file
                if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
                    continue
                }

                $destinationPath = Join-Path $packageRoot $file
                $destinationDir = Split-Path -Parent $destinationPath
                if (-not (Test-Path -LiteralPath $destinationDir)) {
                    New-Item -ItemType Directory -Path $destinationDir | Out-Null
                }

                Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
            }
        }
        finally {
            Pop-Location
        }

        if (Test-Path $archivePath) {
            Remove-Item -Force $archivePath
        }

        Invoke-Native "tar" @("-C", $packageRoot, "-cf", $archivePath, ".")
    }

    Invoke-Step "Upload source archive" {
        Invoke-Native "scp" @("-i", $keyFullPath, $archivePath, "${remote}:/tmp/$archiveName")
    }

    if ($UploadCompose) {
        Invoke-Step "Upload BitStore compose template" {
            $composePath = Join-Path (Join-Path $scriptRoot "bitstore") "docker-compose.yml"
            Invoke-Native "scp" @("-i", $keyFullPath, $composePath, "${remote}:$RemoteAppDir/docker-compose.yml")
        }
    }

    Invoke-Step "Deploy on VM" {
        $remoteScript = @"
set -e
if [ ! -f "$RemoteAppDir/.env" ]; then
  echo "Missing $RemoteAppDir/.env. Refusing to deploy without production secrets."
  exit 1
fi
mkdir -p "$RemoteAppDir/src"
rm -rf "$RemoteAppDir/src"/* "$RemoteAppDir/src"/.[!.]* "$RemoteAppDir/src"/..?* 2>/dev/null || true
tar -xf "/tmp/$archiveName" -C "$RemoteAppDir/src"
rm -f "/tmp/$archiveName"
cd "$RemoteAppDir"
sudo docker compose up -d --build bitstore-web
sudo docker compose ps
"@
        Invoke-Native "ssh" @("-i", $keyFullPath, $remote, $remoteScript)
    }

    Invoke-Step "Verify public URL" {
        $response = $null
        $lastError = $null
        for ($attempt = 1; $attempt -le 12; $attempt++) {
            try {
                $response = Invoke-WebRequest -Uri $PublicUrl -UseBasicParsing -TimeoutSec 30
                break
            }
            catch {
                $lastError = $_
                Write-Host "Verification attempt $attempt failed; waiting for app startup..."
                Start-Sleep -Seconds 5
            }
        }

        if ($null -eq $response) {
            throw $lastError
        }

        Write-Host "Verified $PublicUrl -> HTTP $($response.StatusCode), $($response.Content.Length) bytes"
    }

    Write-Host ""
    Write-Host "Deploy complete." -ForegroundColor Green
}
finally {
    if (Test-Path $archivePath) {
        Remove-Item -Force $archivePath
    }

    if (Test-Path $packageRoot) {
        Remove-Item -Recurse -Force $packageRoot
    }
}
