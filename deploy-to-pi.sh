#!/usr/bin/env bash

################################################################################
# Homassy Deployment Script for Raspberry Pi 4
#
# This script builds ARM64 Docker images on Mac, transfers them to a
# Raspberry Pi via SSH, and deploys the application in production mode.
#
# Usage: ./deploy-to-pi.sh
################################################################################

set -euo pipefail

# =============================================================================
# Configuration and Constants
# =============================================================================

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly COMPOSE_FILE="docker-compose.yml"
readonly COMPOSE_RELEASE_FILE="docker-compose.release.yml"
readonly ENV_FILE=".env"
readonly SERVICES=("homassymigrator" "homassyapi" "homassyweb")
readonly POSTGRES_IMAGE="postgres:16"
readonly VOLUME_NAME="postgres-data"
readonly NETWORK_NAME="homassy-network"

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# Global variables (set by verify_env_file)
RASPBERRY_PI_USER=""
RASPBERRY_PI_HOST=""
RASPBERRY_PI_PORT="22"
RASPBERRY_PI_SSH_KEY=""
RASPBERRY_PI_PASSWORD=""
DEPLOY_PATH="/opt/homassy"
IMAGE_EXPORT_DIR="/tmp/homassy-images"
SSH_CMD=""
SCP_CMD=""
RSYNC_CMD=""
IS_UPDATE=false
USE_SSHPASS=false

# =============================================================================
# Utility Functions
# =============================================================================

log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
    exit 1
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

success() {
    echo -e "${GREEN}✓${NC} $1"
}

cleanup_on_error() {
    local exit_code=$?
    error "Deployment failed with exit code $exit_code"
}

trap cleanup_on_error ERR

# =============================================================================
# Pre-flight Checks
# =============================================================================

verify_docker_running() {
    log "Checking if Docker is running..."

    if ! docker info &>/dev/null; then
        error "Docker is not running. Please start Docker Desktop."
    fi

    success "Docker is running"
}

verify_buildx_available() {
    log "Checking Docker Buildx availability..."

    if ! docker buildx version &>/dev/null; then
        error "Docker Buildx is not available. Please install or update Docker Desktop."
    fi

    # Check if builder exists, create if not
    if ! docker buildx inspect homassy-builder &>/dev/null 2>&1; then
        log "Creating new builder with ARM64 support..."
        docker buildx create \
            --name homassy-builder \
            --driver docker-container \
            --platform linux/arm64,linux/amd64 \
            --use || error "Failed to create buildx builder"
        docker buildx inspect --bootstrap || error "Failed to bootstrap builder"
    else
        log "Using existing homassy-builder"
        docker buildx use homassy-builder || error "Failed to use homassy-builder"
    fi

    success "Docker Buildx is available with ARM64 support"
}

verify_env_file() {
    log "Verifying .env file..."

    if [[ ! -f "$SCRIPT_DIR/$ENV_FILE" ]]; then
        error ".env file not found in project root ($SCRIPT_DIR)"
    fi

    # Load environment variables
    set -a
    # shellcheck disable=SC1090
    source "$SCRIPT_DIR/$ENV_FILE"
    set +a

    # Validate required SSH variables
    local required_vars=("RASPBERRY_PI_USER" "RASPBERRY_PI_HOST")
    for var in "${required_vars[@]}"; do
        if [[ -z "${!var}" ]]; then
            error "Required variable $var is not set in .env file"
        fi
    done

    # Check authentication method
    if [[ -z "$RASPBERRY_PI_SSH_KEY" ]] && [[ -z "$RASPBERRY_PI_PASSWORD" ]]; then
        error "Either RASPBERRY_PI_SSH_KEY or RASPBERRY_PI_PASSWORD must be set in .env file"
    fi

    # Set defaults and export to global variables
    RASPBERRY_PI_PORT=${RASPBERRY_PI_PORT:-22}
    DEPLOY_PATH=${DEPLOY_PATH:-/opt/homassy}
    IMAGE_EXPORT_DIR=${IMAGE_EXPORT_DIR:-/tmp/homassy-images}

    # Check if using password authentication
    if [[ -n "$RASPBERRY_PI_PASSWORD" ]]; then
        USE_SSHPASS=true
        # Check if sshpass is installed
        if ! command -v sshpass &>/dev/null; then
            error "sshpass is not installed. Install it with: brew install hudochenkov/sshpass/sshpass"
        fi
    fi

    success "Environment configuration loaded"
}

