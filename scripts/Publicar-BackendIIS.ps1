# Ejecutar con Windows PowerShell como administrador. Publicacion inicial local.
$ErrorActionPreference = 'Stop'
$raizProyecto = Split-Path -Parent $PSScriptRoot
$identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identidad)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Abre CMD como administrador y vuelve a ejecutar este script.'
}
if (-not (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) {
    throw 'Instala ASP.NET Core Hosting Bundle .NET 10 y reinicia Windows antes de continuar.'
}
Import-Module WebAdministration
$sitio = 'delivery-api'
$pool = 'delivery-api-pool'
if (Test-Path "IIS:\Sites\$sitio") { throw 'El sitio delivery-api ya existe. No se sobrescribio; revisa su configuracion.' }
if (Get-NetTCPConnection -LocalPort 5289 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Puerto 5289 ocupado. Deten la API de consola con Ctrl+C y vuelve a ejecutar.'
}
$features = @('IIS-WebSockets','IIS-StaticContent','IIS-DefaultDocument','IIS-ManagementConsole')
foreach ($feature in $features) {
    if ((Get-WindowsOptionalFeature -Online -FeatureName $feature).State -ne 'Enabled') {
        $resultado = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
        if ($resultado.RestartNeeded) { throw 'Windows requiere reiniciar. Reinicia y vuelve a ejecutar.' }
    }
}
$destino = Join-Path $env:SystemDrive ('inetpub\delivery-api-' + [Guid]::NewGuid().ToString('N'))
$clave = Read-Host 'Contrasena de PostgreSQL (postgres / localhost:5432 / delivery)' -AsSecureString
try {
    Push-Location $raizProyecto
    try {
        & dotnet publish Api/Api.csproj -c Release -o $destino -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion del backend.' }
    }
    finally { Pop-Location }

    if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.identityType -Value ApplicationPoolIdentity

    # Configuracion privada: solo administradores, SYSTEM y el pool del backend.
    $acl = Get-Acl -LiteralPath $destino
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
        $regla = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($regla)
    }
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new("IIS AppPool\$pool", 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $destino -AclObject $acl

    $conexion = New-Object System.Data.Common.DbConnectionStringBuilder
    $conexion['Host']='localhost'; $conexion['Port']='5432'; $conexion['Database']='delivery'
    $conexion['Username']='postgres'; $conexion['Password']=[Net.NetworkCredential]::new('', $clave).Password
    $config = Get-Content (Join-Path $destino 'appsettings.json') -Raw | ConvertFrom-Json
    $config.ConnectionStrings.DefaultConnection = $conexion.ConnectionString
    $bytes = New-Object byte[] 64
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    if ($null -eq $config.Jwt) {
        $config | Add-Member -NotePropertyName Jwt -NotePropertyValue ([pscustomobject]@{}) -Force
    }
    $config.Jwt | Add-Member -NotePropertyName Key -NotePropertyValue ([Convert]::ToBase64String($bytes)) -Force
    if ([string]::IsNullOrWhiteSpace($config.Jwt.Issuer)) {
        $config.Jwt | Add-Member -NotePropertyName Issuer -NotePropertyValue 'Delivery.Api' -Force
    }
    if ([string]::IsNullOrWhiteSpace($config.Jwt.Audience)) {
        $config.Jwt | Add-Member -NotePropertyName Audience -NotePropertyValue 'Delivery.Clientes' -Force
    }
    $config | Add-Member -NotePropertyName Cors -NotePropertyValue @{
        Origenes=@('http://localhost:5290','http://localhost:5330','http://localhost:4200','http://localhost:4300')
        PermitirCualquierOrigenEnDesarrollo=$false
    } -Force
    $config | Add-Member -NotePropertyName OpenApi -NotePropertyValue @{Enabled=$true} -Force
    $json = $config | ConvertTo-Json -Depth 30
    # Sustituye tambien la configuracion base publicada para que no conserve Supabase.
    $json | Set-Content (Join-Path $destino 'appsettings.json') -Encoding UTF8
    $json | Set-Content (Join-Path $destino 'appsettings.Production.json') -Encoding UTF8
    [xml]$web = Get-Content (Join-Path $destino 'web.config')
    $asp = $web.SelectSingleNode('//aspNetCore')
    $envs = $asp.SelectSingleNode('environmentVariables')
    if ($null -eq $envs) { $envs=$web.CreateElement('environmentVariables'); [void]$asp.AppendChild($envs) }
    foreach ($name in @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT')) {
        $entry=$envs.SelectSingleNode("environmentVariable[@name='$name']")
        if ($null -eq $entry) { $entry=$web.CreateElement('environmentVariable'); [void]$envs.AppendChild($entry) }
        $entry.SetAttribute('name',$name); $entry.SetAttribute('value','Production')
    }
    $web.Save((Join-Path $destino 'web.config'))
    New-Website -Name $sitio -Port 5289 -IPAddress '*' -HostHeader 'localhost' -PhysicalPath $destino -ApplicationPool $pool | Out-Null
    Start-Website -Name $sitio
    $respuesta=Invoke-WebRequest 'http://localhost:5289/health' -UseBasicParsing -TimeoutSec 60
    Write-Host "IIS: HTTP $($respuesta.StatusCode). Backend publicado en http://localhost:5289"
    Write-Host 'Conexion permanente: PostgreSQL local / delivery. No depende de mantener esta consola abierta.'
    Write-Host 'La comprobacion /health valida el servidor. Verifica tambien login/catalogo para comprobar acceso a la base.'
}
finally {
    $clave.Dispose()
    if ($conexion) { $conexion.Clear() }
    $json=$null; $config=$null
}
