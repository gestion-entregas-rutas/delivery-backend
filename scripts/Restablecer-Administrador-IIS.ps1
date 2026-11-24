$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Ejecuta como administrador.' }
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
$ruta = (& $appcmd list vdir 'delivery-api/' /text:physicalPath 2>$null | Select-Object -First 1).Trim()
if (-not $ruta -or -not (Test-Path (Join-Path $ruta 'Api.dll'))) { throw 'No se encontro la publicacion de delivery-api.' }
$clave = Read-Host 'Nueva clave temporal del administrador (12-128 caracteres, mayuscula, minuscula, numero y simbolo)' -AsSecureString
$anterior = @{}
foreach ($nombre in @('Bootstrap__Clave','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT')) {
    $anterior[$nombre] = [Environment]::GetEnvironmentVariable($nombre,'Process')
}
try {
    $env:Bootstrap__Clave = [Net.NetworkCredential]::new('', $clave).Password
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:DOTNET_ENVIRONMENT = 'Production'
    $proyecto = Split-Path -Parent $PSScriptRoot
    $temporal = Join-Path $env:TEMP ('delivery-admin-' + [Guid]::NewGuid().ToString('N'))
    Push-Location $proyecto
    try {
        & dotnet publish Api/Api.csproj -c Release -o $temporal -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del comando de restablecimiento.' }
    }
    finally { Pop-Location }
    # La configuracion de IIS contiene la conexion local. El comando se ejecuta en la publicacion temporal.
    Copy-Item (Join-Path $ruta 'appsettings.Production.json') (Join-Path $temporal 'appsettings.Production.json') -Force
    Push-Location $temporal
    try {
        & dotnet (Join-Path $temporal 'Api.dll') --restablecer-admin-local
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo restablecer. Revisa el error anterior.' }
    }
    finally { Pop-Location }
}
finally {
    foreach ($nombre in $anterior.Keys) { [Environment]::SetEnvironmentVariable($nombre,$anterior[$nombre],'Process') }
    $clave.Dispose()
}