verify_ssh_connectivity() {
    log "Testing SSH connectivity to Raspberry Pi..."

    local ssh_base_opts="-o ConnectTimeout=10 -o StrictHostKeyChecking=no"
    local ssh_cmd=""
    local scp_cmd=""
    local rsync_cmd=""

    if [[ "$USE_SSHPASS" == true ]]; then
        # Password authentication - use environment variable to avoid shell escaping issues
        export SSHPASS="$RASPBERRY_PI_PASSWORD"
        ssh_cmd="sshpass -e ssh $ssh_base_opts -p $RASPBERRY_PI_PORT $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST"
        scp_cmd="sshpass -e scp $ssh_base_opts -P $RASPBERRY_PI_PORT"
        # For rsync, we need to wrap the entire ssh command with sshpass
        rsync_cmd="rsync -e \"sshpass -e ssh $ssh_base_opts -p $RASPBERRY_PI_PORT\""
    else
        # SSH key authentication
        if [[ -n "$RASPBERRY_PI_SSH_KEY" ]] && [[ -f "$RASPBERRY_PI_SSH_KEY" ]]; then
            # Check SSH key permissions
            if [[ "$(uname)" == "Darwin" ]]; then
                local perms=$(stat -f "%OLp" "$RASPBERRY_PI_SSH_KEY")
            else
                local perms=$(stat -c "%a" "$RASPBERRY_PI_SSH_KEY")
            fi

            if [[ "$perms" != "600" ]]; then
                warn "SSH key permissions are $perms, fixing to 600..."
                chmod 600 "$RASPBERRY_PI_SSH_KEY"
            fi

            ssh_cmd="ssh $ssh_base_opts -i $RASPBERRY_PI_SSH_KEY -p $RASPBERRY_PI_PORT $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST"
            scp_cmd="scp $ssh_base_opts -i $RASPBERRY_PI_SSH_KEY -P $RASPBERRY_PI_PORT"
            rsync_cmd="rsync -e \"ssh $ssh_base_opts -i $RASPBERRY_PI_SSH_KEY -p $RASPBERRY_PI_PORT\""
        else
            error "RASPBERRY_PI_SSH_KEY is set but file does not exist: $RASPBERRY_PI_SSH_KEY"
        fi
    fi

    # Test connection
    if ! eval "$ssh_cmd 'echo SSH connection successful'" &>/dev/null; then
        error "Cannot connect to Raspberry Pi via SSH at $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST:$RASPBERRY_PI_PORT
Please check:
  - SSH credentials in .env file
  - Network connectivity
  - SSH password/key is correct
  - Raspberry Pi is powered on and accessible"
    fi

    # Store commands for reuse
    SSH_CMD="$ssh_cmd"
    SCP_CMD="$scp_cmd"
    RSYNC_CMD="$rsync_cmd"

    success "SSH connectivity verified"
}

check_remote_disk_space() {
    log "Checking available disk space on Raspberry Pi..."

    local available=$(eval "$SSH_CMD 'df -BG $DEPLOY_PATH 2>/dev/null | tail -1 | awk '\"'\"'{print \$4}'\"'\"' | sed '\"'\"'s/G//'\"'\"''" || echo "0")
    local required=3  # GB minimum

    if [[ $available -lt $required ]]; then
        warn "Low disk space: ${available}GB available, ${required}GB recommended"
        warn "Cleaning up old Docker images..."
        eval "$SSH_CMD 'docker image prune -af --filter \"until=48h\"'" || warn "Failed to prune old images"
    else
        success "Sufficient disk space available (${available}GB)"
    fi
}

# =============================================================================
# Build Phase
# =============================================================================

setup_buildx_builder() {
    log "Setting up Docker Buildx builder..."

    # Bootstrap builder if needed
    if ! docker buildx inspect homassy-builder &>/dev/null; then
        error "Buildx builder not found. Run verify_buildx_available first."
    fi

    docker buildx use homassy-builder || error "Failed to use buildx builder"
    docker buildx inspect --bootstrap &>/dev/null || true

    success "Buildx builder ready"
}

