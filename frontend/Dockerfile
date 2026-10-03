# Build context: ./frontend (see docker-compose.yml).
FROM node:24-alpine AS build
WORKDIR /src

# Install first so the dependency layer is cached until the lockfile changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/frontend/browser/ /usr/share/nginx/html/
