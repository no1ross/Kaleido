FROM node:22-alpine AS build
ARG BUILD_CONFIGURATION=container
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build -- --configuration $BUILD_CONFIGURATION

FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/priorauth-ui/browser /usr/share/nginx/html
EXPOSE 8080
