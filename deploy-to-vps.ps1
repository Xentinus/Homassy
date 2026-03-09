<#
.SYNOPSIS
    Homassy Deployment Script for VPS (x86_64/AMD64) - PowerShell Version

.DESCRIPTION
    Builds AMD64 Docker images, transfers them to a VPS via SSH,
    and deploys the application in production mode.

    Requirements:
    - PowerShell 7+
    - Docker Desktop
    - OpenSSH client (built into Windows 11)
    - For password auth: PuTTY plink.exe + pscp.exe in PATH

.NOTES
    Usage: .\deploy-to-vps.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# =============================================================================
# Configuration and Constants
# =============================================================================

$ScriptDir     = Split-Path -Parent $MyInvocation.MyCommand.Path
$ComposeFile   = "docker-compose.production.yml"
$EnvFile       = ".env"
$Services      = @("homassymigrator", "homassyapi", "homassyweb", "homassyemail", "homassynotifications")
$PostgresImage = "postgres:16"
$VolumeName    = "postgres-data"
$NetworkName   = "homassy-network"
$ControlPath   = Join-Path $env:TEMP "homassy-deploy-ssh.sock"

# =============================================================================
# Global State
# =============================================================================

$Global:VpsUser        = ""
$Global:VpsHost        = ""
$Global:VpsPort        = "22"
$Global:VpsSshKey      = ""
$Global:VpsPassword    = ""
$Global:DeployPath     = "/opt/homassy"
$Global:ImageExportDir = Join-Path $env:TEMP "homassy-images"
$Global:IsUpdate       = $false
$Global:UsePlink       = $false
$Global:SshKeyArgs     = @()

# =============================================================================
# Utility Functions
# =============================================================================

function Write-Log {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message" -ForegroundColor Cyan
}

function Stop-WithError {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

# =============================================================================
# Remote Execution Helpers
# =============================================================================

function Invoke-RemoteCommand {
    param(
        [string]$Command,
        [switch]$AllowFailure
    )

    # Temporarily suppress ErrorActionPreference so that stderr lines from external
    # tools (written as ErrorRecord objects by PowerShell 5.1 when using 2>&1)
    # do not trigger "Stop" before we can inspect the exit code ourselves.
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    if ($Global:UsePlink) {
        $output = & plink -batch -pw $Global:VpsPassword -P $Global:VpsPort `
            "$($Global:VpsUser)@$($Global:VpsHost)" $Command 2>&1
    } else {
        $sshArgs = @(
            "-o", "StrictHostKeyChecking=no",
            "-o", "ControlMaster=auto",
            "-o", "ControlPath=$ControlPath",
            "-o", "ControlPersist=10m",
            "-p", $Global:VpsPort
        ) + $Global:SshKeyArgs + @("$($Global:VpsUser)@$($Global:VpsHost)", $Command)

        $output = & ssh @sshArgs 2>&1
    }

    $exitCode  = $LASTEXITCODE
    $ErrorActionPreference = $prevEAP
    $outputStr = if ($output) { ($output | ForEach-Object { "$_" }) -join "`n" } else { "" }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Remote command failed (exit $exitCode): $Command"
    }

    return @{ Output = $outputStr.Trim(); ExitCode = $exitCode }
}

function Invoke-ScpUpload {
    param(
        [string]$Source,
        [string]$Destination,
        [switch]$Recursive
    )

    if ($Global:UsePlink) {
        $scpArgs = @("-pw", $Global:VpsPassword, "-P", $Global:VpsPort)
        if ($Recursive) { $scpArgs += "-r" }
        $scpArgs += @($Source, $Destination)
        & pscp @scpArgs
    } else {
        $scpArgs = @(
            "-o", "StrictHostKeyChecking=no",
            "-o", "ControlMaster=auto",
            "-o", "ControlPath=$ControlPath",
            "-o", "ControlPersist=10m",
            "-P", $Global:VpsPort
        ) + $Global:SshKeyArgs
        if ($Recursive) { $scpArgs += "-r" }
        $scpArgs += @($Source, $Destination)
        & scp @scpArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "SCP transfer failed: $Source -> $Destination"
    }
}

function Close-SshControlMaster {
    if (-not $Global:UsePlink -and (Test-Path $ControlPath)) {
        & ssh -O exit -o "ControlPath=$ControlPath" placeholder 2>$null
    }
}

# =============================================================================
# Pre-flight Checks
# =============================================================================

function Test-DockerRunning {
    Write-Log "Checking if Docker is running..."
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Docker is not running. Please start Docker Desktop."
    }
    Write-Success "Docker is running"
}

