# Hastane Randevu arayüzü (web/) — çok aşamalı imaj: Node ile derle, nginx ile sun.
#
#   docker build -t hospital-web web
#
# Çalıştırma (API ile birlikte): kökteki docker-compose.app.yml. nginx statik dosyaları sunar ve /api/ isteklerini API'ye
# vekiller; tarayıcı için aynı origin olduğundan CORS gerekmez. Gizli bilgi imaja girmez (arayüz herkese açık statik dosyadır).

# ---------- Derleme ----------
FROM node:22-alpine AS build
WORKDIR /app

# Önce yalnızca bağımlılık dosyaları: kaynak değişince npm ci yeniden çalışmaz (katman önbelleği).
COPY package.json package-lock.json .npmrc ./
RUN npm ci

COPY . .
# VITE_API_URL verilmez: istemci göreli /api yoluna istek atar (nginx vekiller).
RUN npm run build

# ---------- Çalışma ----------
# nginx-unprivileged: root olmayan kullanıcı, 8080. /etc/nginx/templates/*.template açılışta ortam değişkenleriyle (API_UPSTREAM) işlenir.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY nginx/security-headers.conf /etc/nginx/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html

ENV API_UPSTREAM=api:8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=5 \
  CMD wget -qO- http://127.0.0.1:8080/healthz >/dev/null || exit 1
