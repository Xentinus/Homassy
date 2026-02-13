#!/bin/bash
# migrate-users-to-kratos.sh
# Migrates existing users from Homassy database to Kratos identity system

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
DRY_RUN=false
VERIFY_ONLY=false
STATS_ONLY=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --verify)
            VERIFY_ONLY=true
            shift
            ;;
        --stats)
            STATS_ONLY=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --dry-run    Simulate migration without making changes"
            echo "  --verify     Only verify migration status"
            echo "  --stats      Only show migration statistics"
            echo "  -h, --help   Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${GREEN}=== Homassy Kratos User Migration ===${NC}"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Error: Docker is not running${NC}"
    exit 1
fi

# Check if required containers are running
if ! docker-compose ps | grep -q "homassy-postgres.*Up"; then
    echo -e "${YELLOW}Warning: PostgreSQL container is not running${NC}"
    echo "Starting PostgreSQL..."
    docker-compose up -d postgres
    sleep 5
fi

if ! docker-compose ps | grep -q "homassy-kratos.*Up"; then
    echo -e "${YELLOW}Warning: Kratos container is not running${NC}"
    echo "Starting Kratos..."
    docker-compose up -d homassy.kratos
    sleep 10
fi

# Check Kratos health
echo "Checking Kratos health..."
if ! curl -s http://localhost:4434/health/alive | grep -q "ok"; then
    echo -e "${RED}Error: Kratos is not healthy${NC}"
    echo "Please ensure Kratos is running and healthy before migration"
    exit 1
fi
echo -e "${GREEN}✓ Kratos is healthy${NC}"

# Build the migrator
echo ""
echo "Building Homassy.Migrator..."
cd "$(dirname "$0")/.."
dotnet build -c Release --no-restore

# Run the appropriate command
cd "$(dirname "$0")/../bin/Release/net10.0"

if [ "$STATS_ONLY" = true ]; then
    echo ""
    echo -e "${YELLOW}Showing migration statistics...${NC}"
    dotnet Homassy.Migrator.dll kratos-stats
elif [ "$VERIFY_ONLY" = true ]; then
    echo ""
    echo -e "${YELLOW}Verifying migration status...${NC}"
    dotnet Homassy.Migrator.dll verify-kratos
else
    if [ "$DRY_RUN" = true ]; then
        echo ""
        echo -e "${YELLOW}Starting DRY RUN migration (no changes will be made)...${NC}"
        dotnet Homassy.Migrator.dll migrate-to-kratos --dry-run
    else
        echo ""
        echo -e "${YELLOW}Starting user migration to Kratos...${NC}"
        echo -e "${RED}⚠️  This will modify production data!${NC}"
        read -p "Are you sure you want to continue? (yes/no): " confirm
        if [ "$confirm" != "yes" ]; then
            echo "Migration cancelled."
            exit 0
        fi
        
        dotnet Homassy.Migrator.dll migrate-to-kratos
        
        echo ""
        echo -e "${GREEN}Migration complete! Verifying...${NC}"
        dotnet Homassy.Migrator.dll verify-kratos
    fi
fi

echo ""
echo -e "${GREEN}Done!${NC}"