function Read-EnvFile {
    Write-Log "Verifying .env file..."

    $envPath = Join-Path $ScriptDir $EnvFile
    if (-not (Test-Path $envPath)) {
        Stop-WithError ".env file not found in project root ($ScriptDir)"
    }

    # Parse .env file and export all variables into the process scope
    Get-Content $envPath | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line -match '^([^=]+)=(.*)$') {
            $key   = $Matches[1].Trim()
            $value = $Matches[2].Trim().Trim('"').Trim("'")
            [System.Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
    }

    # Validate required variables
    foreach ($var in @("VPS_USER", "VPS_HOST")) {
        if ([string]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable($var))) {
            Stop-WithError "Required variable $var is not set in .env file"
        }
    }

    $sshKey   = [System.Environment]::GetEnvironmentVariable("VPS_SSH_KEY")
    $password = [System.Environment]::GetEnvironmentVariable("VPS_PASSWORD")

    if ([string]::IsNullOrEmpty($sshKey) -and [string]::IsNullOrEmpty($password)) {
        Stop-WithError "Either VPS_SSH_KEY or VPS_PASSWORD must be set in .env file"
    }

    $Global:VpsUser     = [System.Environment]::GetEnvironmentVariable("VPS_USER")
    $Global:VpsHost     = [System.Environment]::GetEnvironmentVariable("VPS_HOST")
    $Global:VpsSshKey   = $sshKey
    $Global:VpsPassword = $password

    $port = [System.Environment]::GetEnvironmentVariable("VPS_PORT")
    if (-not [string]::IsNullOrEmpty($port)) { $Global:VpsPort = $port }

    $deployPath = [System.Environment]::GetEnvironmentVariable("DEPLOY_PATH")
    if (-not [string]::IsNullOrEmpty($deployPath)) { $Global:DeployPath = $deployPath }

    $imageExportDir = [System.Environment]::GetEnvironmentVariable("IMAGE_EXPORT_DIR")
    if (-not [string]::IsNullOrEmpty($imageExportDir)) { $Global:ImageExportDir = $imageExportDir }

    Write-Success "Environment configuration loaded"
}

function Test-SshConnectivity {
    Write-Log "Testing SSH connectivity to VPS..."

    if (-not [string]::IsNullOrEmpty($Global:VpsPassword)) {
        # Password authentication via PuTTY plink/pscp
        # Auto-detect plink/pscp from PATH or common install locations
        $puttyPaths = @(
            "C:\Program Files\PuTTY",
            "C:\Program Files (x86)\PuTTY",
            "$env:LOCALAPPDATA\Programs\PuTTY",
            "$env:ProgramFiles\PuTTY"
        )

        $plinkCmd = Get-Command plink -ErrorAction SilentlyContinue
        if (-not $plinkCmd) {
            foreach ($p in $puttyPaths) {
                if (Test-Path (Join-Path $p "plink.exe")) {
                    $env:PATH = "$p;$env:PATH"
                    $plinkCmd = Get-Command plink -ErrorAction SilentlyContinue
                    break
                }
            }
        }
        if (-not $plinkCmd) {
            Stop-WithError "Password authentication requires 'plink' from PuTTY.`nInstall PuTTY and ensure plink.exe is in your PATH: https://www.putty.org/"
        }
        if (-not (Get-Command pscp -ErrorAction SilentlyContinue)) {
            Stop-WithError "Password authentication requires 'pscp' from PuTTY.`nInstall PuTTY and ensure pscp.exe is in your PATH."
        }

        $Global:UsePlink = $true

        # Accept host key on first connect by piping 'y' (caches key in PuTTY registry)
        # Subsequent -batch calls will succeed once the key is cached
        "y" | & plink -pw $Global:VpsPassword -P $Global:VpsPort `
            "$($Global:VpsUser)@$($Global:VpsHost)" "echo SSH connection successful" 2>$null | Out-Null

    } else {
        # SSH key authentication via built-in OpenSSH
        if (-not (Test-Path $Global:VpsSshKey)) {
            Stop-WithError "VPS_SSH_KEY is set but file does not exist: $($Global:VpsSshKey)"
        }

        $Global:UsePlink   = $false
        $Global:SshKeyArgs = @("-i", $Global:VpsSshKey)

        $sshArgs = @(
            "-o", "ConnectTimeout=10",
            "-o", "StrictHostKeyChecking=no",
            "-o", "ControlMaster=auto",
            "-o", "ControlPath=$ControlPath",
            "-o", "ControlPersist=10m",
            "-p", $Global:VpsPort,
            "-i", $Global:VpsSshKey,
            "$($Global:VpsUser)@$($Global:VpsHost)",
            "echo SSH connection successful"
        )

        & ssh @sshArgs 2>$null | Out-Null
    }

    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Cannot connect to VPS via SSH at $($Global:VpsUser)@$($Global:VpsHost):$($Global:VpsPort)`nCheck your credentials and network connectivity."
    }

    Write-Success "SSH connectivity verified"
}

