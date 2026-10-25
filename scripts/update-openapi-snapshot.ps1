# API sözleşmesinin (OpenAPI) dondurulmuş dosyasını (docs/api/openapi.v1.json) yeniden üretir.
#
# Ne zaman: bir uç noktayı/parametreyi/yanıtı/yetkiyi BİLİNÇLİ olarak değiştirdikten sonra. OpenApiContractTests, üretilen belge
# dosyadan farklıysa kırılır; bu betik dosyayı günceller. Ardından `git diff docs/api/openapi.v1.json` ile farkı inceleyip commit edin.
#
# Neden container: Windows Smart App Control yerel test DLL'lerini bazen engeller (bkz. test-docker.ps1). Veritabanı gerekmez.
#
# Kullanım:  ./scripts/update-openapi-snapshot.ps1

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $repo 'docs/api'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$inner = @"
set -e
mkdir -p /work
tar -C /src --exclude=bin --exclude=obj --exclude=.git --exclude=.vs -cf - . | tar -C /work -xf -
cd /work
dotnet test tests/Hospital.IntegrationTests/Hospital.IntegrationTests.csproj --filter "FullyQualifiedName~OpenApiContractTests.Sozlesme_dondurulmus_dosyayla_ayni"
"@
$inner = $inner -replace "`r", ''

docker run --rm `
    -v "${repo}:/src:ro" `
    -v "${out}:/out" `
    -v hospital-nuget-cache:/root/.nuget/packages `
    -e OPENAPI_SNAPSHOT_UPDATE=1 `
    -e OPENAPI_SNAPSHOT_PATH=/out/openapi.v1.json `
    -e DOTNET_CLI_TELEMETRY_OPTOOUT=1 `
    -e DOTNET_NOLOGO=1 `
    mcr.microsoft.com/dotnet/sdk:9.0 `
    bash -c $inner

if ($LASTEXITCODE -eq 0) {
    Write-Host "Güncellendi: docs/api/openapi.v1.json  (git diff ile inceleyin)"
}

exit $LASTEXITCODE
