param([string]$Usuario)
$ErrorActionPreference = 'Stop'
$raizProyecto = Split-Path -Parent $PSScriptRoot
if (-not $Usuario) { $Usuario = Read-Host 'Nombre del administrador inicial' }
$claveSegura = Read-Host 'Clave temporal (12–128 caracteres, mayúscula, minúscula, número y símbolo)' -AsSecureString
$bootstrapUsuarioAnterior = $env:Bootstrap__Usuario
$bootstrapClaveAnterior = $env:Bootstrap__Clave
try {
    $env:Bootstrap__Usuario = $Usuario
    $env:Bootstrap__Clave = [Net.NetworkCredential]::new('', $claveSegura).Password
    Push-Location $raizProyecto
    try {
        & dotnet run --project Api --launch-profile http -- --crear-admin
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el administrador inicial.' }
    }
    finally { Pop-Location }
}
finally {
    $env:Bootstrap__Usuario = $bootstrapUsuarioAnterior
    $env:Bootstrap__Clave = $bootstrapClaveAnterior
    $claveSegura.Dispose()
}