function Test-RemoteDiskSpace {
    Write-Log "Checking available disk space on VPS..."

    try {
        $result    = Invoke-RemoteCommand "df -BG $($Global:DeployPath) 2>/dev/null | tail -1 | awk '{print \$4}' | sed 's/G//'" -AllowFailure
        $available = [int]($result.Output -replace '\D', '')
        $required  = 3

        if ($available -lt $required) {
            Write-Warn "Low disk space: ${available}GB available, ${required}GB recommended"
            Write-Warn "Cleaning up old Docker images..."
            Invoke-RemoteCommand 'docker image prune -af --filter "until=48h"' -AllowFailure | Out-Null
        } else {
            Write-Success "Sufficient disk space available (${available}GB)"
        }
    } catch {
        Write-Warn "Could not check disk space: $_"
    }
}

# =============================================================================
# Build Phase
# =============================================================================

function Build-Amd64Images {
    Write-Log "Building AMD64 Docker images (linux/amd64)..."

    # Temporarily relax error handling for setup commands that write to stderr on success
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    & docker context use default 2>&1 | Out-Null
    & docker buildx use default 2>&1 | Out-Null
    $ErrorActionPreference = $prevEAP
    $env:DOCKER_BUILDKIT = "1"

    $kratosPublicUrl = [System.Environment]::GetEnvironmentVariable("KRATOS_PUBLIC_URL")
    $vpsApiBase      = [System.Environment]::GetEnvironmentVariable("VPS_API_BASE")

    Write-Log "Building migrator image..."
    & docker build --platform linux/amd64 --target final `
        -t homassymigrator:latest -f Homassy.Migrator/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to build migrator image" }

    Write-Log "Building API image..."
    & docker build --platform linux/amd64 --target final `
        --build-arg BUILD_CONFIGURATION=Release `
        -t homassyapi:latest -f Homassy.API/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to build API image" }

    Write-Log "Building Web image..."
    Write-Log "Using KRATOS_PUBLIC_URL: $kratosPublicUrl"
    Write-Log "Using VPS_API_BASE: $vpsApiBase"
    & docker build --platform linux/amd64 --target production `
        --build-arg "NUXT_PUBLIC_KRATOS_URL=$kratosPublicUrl" `
        --build-arg "NUXT_PUBLIC_API_BASE=$vpsApiBase" `
        -t homassyweb:latest -f Homassy.Web/Dockerfile Homassy.Web
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to build Web image" }

    Write-Log "Building Email image..."
    & docker build --platform linux/amd64 --target final `
        --build-arg BUILD_CONFIGURATION=Release `
        -t homassyemail:latest -f Homassy.Email/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to build Email image" }

    Write-Log "Building Notifications image..."
    & docker build --platform linux/amd64 --target final `
        --build-arg BUILD_CONFIGURATION=Release `
        -t homassynotifications:latest -f Homassy.Notifications/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to build Notifications image" }

    Write-Success "All AMD64 images built successfully"
}

function Save-ImagesToTar {
    Write-Log "Exporting Docker images to tar archives..."

    if (-not (Test-Path $Global:ImageExportDir)) {
        New-Item -ItemType Directory -Path $Global:ImageExportDir -Force | Out-Null
    }

    foreach ($service in $Services) {
        $tarFile = Join-Path $Global:ImageExportDir "$service.tar"
        Write-Log "Saving ${service}:latest..."
        & docker save -o $tarFile "${service}:latest"
        if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to save $service image" }
    }

    Write-Success "Images exported to $($Global:ImageExportDir)"
}

# =============================================================================
# Transfer Phase
# =============================================================================

