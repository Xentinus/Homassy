# Development Stage - Hot reload támogatással
FROM node:22-alpine AS development
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
EXPOSE 3000
ENV NODE_ENV=development
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# Build Stage
FROM node:22-alpine AS build
WORKDIR /app

# Install build dependencies for native modules
RUN apk add --no-cache python3 make g++

COPY package*.json ./
RUN npm ci

COPY . .

# Run build with verbose output and verify success
RUN npm run build && \
    if [ ! -d ".output" ]; then \
        echo "ERROR: .output directory was not created!"; \
        exit 1; \
    fi && \
    if [ ! -f ".output/server/index.mjs" ]; then \
        echo "ERROR: .output/server/index.mjs was not created!"; \
        exit 1; \
    fi && \
    echo "Build successful - .output directory exists"

# Production Stage
FROM node:22-alpine AS production
WORKDIR /app

# Copy the entire built output (includes all runtime dependencies)
COPY --from=build /app/.output ./.output

EXPOSE 3000
ENV NODE_ENV=production
CMD ["node", ".output/server/index.mjs"]
