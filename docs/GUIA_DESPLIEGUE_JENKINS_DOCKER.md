# Preparación de despliegue local y CI/CD

## Alcance implementado en este repositorio

- `Dockerfile`: imagen multi-stage de la API .NET.
- `compose.yaml`: PostgreSQL/PostGIS y API para el entorno local, con volumen persistente y reinicio automático de contenedores.
- `Jenkinsfile`: restore, build, pruebas, creación de imagen, despliegue local y etapa opcional de publicación en Render/Vercel.
- `render.yaml`: orígenes CORS de los frontends Vercel y de desarrollo.
- `Api/appsettings.json`: sin credenciales de base de datos; se configuran por variables de entorno.

El repositorio Angular y el de Flutter no están disponibles dentro de este workspace. Por ello, esta primera composición levanta **API + base PostgreSQL local**, no el frontend web. Flutter es un cliente que se instala/ejecuta en un dispositivo y debe apuntar a la IP del equipo que ejecuta la API. Para integrar el frontend en Compose/Jenkins se debe agregar su repositorio como checkout/submódulo y conocer su comando de build y variables de API.

## 1. Requisitos del nodo local/Jenkins

- Windows 10/11 con virtualización habilitada.
- Docker Desktop usando contenedores Linux y Docker Compose v2.
- .NET SDK 10 y herramienta `dotnet-ef` 10.0.12 disponible en `PATH`.
- Jenkins con plugins Pipeline, Git y Credentials Binding; el agente debe poder ejecutar `dotnet`, `docker`, `docker compose` y PowerShell.
- Acceso de red para restaurar paquetes NuGet y obtener las imágenes base.

Instalar la herramienta EF en la cuenta que ejecuta Jenkins (una vez):

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
```

## 2. Arranque local de API y base de datos

Desde la raíz del repositorio:

```powershell
Copy-Item .env.example .env
```

Editar `.env` y reemplazar `POSTGRES_PASSWORD` y `JWT_KEY` por valores aleatorios. No subir `.env` al repositorio. Iniciar PostgreSQL y esperar que esté saludable:

```powershell
docker compose -f compose.yaml up -d postgres
docker compose -f compose.yaml ps
```

Aplicar las migraciones a la base local. La cadena debe usar el puerto publicado por Compose (`54329`):

```powershell
$env:ConnectionStrings__DefaultConnection = Read-Host "Cadena local: Host=localhost;Port=54329;Database=delivery;Username=delivery;Password=..."
$env:Jwt__Key = Read-Host "JWT local (mínimo 32 bytes)"
$env:Jwt__Issuer = "Delivery.Api"
$env:Jwt__Audience = "Delivery.Clientes"
dotnet ef database update --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj
```

Luego construir y levantar la API:

```powershell
docker build -t delivery-api:local .
docker compose -f compose.yaml up -d api
```

Comprobaciones:

```powershell
Invoke-WebRequest http://localhost:5289/health
```

Swagger queda en `http://localhost:5289/swagger`. El catálogo de desarrollo se siembra al iniciar la API si la base no tiene categorías; el seeder actual no demuestra el requisito de 200 productos, que queda pendiente de preparar y verificar.

Para crear el primer administrador local, guardar temporalmente `BOOTSTRAP_USER` y `BOOTSTRAP_PASSWORD` en el `.env` local y ejecutar una sola vez:

```powershell
docker compose -f compose.yaml run --rm api --crear-admin
```

La operación falla si ya existe un administrador. No usar ni copiar estas credenciales locales a Render.

Para una aplicación Flutter en un teléfono físico, configurar la URL base con la IP LAN del computador, por ejemplo `http://192.168.1.25:5289/api`; `localhost` en el teléfono apunta al propio teléfono. Ambos dispositivos deben estar en una red que permita ese tráfico.

## 3. Recuperación después de reiniciar el computador

Los servicios usan `restart: unless-stopped` y PostgreSQL guarda sus datos en el volumen `delivery-postgres-data`. Configurar Docker Desktop para iniciar al iniciar sesión en Windows. Después de reiniciar y abrir sesión, validar:

```powershell
docker compose -f compose.yaml ps
Invoke-WebRequest http://localhost:5289/health
```