function New-RemoteDirectories {
    Write-Log "Creating directory structure on VPS..."

    $result          = Invoke-RemoteCommand "test -d $($Global:DeployPath) && echo exists || echo notexists" -AllowFailure
    $Global:IsUpdate = $result.Output -match "exists"

    if ($Global:IsUpdate) {
        Write-Log "Existing deployment detected - performing update"
    } else {
        Write-Log "First-time deployment detected"
    }

    Invoke-RemoteCommand "sudo mkdir -p $($Global:DeployPath)/{images,logs}" | Out-Null
    Invoke-RemoteCommand "sudo chown -R $($Global:VpsUser):$($Global:VpsUser) $($Global:DeployPath)" | Out-Null

    Write-Success "Remote directories created"
}

function Send-DockerImages {
    Write-Log "Transferring Docker images to VPS (this may take a while)..."

    foreach ($service in $Services) {
        $tarFile    = Join-Path $Global:ImageExportDir "$service.tar"
        $remoteDest = "$($Global:VpsUser)@$($Global:VpsHost):$($Global:DeployPath)/images/"

        Write-Log "Transferring ${service}.tar..."
        Invoke-ScpUpload -Source $tarFile -Destination $remoteDest
    }

    Write-Success "All images transferred"
}

function Send-ComposeFile {
    Write-Log "Transferring Docker Compose file..."

    Invoke-ScpUpload `
        -Source (Join-Path $ScriptDir $ComposeFile) `
        -Destination "$($Global:VpsUser)@$($Global:VpsHost):$($Global:DeployPath)/"

    Write-Success "Compose file transferred"
}

function Send-EnvFile {
    Write-Log "Transferring environment configuration..."

    $vpsApiBase = [System.Environment]::GetEnvironmentVariable("VPS_API_BASE")
    if ([string]::IsNullOrEmpty($vpsApiBase)) {
        Stop-WithError "VPS_API_BASE is not set in .env file. Required for production deployment."
    }

    $tempEnv = [System.IO.Path]::GetTempFileName()

    try {
        Get-Content (Join-Path $ScriptDir $EnvFile) `
        | Where-Object { -not ($_ -match "^\s*VPS_") -and -not ($_ -match "^\s*RASPBERRY_PI_") } `
        | ForEach-Object {
            if ($_ -match "^NUXT_PUBLIC_API_BASE=") { "NUXT_PUBLIC_API_BASE=$vpsApiBase" } else { $_ }
        } | Set-Content $tempEnv -Encoding UTF8

        Invoke-ScpUpload `
            -Source $tempEnv `
            -Destination "$($Global:VpsUser)@$($Global:VpsHost):$($Global:DeployPath)/.env"
    } finally {
        Remove-Item $tempEnv -Force -ErrorAction SilentlyContinue
    }

    Write-Success "Environment file transferred (API base set to $vpsApiBase)"
}

function Send-KratosConfig {
    Write-Log "Transferring Kratos configuration files..."

    $kratosSrc       = Join-Path $ScriptDir "Homassy.Kratos"
    $remoteKratosDir = "$($Global:DeployPath)/Homassy.Kratos"
    $remoteDest      = "$($Global:VpsUser)@$($Global:VpsHost):$remoteKratosDir/"

    Invoke-RemoteCommand "sudo rm -rf $remoteKratosDir" -AllowFailure | Out-Null
    Invoke-RemoteCommand "sudo mkdir -p $remoteKratosDir/templates && sudo chown -R $($Global:VpsUser):$($Global:VpsUser) $remoteKratosDir" | Out-Null

    # Transfer kratos.production.yml and rename to kratos.yml on the remote
    Invoke-ScpUpload -Source (Join-Path $kratosSrc "kratos.production.yml") -Destination $remoteDest
    Invoke-RemoteCommand "mv $remoteKratosDir/kratos.production.yml $remoteKratosDir/kratos.yml" | Out-Null

    Invoke-ScpUpload -Source (Join-Path $kratosSrc "identity.schema.json") -Destination $remoteDest

    $templatesPath = Join-Path $kratosSrc "templates"
    if (Test-Path $templatesPath) {
        Invoke-ScpUpload -Source $templatesPath -Destination $remoteDest -Recursive
    }

    Write-Success "Kratos configuration transferred"
}

# =============================================================================
# Deployment Phase
# =============================================================================

function Import-ImagesOnVps {
    Write-Log "Loading Docker images on VPS..."

    $remoteScript = ($Services | ForEach-Object {
        "echo 'Loading $_...' && docker load -i $($Global:DeployPath)/images/$_.tar"
    }) -join " && "

    Invoke-RemoteCommand $remoteScript | Out-Null

    Write-Success "All images loaded on VPS"
}