build_arm64_images() {
    log "Building ARM64 Docker images (this may take several minutes)..."

    # Build Migrator
    log "Building migrator image..."
    docker buildx build \
        --platform linux/arm64 \
        --target final \
        -t homassymigrator:latest \
        -f Homassy.Migrator/Dockerfile \
        --load \
        . || error "Failed to build migrator image"

    # Build API
    log "Building API image..."
    docker buildx build \
        --platform linux/arm64 \
        --target final \
        --build-arg BUILD_CONFIGURATION=Release \
        -t homassyapi:latest \
        -f Homassy.API/Dockerfile \
        --load \
        . || error "Failed to build API image"

    # Build Web
    log "Building Web image..."
    docker buildx build \
        --platform linux/arm64 \
        --target production \
        -t homassyweb:latest \
        -f Homassy.Web/Dockerfile \
        --load \
        --progress=plain \
        Homassy.Web || error "Failed to build Web image"

    success "All ARM64 images built successfully"
}

save_images_to_tar() {
    log "Exporting Docker images to tar archives..."

    mkdir -p "$IMAGE_EXPORT_DIR"

    # Export each custom service image
    for service in "${SERVICES[@]}"; do
        local tar_file="$IMAGE_EXPORT_DIR/${service}.tar"

        log "Saving ${service}:latest..."
        docker save -o "$tar_file" "${service}:latest" || \
            error "Failed to save ${service} image"
    done

    success "Images exported to $IMAGE_EXPORT_DIR"
}

# =============================================================================
# Transfer Phase
# =============================================================================

create_remote_directories() {
    log "Creating directory structure on Raspberry Pi..."

    # Check if deployment directory exists (determines if this is an update)
    if eval "$SSH_CMD 'test -d $DEPLOY_PATH'" 2>/dev/null; then
        log "Existing deployment detected - performing update"
        IS_UPDATE=true
    else
        log "First-time deployment detected"
        IS_UPDATE=false
    fi

    eval "$SSH_CMD 'sudo mkdir -p $DEPLOY_PATH/{images,logs}'" || \
        error "Failed to create remote directories"

    eval "$SSH_CMD 'sudo chown -R $RASPBERRY_PI_USER:$RASPBERRY_PI_USER $DEPLOY_PATH'" || \
        error "Failed to set directory permissions"

    success "Remote directories created"
}

transfer_docker_images() {
    log "Transferring Docker images to Raspberry Pi (this may take a while)..."

    for service in "${SERVICES[@]}"; do
        local tar_file="$IMAGE_EXPORT_DIR/${service}.tar"
        local remote_path="$RASPBERRY_PI_USER@$RASPBERRY_PI_HOST:$DEPLOY_PATH/images/"

        log "Transferring ${service}.tar..."

        # Use rsync if available for resume capability, otherwise scp
        if command -v rsync &>/dev/null; then
            eval "$RSYNC_CMD -avz --progress '$tar_file' '$remote_path'" || \
                error "Failed to transfer ${service}.tar"
        else
            eval "$SCP_CMD '$tar_file' '$remote_path'" || \
                error "Failed to transfer ${service}.tar"
        fi
    done

    success "All images transferred"
}

transfer_compose_files() {
    log "Transferring Docker Compose files..."

    eval "$SCP_CMD \
        '$SCRIPT_DIR/$COMPOSE_FILE' \
        '$SCRIPT_DIR/$COMPOSE_RELEASE_FILE' \
        '$RASPBERRY_PI_USER@$RASPBERRY_PI_HOST:$DEPLOY_PATH/'" || \
        error "Failed to transfer compose files"

    success "Compose files transferred"
}

