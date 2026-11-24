param([switch]$IniciarApi)
$ErrorActionPreference = 'Stop'
$raizProyecto = Split-Path -Parent $PSScriptRoot
$variables = @('ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT')
$anteriores = @{}
foreach ($nombre in $variables) { $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process') }
$claveSegura = Read-Host 'Contrasena de PostgreSQL para postgres (localhost:5432 / delivery)' -AsSecureString
try {
    $conexion = New-Object System.Data.Common.DbConnectionStringBuilder
    $conexion['Host'] = 'localhost'
    $conexion['Port'] = '5432'
    $conexion['Database'] = 'delivery'
    $conexion['Username'] = 'postgres'
    $conexion['Password'] = [Net.NetworkCredential]::new('', $claveSegura).Password
    $env:ConnectionStrings__DefaultConnection = $conexion.ConnectionString
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Push-Location $raizProyecto
    try {
        & dotnet run --project Api/Api.csproj --no-launch-profile -- --preparar-local
        if ($LASTEXITCODE -ne 0) { throw 'No se completo la preparacion. Revisa el error anterior (conexion o PostGIS).' }
        if ($IniciarApi) {
            & dotnet run --project Api/Api.csproj --no-launch-profile -- --urls http://localhost:5289
            if ($LASTEXITCODE -ne 0) { throw 'La API termino con un error.' }
        }
    }
    finally { Pop-Location }
}
finally {
    foreach ($nombre in $variables) { [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre], 'Process') }
    if ($conexion) { $conexion.Clear() }
    $claveSegura.Dispose()
}
