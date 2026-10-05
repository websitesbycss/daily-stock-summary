# syntax=docker/dockerfile:1
# Build context: ./frontend

FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# Empty means "same origin": the app calls /api/... on the host that served it, and nginx proxies that to the API.
# The value is compiled into the public bundle, so it must never hold a secret.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# Runs as the non-root nginx user and listens on 8080.
FROM nginxinc/nginx-unprivileged:stable-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