function Stop-ExistingContainers {
    Write-Log "Checking for existing containers..."

    $result = Invoke-RemoteCommand "cd $($Global:DeployPath) && docker compose -f $ComposeFile ps -q 2>/dev/null" -AllowFailure

    if (-not [string]::IsNullOrEmpty($result.Output)) {
        Write-Log "Found running containers, stopping gracefully..."
        Invoke-RemoteCommand "cd $($Global:DeployPath) && docker compose -f $ComposeFile down --remove-orphans" | Out-Null
        Write-Success "Containers stopped"
    } else {
        Write-Log "No running containers found"
    }

    Write-Log "Cleaning up Kratos container (clearing mount cache)..."
    Invoke-RemoteCommand "docker rm -f homassy-kratos 2>/dev/null || true" -AllowFailure | Out-Null

    $volumeResult = Invoke-RemoteCommand "docker volume ls -q | grep -w $VolumeName" -AllowFailure
    if ([string]::IsNullOrEmpty($volumeResult.Output)) {
        if ($Global:IsUpdate) {
            Write-Warn "postgres-data volume does not exist (unexpected for update)"
        } else {
            Write-Log "postgres-data volume will be created on first startup"
        }
    } else {
        Write-Success "postgres-data volume preserved"
    }
}

function Start-RemoteServices {
    Write-Log "Starting services with production configuration..."

    $r = Invoke-RemoteCommand "test -f $($Global:DeployPath)/Homassy.Kratos/kratos.yml && echo ok || echo missing" -AllowFailure
    if ($r.Output -notmatch "ok") { Stop-WithError "kratos.yml is missing or not a file. Re-run deployment." }

    $r = Invoke-RemoteCommand "test -f $($Global:DeployPath)/Homassy.Kratos/identity.schema.json && echo ok || echo missing" -AllowFailure
    if ($r.Output -notmatch "ok") { Stop-WithError "identity.schema.json is missing or not a file. Re-run deployment." }

    Write-Success "Kratos config files verified"

    Invoke-RemoteCommand "docker network inspect $NetworkName >/dev/null 2>&1 || docker network create $NetworkName" | Out-Null

    Write-Log "Checking PostgreSQL 16 image..."
    $pgImages = Invoke-RemoteCommand "docker images -q $PostgresImage" -AllowFailure

    if ([string]::IsNullOrEmpty($pgImages.Output)) {
        Write-Log "Pulling PostgreSQL 16 image (with retry)..."
        $maxRetries = 3
        $pulled     = $false

        for ($retry = 0; $retry -lt $maxRetries -and -not $pulled; $retry++) {
            $pullResult = Invoke-RemoteCommand "docker pull $PostgresImage" -AllowFailure
            if ($pullResult.ExitCode -eq 0) {
                Write-Success "PostgreSQL image pulled"
                $pulled = $true
            } elseif ($retry -lt $maxRetries - 1) {
                Write-Warn "Pull failed, retrying ($($retry + 1)/$maxRetries)..."
                Start-Sleep 5
            } else {
                Stop-WithError "Failed to pull postgres image after $maxRetries attempts."
            }
        }
    } else {
        Write-Log "PostgreSQL image already exists, skipping pull"
    }

    Write-Log "Starting all services..."
    Invoke-RemoteCommand "cd $($Global:DeployPath) && docker compose -f $ComposeFile up -d" | Out-Null

    Write-Success "Services started"
}