transfer_env_file() {
    log "Transferring environment configuration..."

    # Create a deployment-specific .env (filter out SSH credentials and update API base)
    local temp_env=$(mktemp)

    # Filter out RASPBERRY_PI_ variables and update NUXT_PUBLIC_API_BASE
    grep -v "^RASPBERRY_PI" "$SCRIPT_DIR/$ENV_FILE" | \
        sed "s|NUXT_PUBLIC_API_BASE=.*|NUXT_PUBLIC_API_BASE=http://$RASPBERRY_PI_HOST:5226|g" > "$temp_env"

    eval "$SCP_CMD '$temp_env' '$RASPBERRY_PI_USER@$RASPBERRY_PI_HOST:$DEPLOY_PATH/.env'" || \
        error "Failed to transfer .env file"

    rm "$temp_env"

    success "Environment file transferred"
}

# =============================================================================
# Deployment Phase
# =============================================================================

load_images_on_pi() {
    log "Loading Docker images on Raspberry Pi..."

    for service in "${SERVICES[@]}"; do
        log "Loading ${service}..."

        eval "$SSH_CMD 'docker load -i $DEPLOY_PATH/images/${service}.tar'" || \
            error "Failed to load ${service} image"
    done

    success "All images loaded on Raspberry Pi"
}

stop_existing_containers() {
    log "Checking for existing containers..."

    # Check if containers are running
    local running=$(eval "$SSH_CMD 'cd $DEPLOY_PATH && docker compose ps -q 2>/dev/null'" || echo "")

    if [[ -n "$running" ]]; then
        log "Found running containers, stopping gracefully..."

        # Use docker compose down but preserve volumes
        eval "$SSH_CMD 'cd $DEPLOY_PATH && docker compose down --remove-orphans'" || \
            error "Failed to stop containers"

        success "Containers stopped"
    else
        log "No running containers found"
    fi

    # Verify postgres-data volume exists and warn if not
    local volume_exists=$(eval "$SSH_CMD 'docker volume ls -q | grep -w $VOLUME_NAME'" || echo "")

    if [[ -z "$volume_exists" ]]; then
        if [[ "$IS_UPDATE" == true ]]; then
            warn "postgres-data volume does not exist (unexpected for update)"
        else
            log "postgres-data volume will be created on first startup"
        fi
    else
        success "postgres-data volume preserved"
    fi
}

start_services() {
    log "Starting services with production configuration..."

    # Ensure network exists
    eval "$SSH_CMD 'docker network inspect $NETWORK_NAME >/dev/null 2>&1 || docker network create $NETWORK_NAME'" || \
        error "Failed to create Docker network"

    # Pull postgres image (official ARM64 image) with retry logic
    log "Checking PostgreSQL 16 image..."
    local postgres_exists=$(eval "$SSH_CMD 'docker images -q $POSTGRES_IMAGE'" || echo "")

    if [[ -z "$postgres_exists" ]]; then
        log "Pulling PostgreSQL 16 image (with retry)..."
        local max_retries=3
        local retry=0

        while [[ $retry -lt $max_retries ]]; do
            if eval "$SSH_CMD 'docker pull --platform linux/arm64 $POSTGRES_IMAGE'"; then
                success "PostgreSQL image pulled"
                break
            else
                retry=$((retry + 1))
                if [[ $retry -lt $max_retries ]]; then
                    warn "Pull failed, retrying ($retry/$max_retries)..."
                    sleep 5
                else
                    error "Failed to pull postgres image after $max_retries attempts.
Please check internet connection on Raspberry Pi and try again.
You can also manually pull the image: ssh $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST 'docker pull postgres:16'"
                fi
            fi
        done
    else
        log "PostgreSQL image already exists, skipping pull"
    fi

    # Start services using both compose files
    log "Starting all services..."
    eval "$SSH_CMD 'cd $DEPLOY_PATH && docker compose -f $COMPOSE_FILE -f $COMPOSE_RELEASE_FILE up -d'" || \
        error "Failed to start services"

    success "Services started"
}