Esto recupera los contenedores cuando el computador vuelve a estar encendido; la aplicación local no está disponible mientras el equipo está apagado. Para disponibilidad en ese intervalo, utilizar el despliegue cloud.

## 4. Configurar Jenkins

Crear un Pipeline desde SCM apuntando al repositorio `delivery-backend`, con script `Jenkinsfile`. Para CI automática, activar el webhook de GitHub (o el mecanismo de polling acordado) para que cada cambio inicie una ejecución.

Agregar estas credenciales tipo **Secret text** en Jenkins:

| ID de credencial | Contenido |
|---|---|
| `delivery-local-postgres-password` | Contraseña exclusiva de PostgreSQL local |
| `delivery-ci-jwt-key` | Clave aleatoria de al menos 32 bytes para el arranque de diseño de EF |
| `delivery-supabase-connection` | Cadena de conexión de la base cloud, sin envoltura JSON |
| `delivery-render-deploy-hook` | URL privada del Deploy Hook del servicio Render |
| `delivery-vercel-deploy-hook` | URL privada del Deploy Hook del proyecto Vercel del E-Commerce |

No imprimir estas credenciales en la consola del pipeline ni guardarlas en Git. La etapa `DEPLOY_CLOUD` inicia desactivada hasta que el equipo cree los hooks y registre todas las credenciales. Al habilitarla en `main`, Jenkins aplica migraciones a Supabase y solicita el despliegue de Render y Vercel. Confirmar antes que los hooks apunten a los proyectos y ramas correctos.

La etapa local usa un PostgreSQL en Docker, migra el esquema, levanta la API y exige un `200` de `/health`. El despliegue cloud solo se ejecuta en `main` y si se activa `DEPLOY_CLOUD`.

## 5. Entorno cloud actual

- Frontend administración conocido: `https://delivery-frontend-web.vercel.app`.
- Frontend E-Commerce conocido: `https://ecommerce-f2vm.vercel.app`.
- API: Render, imagen Docker desde este repositorio.
- Datos: PostgreSQL/Supabase configurada en las variables privadas de Render/Jenkins.
- Aplicación móvil: cliente Flutter; necesita poder acceder públicamente a la API y a SignalR.

`render.yaml` declara los dos dominios Vercel y los orígenes locales de desarrollo. Si esos dominios cambian, actualizar también las variables `Cors__Origenes__N` en el servicio Render y volver a desplegar.

Jenkins utiliza deploy hooks para solicitar que Render y Vercel construyan/desplieguen las revisiones que sus servicios tengan configuradas. Verificar que ambos proveedores estén conectados a la rama `main` (o ajustar la rama en el panel). El webhook de Jenkins debe ejecutarse después de que el commit esté disponible en remoto.

## 6. Pruebas y evidencias para el informe

Guardar capturas de:

1. Jenkins: ejecución con `Restore y compilación`, `Pruebas automatizadas`, `Construir imagen Docker`, `Desplegar local` y, al habilitarlo, `Desplegar nube`.
2. Docker Desktop/terminal mostrando ambos contenedores `healthy/running` y el volumen persistente.
3. Swagger y `/health` en local; frontend y API accesibles desde nube.
4. Una prueba de catálogo/checkout con datos de prueba y PayPal Sandbox.
5. Reinicio del computador y recuperación del contenedor.
6. Fallos encontrados y la solución aplicada, con sus logs sin secretos.

No documentar secretos, tokens JWT, credenciales de PayPal ni cadenas de base de datos en capturas. Usar cuentas Sandbox/datos sintéticos. El requisito de catálogo con 200 productos debe quedar respaldado por una verificación del número de productos y su procedencia antes de marcarse como cumplido.

## Pendientes fuera de este primer corte

- Incorporar el repositorio web del E-Commerce al Compose y al checkout de Jenkins; no está presente en este workspace.
- Confirmar el repositorio Flutter y documentar/build de la app móvil; la búsqueda local no encontró un `pubspec.yaml`.
- Cargar y verificar 200 productos reales con imágenes válidas y fuente/permiso de uso documentados.
- Ejecutar Docker/Jenkins en la máquina del equipo y tomar capturas reales. El entorno actual todavía no confirma que Docker Desktop esté instalado/activo ni tiene las credenciales Jenkins/Render/Vercel.
- Validar la prueba de reinicio y la disponibilidad desde Internet después de redeploy.
