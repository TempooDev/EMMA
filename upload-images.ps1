# Definición de variables
$githubUser = "tempoodev"

Write-Host "Ejecutando Aspire Push y detectando servicios..." -ForegroundColor White

# Ejecutar aspire push y capturar la salida
$output = aspire do push | Out-String
Write-Host $output

# 1. Extraer el Tag
if ($output -match "aspire-deploy-\d{14}") {
    $aspireTag = $matches[0]
    Write-Host "`nTag detectado: $aspireTag" -ForegroundColor Green
} else {
    Write-Host "`nError: No se pudo encontrar el tag de despliegue." -ForegroundColor Red
    exit
}

# 2. Detectar Servicios (Busca el patrón 'Successfully tagged NAME as')
$detectedServices = [System.Collections.Generic.HashSet[string]]::new()
$output -split "`r`n" | ForEach-Object {
    if ($_ -match "Successfully tagged (\S+) as") {
        $null = $detectedServices.Add($matches[1])
    }
}

if ($detectedServices.Count -eq 0) {
    Write-Host "No se detectaron servicios para subir." -ForegroundColor Yellow
    exit
}

Write-Host "Servicios detectados: $($detectedServices -join ', ')" -ForegroundColor Gray

# 3. Procesar y Subir
foreach ($name in $detectedServices) {
    Write-Host "--- Procesando: $name ---" -ForegroundColor Cyan
    $localImage = "${name}:${aspireTag}"
    $remoteImage = "ghcr.io/${githubUser}/${name}:latest"
    
    podman tag $localImage $remoteImage
    Write-Host "Subiendo $remoteImage..." -ForegroundColor Yellow
    podman push $remoteImage
}

Write-Host "`n¡Despliegue dinámico completado!" -ForegroundColor Green