verify_service_health() {
    log "Verifying service health (this may take up to 2 minutes)..."

    local max_wait=120  # 2 minutes
    local elapsed=0
    local interval=5

    while [[ $elapsed -lt $max_wait ]]; do
        # Check postgres health
        local pg_health=$(eval "$SSH_CMD 'docker inspect --format=\"{{.State.Health.Status}}\" homassy-postgres 2>/dev/null'" || echo "starting")

        # Check API health
        local api_health=$(eval "$SSH_CMD 'docker inspect --format=\"{{.State.Health.Status}}\" homassy-api 2>/dev/null'" || echo "starting")

        # Check migrator status (should complete successfully)
        local migrator_status=$(eval "$SSH_CMD 'docker inspect --format=\"{{.State.Status}}\" homassy-migrator 2>/dev/null'" || echo "running")

        # Check web status
        local web_status=$(eval "$SSH_CMD 'docker inspect --format=\"{{.State.Status}}\" homassy-web 2>/dev/null'" || echo "starting")
        local web_exit_code=$(eval "$SSH_CMD 'docker inspect --format=\"{{.State.ExitCode}}\" homassy-web 2>/dev/null'" || echo "0")

        log "Status: Postgres=$pg_health | API=$api_health | Migrator=$migrator_status | Web=$web_status (exit:$web_exit_code) ($elapsed/$max_wait sec)"

        # If web container exited with error, show logs and fail immediately
        if [[ "$web_status" == "exited" ]] && [[ "$web_exit_code" != "0" ]]; then
            error "Web container failed to start with exit code $web_exit_code.
Last 50 lines of logs:
$(eval "$SSH_CMD 'docker logs --tail 50 homassy-web'")"
        fi

        if [[ "$pg_health" == "healthy" ]] && \
           [[ "$api_health" == "healthy" ]] && \
           [[ "$migrator_status" == "exited" ]] && \
           [[ "$web_status" == "running" ]]; then
            success "All services are healthy"
            return 0
        fi

        sleep $interval
        elapsed=$((elapsed + interval))
    done

    error "Services did not become healthy within $max_wait seconds. Check logs with:
  ssh $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST 'cd $DEPLOY_PATH && docker compose logs'"
}

# =============================================================================
# Cleanup Phase
# =============================================================================

cleanup_local_tar_files() {
    log "Cleaning up local tar files..."

    rm -rf "$IMAGE_EXPORT_DIR"

    success "Local cleanup complete"
}

cleanup_remote_tar_files() {
    log "Cleaning up remote tar files..."

    eval "$SSH_CMD 'rm -rf $DEPLOY_PATH/images/*.tar'" || \
        warn "Failed to clean remote tar files"

    success "Remote cleanup complete"
}

show_deployment_status() {
    echo ""
    echo "========================================"
    log "Deployment Status"
    echo "========================================"
    echo ""

    # Show running containers
    log "Running containers:"
    eval "$SSH_CMD 'cd $DEPLOY_PATH && docker compose ps'"
    echo ""

    # Show volume status
    log "Volume status:"
    eval "$SSH_CMD 'docker volume ls | grep $VOLUME_NAME || echo \"Volume not found\"'"
    echo ""

    echo "========================================"
    success "Deployment completed successfully!"
    echo "========================================"
    echo ""
    echo "Access the application at:"
    echo "  Frontend: http://$RASPBERRY_PI_HOST:3000"
    echo "  API:      http://$RASPBERRY_PI_HOST:5226"
    echo ""
    echo "View logs with:"
    echo "  ssh $RASPBERRY_PI_USER@$RASPBERRY_PI_HOST 'cd $DEPLOY_PATH && docker compose logs -f'"
    echo ""
}

# =============================================================================
# Main Execution Flow
# =============================================================================

main() {
    echo "========================================"
    log "Homassy Deployment to Raspberry Pi"
    echo "========================================"
    echo ""

    # Pre-flight checks
    verify_docker_running
    verify_buildx_available
    verify_env_file
    verify_ssh_connectivity
    check_remote_disk_space

    echo ""
    success "Pre-flight checks passed"
    echo ""

    # Build phase
    setup_buildx_builder
    build_arm64_images
    save_images_to_tar

    echo ""
    success "Build phase complete"
    echo ""

    # Transfer phase
    create_remote_directories
    transfer_docker_images
    transfer_compose_files
    transfer_env_file

    echo ""
    success "Transfer phase complete"
    echo ""

    # Deployment phase
    load_images_on_pi
    stop_existing_containers
    start_services
    verify_service_health

    echo ""
    success "Deployment phase complete"
    echo ""

    # Cleanup phase
    cleanup_local_tar_files
    cleanup_remote_tar_files

    show_deployment_status
}

# Run main function
main "$@"
