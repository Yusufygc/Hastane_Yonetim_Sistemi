# Derleme + testleri Linux .NET 9 SDK container'ında çalıştırır.
#
# Neden: Windows Smart App Control, yerelde derlenen imzasız DLL'leri (ör. Hospital.Api.dll) bazen
# "bilinmeyen" sayıp yüklemeyi reddeder (0x800711C7). Container içinde bu politika uygulanmaz.
# Önkoşul: Docker Desktop açık ve `docker compose up -d` ile PostgreSQL ve Mailpit çalışıyor.
#
# Kullanım:  ./scripts/test-docker.ps1 [dotnet test argümanları]
#   ./scripts/test-docker.ps1
#   ./scripts/test-docker.ps1 --filter "FullyQualifiedName~AuthFlowTests"

# docker, ilerleme çıktısını stderr'e yazar; PowerShell bunu hata saymasın diye 'Stop' kullanılmaz,
# sonuç $LASTEXITCODE ile denetlenir.
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$extra = $args -join ' '

$adminConnection = 'Host=host.docker.internal;Port=5432;Database=postgres;Username=hospital;Password=hospital_dev'

# Kaynak salt-okunur bağlanır; bin/obj/.git hariç kopyalanıp container içinde derlenir (Windows çıktısı bozulmaz).
$inner = @"
set -e
mkdir -p /work
tar -C /src --exclude=bin --exclude=obj --exclude=.git --exclude=.vs -cf - . | tar -C /work -xf -
cd /work
dotnet test Hospital.sln $extra
"@

# Git checkout bu dosyayı CRLF'e çevirmiş olabilir; bash'e \r karakteri gitmesin.
$inner = $inner -replace "`r", ''

docker run --rm `
    --add-host=host.docker.internal:host-gateway `
    -v "${repo}:/src:ro" `
    -v hospital-nuget-cache:/root/.nuget/packages `
    -e "TEST_POSTGRES_ADMIN=$adminConnection" `
    -e MAILPIT_HOST=host.docker.internal `
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 `
    -e DOTNET_NOLOGO=1 `
    mcr.microsoft.com/dotnet/sdk:9.0 `
    bash -c $inner

exit $LASTEXITCODE
