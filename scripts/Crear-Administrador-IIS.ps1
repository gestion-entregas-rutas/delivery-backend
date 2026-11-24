param([string]$Usuario)
$ErrorActionPreference = 'Stop'
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
if (-not (Test-Path $appcmd)) { throw 'IIS no esta instalado.' }
$ruta = (& $appcmd list vdir 'delivery-api/' /text:physicalPath 2>$null | Select-Object -First 1).Trim()
if (-not $ruta -or -not (Test-Path (Join-Path $ruta 'Api.dll'))) { throw 'No se encontro la publicacion del sitio delivery-api.' }
if (-not $Usuario) { $Usuario = Read-Host 'Nombre del administrador inicial' }
$clave = Read-Host 'Clave temporal del admin (12-128 caracteres, mayuscula, minuscula, numero y simbolo)' -AsSecureString
$anteriores = @{}
foreach ($nombre in @('Bootstrap__Usuario','Bootstrap__Clave','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT')) {
    $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre,'Process')
}
try {
    $env:Bootstrap__Usuario = $Usuario
    $env:Bootstrap__Clave = [Net.NetworkCredential]::new('', $clave).Password
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:DOTNET_ENVIRONMENT = 'Production'
    Push-Location $ruta
    try {
        & dotnet (Join-Path $ruta 'Api.dll') --crear-admin
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el administrador. Revisa el error anterior.' }
    }
    finally { Pop-Location }
}
finally {
    foreach ($nombre in $anteriores.Keys) { [Environment]::SetEnvironmentVariable($nombre,$anteriores[$nombre],'Process') }
    $clave.Dispose()
}
