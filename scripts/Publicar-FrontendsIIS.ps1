# Publica el panel y la tienda en IIS local. Ejecutar como administrador.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Ejecuta PowerShell como administrador.' }
if (-not (Test-Path "$env:windir\System32\inetsrv\rewrite.dll")) { throw 'Instala IIS URL Rewrite 2.1 antes de continuar.' }
Import-Module WebAdministration
$apps = @(
    @{Name='RouteOS-Web'; Port=5290; Project='delivery-frontend-web'; Output='dist\frontend-web\browser'; Index='index.csr.html'; Route='login'},
    @{Name='front-tienda'; Port=5330; Project='Ecommerce'; Output='dist\front-tienda\browser'; Index='index.html'; Route='productos'}
)
# Validar ambos sitios antes de realizar cambios.
foreach ($app in $apps) {
    if (Test-Path "IIS:\Sites\$($app.Name)") { throw "El sitio $($app.Name) ya existe. No se sobrescribio." }
    if (Get-NetTCPConnection -LocalPort $app.Port -State Listen -ErrorAction SilentlyContinue) { throw "Puerto $($app.Port) ocupado." }
}
foreach ($app in $apps) {
    $proyecto = Join-Path $raiz $app.Project
    Push-Location $proyecto
    try {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de $($app.Name)." }
    } finally { Pop-Location }
    $origen = Join-Path $proyecto $app.Output
    foreach ($archivo in @($app.Index,'web.config')) {
        if (-not (Test-Path (Join-Path $origen $archivo))) { throw "Falta $archivo en $origen." }
    }
}
foreach ($app in $apps) {
    $pool = "$($app.Name)-pool"
    $destino = Join-Path "$env:SystemDrive\inetpub" ($app.Name + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $destino | Out-Null
    Copy-Item -Path (Join-Path (Join-Path (Join-Path $raiz $app.Project) $app.Output) '*') -Destination $destino -Recurse -Force
    if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    & icacls.exe $destino /grant "IIS AppPool\${pool}:(OI)(CI)RX" /grant '*S-1-5-17:(OI)(CI)RX' /T | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "No se pudieron asignar permisos a $destino." }
    New-Website -Name $app.Name -Port $app.Port -IPAddress '*' -HostHeader 'localhost' -PhysicalPath $destino -ApplicationPool $pool | Out-Null
    Start-Website -Name $app.Name
}
foreach ($app in $apps) {
    foreach ($ruta in @('', $app.Route)) {
        $url = "http://localhost:$($app.Port)/$ruta"
        $respuesta = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 30
        Write-Host "$($app.Name): HTTP $($respuesta.StatusCode) - $url"
    }
}
Write-Host 'Publicacion completada. Panel: http://localhost:5290 ; tienda: http://localhost:5330'
Write-Host 'Ambos usan http://localhost:5289. Comprueba login, catalogo y pedidos en el navegador.'
