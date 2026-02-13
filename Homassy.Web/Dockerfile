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

# Build arguments for environment variables
ARG NUXT_PUBLIC_KRATOS_URL=http://localhost:4433
ARG NUXT_PUBLIC_API_BASE=http://localhost:5226

# Install build dependencies for native modules
RUN apk add --no-cache python3 make g++

COPY package*.json ./
RUN npm ci

COPY . .

# Memory optimizations for Nuxt/Nitro build
# Docker has 8GB - allow Node.js to use up to 4GB for heap
ENV NODE_OPTIONS="--max-old-space-size=4096"
ENV NITRO_PRESET=node-server
ENV GENERATE_SOURCEMAP=false
ENV NUXT_TELEMETRY_DISABLED=1
# Pass build args as env vars for Nuxt build
ENV NUXT_PUBLIC_KRATOS_URL=${NUXT_PUBLIC_KRATOS_URL}
ENV NUXT_PUBLIC_API_BASE=${NUXT_PUBLIC_API_BASE}

# Run build with memory cleanup between stages
RUN npm run build && \
    if [ ! -d ".output" ]; then \
        echo "ERROR: .output directory was not created!"; \
        exit 1; \
    fi && \
    if [ ! -f ".output/server/index.mjs" ]; then \
        echo "ERROR: .output/server/index.mjs was not created!"; \
        exit 1; \
    fi && \
    echo "Build successful - .output directory exists" && \
    rm -rf node_modules/.cache

# Production Stage
FROM node:22-alpine AS production
WORKDIR /app

# Copy the entire built output (includes all runtime dependencies)
COPY --from=build /app/.output ./.output

EXPOSE 3000
ENV NODE_ENV=production
CMD ["node", ".output/server/index.mjs"]