function Test-ServiceHealth {
    Write-Log "Verifying service health (this may take up to 2 minutes)..."

    $maxWait  = 120
    $elapsed  = 0
    $interval = 5

    while ($elapsed -lt $maxWait) {
        $pgHealth       = (Invoke-RemoteCommand 'docker inspect --format="{{.State.Health.Status}}" homassy-postgres 2>/dev/null' -AllowFailure).Output.Trim()
        $apiHealth      = (Invoke-RemoteCommand 'docker inspect --format="{{.State.Health.Status}}" homassy-api 2>/dev/null' -AllowFailure).Output.Trim()
        $migratorStatus = (Invoke-RemoteCommand 'docker inspect --format="{{.State.Status}}" homassy-migrator 2>/dev/null' -AllowFailure).Output.Trim()
        $webStatus      = (Invoke-RemoteCommand 'docker inspect --format="{{.State.Status}}" homassy-web 2>/dev/null' -AllowFailure).Output.Trim()
        $webExitCode    = (Invoke-RemoteCommand 'docker inspect --format="{{.State.ExitCode}}" homassy-web 2>/dev/null' -AllowFailure).Output.Trim()

        if (-not $pgHealth)       { $pgHealth       = "starting" }
        if (-not $apiHealth)      { $apiHealth      = "starting" }
        if (-not $migratorStatus) { $migratorStatus = "starting" }
        if (-not $webStatus)      { $webStatus      = "starting" }
        if (-not $webExitCode)    { $webExitCode    = "0" }

        Write-Log "Status: Postgres=$pgHealth | API=$apiHealth | Migrator=$migratorStatus | Web=$webStatus (exit:$webExitCode) ($elapsed/$maxWait sec)"

        if ($webStatus -eq "exited" -and $webExitCode -ne "0") {
            $logs = (Invoke-RemoteCommand "docker logs --tail 50 homassy-web" -AllowFailure).Output
            Stop-WithError "Web container failed (exit $webExitCode).`nLast 50 log lines:`n$logs"
        }

        if ($pgHealth -eq "healthy" -and $apiHealth -eq "healthy" -and
            $migratorStatus -eq "exited" -and $webStatus -eq "running") {
            Write-Success "All services are healthy"
            return
        }

        Start-Sleep $interval
        $elapsed += $interval
    }

    Stop-WithError "Services did not become healthy within $maxWait seconds.`nCheck logs with: ssh $($Global:VpsUser)@$($Global:VpsHost) 'cd $($Global:DeployPath) && docker compose logs'"
}

# =============================================================================
# Cleanup Phase
# =============================================================================

function Remove-LocalTarFiles {
    Write-Log "Cleaning up local tar files..."
    if (Test-Path $Global:ImageExportDir) {
        Remove-Item $Global:ImageExportDir -Recurse -Force
    }
    Write-Success "Local cleanup complete"
}

function Remove-RemoteTarFiles {
    Write-Log "Cleaning up remote tar files..."
    Invoke-RemoteCommand "rm -rf $($Global:DeployPath)/images/*.tar" -AllowFailure | Out-Null
    Write-Success "Remote cleanup complete"
}

function Show-DeploymentStatus {
    Write-Host ""
    Write-Host "========================================"
    Write-Log "Deployment Status"
    Write-Host "========================================"
    Write-Host ""

    Write-Log "Running containers:"
    $containers = Invoke-RemoteCommand "cd $($Global:DeployPath) && docker compose -f $ComposeFile ps" -AllowFailure
    Write-Host $containers.Output
    Write-Host ""

    Write-Log "Volume status:"
    $volumes = Invoke-RemoteCommand "docker volume ls | grep $VolumeName || echo 'Volume not found'" -AllowFailure
    Write-Host $volumes.Output
    Write-Host ""

    Write-Host "========================================"
    Write-Success "Deployment completed successfully!"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "Access the application at:"
    Write-Host "  Frontend: http://$($Global:VpsHost):3000"
    Write-Host "  API:      http://$($Global:VpsHost):5226"
    Write-Host ""
    Write-Host "View logs with:"
    Write-Host "  ssh $($Global:VpsUser)@$($Global:VpsHost) 'cd $($Global:DeployPath) && docker compose logs -f'"
    Write-Host ""
}

# =============================================================================
# Main Execution Flow
# =============================================================================

function Main {
    Write-Host "========================================"
    Write-Log "Homassy Deployment to VPS"
    Write-Host "========================================"
    Write-Host ""

    Push-Location $ScriptDir

    try {
        # Pre-flight checks
        Test-DockerRunning
        Read-EnvFile
        Test-SshConnectivity
        Test-RemoteDiskSpace

        Write-Host ""; Write-Success "Pre-flight checks passed"; Write-Host ""

        # Build phase
        Build-Amd64Images
        Save-ImagesToTar

        Write-Host ""; Write-Success "Build phase complete"; Write-Host ""

        # Transfer phase
        New-RemoteDirectories
        Send-DockerImages
        Send-ComposeFile
        Send-EnvFile
        Send-KratosConfig

        Write-Host ""; Write-Success "Transfer phase complete"; Write-Host ""

        # Deployment phase
        Import-ImagesOnVps
        Stop-ExistingContainers
        Start-RemoteServices
        Test-ServiceHealth

        Write-Host ""; Write-Success "Deployment phase complete"; Write-Host ""

        # Cleanup
        Remove-LocalTarFiles
        Remove-RemoteTarFiles
        Close-SshControlMaster

        Show-DeploymentStatus

    } catch {
        Write-Host "[ERROR] Deployment failed: $_" -ForegroundColor Red
        Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
        exit 1
    } finally {
        Pop-Location
    }
}

# Run main function
Main
