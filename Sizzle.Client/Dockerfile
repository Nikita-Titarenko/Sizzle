# Build stage: compile the Angular app.
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

# Runtime stage: serve the static files and proxy API traffic to the backend.
FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/Sizzle.Client/browser /usr/share/nginx/html
EXPOSE 80
