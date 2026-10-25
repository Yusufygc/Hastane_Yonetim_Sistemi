# Hastane / Klinik Randevu Sistemi API'si (Hospital.WebAPI) — çok aşamalı imaj.
#
#   docker build -t hospital-api .
#
# Çalıştırma (PostgreSQL + Mailpit ile birlikte): docker-compose.app.yml ve .env.example'a bakın.
# Gizli ayarlar imaja GÖMÜLMEZ; çalışma anında ortam değişkenleriyle verilir (Jwt__SigningKey,
# Security__DataProtection__Key, ConnectionStrings__Default, Seed__SuperAdmin__*, Email__Smtp__*).

# ---------- Derleme ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: bağımlılık geri yüklemesi (restore) kaynak değişince yeniden çalışmaz (katman önbelleği).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Core/Hospital.Domain/Hospital.Domain.csproj src/Core/Hospital.Domain/
COPY src/Core/Hospital.Application/Hospital.Application.csproj src/Core/Hospital.Application/
COPY src/Infrastructure/Hospital.Infrastructure/Hospital.Infrastructure.csproj src/Infrastructure/Hospital.Infrastructure/
COPY src/Infrastructure/Hospital.Persistence/Hospital.Persistence.csproj src/Infrastructure/Hospital.Persistence/
COPY src/Presentation/Hospital.WebAPI/Hospital.WebAPI.csproj src/Presentation/Hospital.WebAPI/
RUN dotnet restore src/Presentation/Hospital.WebAPI/Hospital.WebAPI.csproj

COPY src ./src
RUN dotnet publish src/Presentation/Hospital.WebAPI/Hospital.WebAPI.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Çalışma ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# 8080: root olmayan kullanıcı 1024 altı portu dinleyemez. Üretim ortamı varsayılandır (Scalar/OpenAPI açık değil;
# gerekirse OpenApi__Enabled=true).
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish .

# Root olmayan kullanıcı (aspnet imajı APP_UID tanımlar).
USER $APP_UID
ENTRYPOINT ["dotnet", "Hospital.WebAPI.dll"]
