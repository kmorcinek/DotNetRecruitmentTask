#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PRODUCT_PORT=5044
STOCK_PORT=5132
PRODUCT_PID=""
STOCK_PID=""

# Cleanup function
cleanup() {
    echo -e "\n${YELLOW}Cleaning up...${NC}"

    if [ ! -z "$PRODUCT_PID" ] && kill -0 $PRODUCT_PID 2>/dev/null; then
        echo "Stopping ProductService (PID: $PRODUCT_PID)"
        kill $PRODUCT_PID 2>/dev/null || true
    fi

    if [ ! -z "$STOCK_PID" ] && kill -0 $STOCK_PID 2>/dev/null; then
        echo "Stopping StockService (PID: $STOCK_PID)"
        kill $STOCK_PID 2>/dev/null || true
    fi

    # Kill any remaining processes on the ports
    lsof -ti :$PRODUCT_PORT | xargs kill -9 2>/dev/null || true
    lsof -ti :$STOCK_PORT | xargs kill -9 2>/dev/null || true

    echo -e "${GREEN}Cleanup complete${NC}"
}

# Register cleanup on script exit
trap cleanup EXIT INT TERM

echo -e "${YELLOW}=== E2E Test Runner ===${NC}\n"

# Step 1: Kill existing processes on ports
echo -e "${YELLOW}[1/6] Killing existing processes on ports $PRODUCT_PORT and $STOCK_PORT...${NC}"
lsof -ti :$PRODUCT_PORT | xargs kill -9 2>/dev/null || true
lsof -ti :$STOCK_PORT | xargs kill -9 2>/dev/null || true
sleep 1

# Step 2: Build solution
echo -e "\n${YELLOW}[2/6] Building solution...${NC}"
dotnet build

# Step 3: Start infrastructure with docker-compose
echo -e "\n${YELLOW}[3/6] Starting infrastructure (PostgreSQL, RabbitMQ)...${NC}"
docker compose up -d postgres-product postgres-inventory rabbitmq
sleep 3

# Step 4: Start ProductService
echo -e "\n${YELLOW}[4/6] Starting ProductService on port $PRODUCT_PORT...${NC}"
cd src/ProductService/ProductService.API
dotnet run --no-build > /dev/null 2>&1 &
PRODUCT_PID=$!
cd ../../..
echo "ProductService started (PID: $PRODUCT_PID)"

# Step 5: Start StockService
echo -e "\n${YELLOW}[5/6] Starting StockService on port $STOCK_PORT...${NC}"
cd src/Stock/Stock.API
dotnet run --no-build > /dev/null 2>&1 &
STOCK_PID=$!
cd ../../..
echo "StockService started (PID: $STOCK_PID)"

# Wait for services to be ready
echo -e "\n${YELLOW}Waiting for services to be ready...${NC}"
max_attempts=30
attempt=0

while [ $attempt -lt $max_attempts ]; do
    if curl -s http://localhost:$PRODUCT_PORT/health > /dev/null 2>&1 && \
       curl -s http://localhost:$STOCK_PORT/health > /dev/null 2>&1; then
        echo -e "${GREEN}Services are ready!${NC}"
        break
    fi

    attempt=$((attempt + 1))
    if [ $attempt -eq $max_attempts ]; then
        echo -e "${RED}Services failed to start within timeout${NC}"
        exit 1
    fi

    echo -n "."
    sleep 1
done

# Step 6: Run E2E tests
echo -e "\n${YELLOW}[6/6] Running E2E tests...${NC}\n"
dotnet test tests/E2E.Tests/E2E.Tests.csproj --no-build

echo -e "\n${GREEN}=== All tests completed successfully ===${NC}"
