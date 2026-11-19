pipeline {
    agent any

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 45, unit: 'MINUTES')
    }

    parameters {
        booleanParam(name: 'DEPLOY_LOCAL', defaultValue: true, description: 'Despliega API y PostgreSQL localmente con Docker Compose.')
        booleanParam(name: 'DEPLOY_CLOUD', defaultValue: false, description: 'Activa migraciones Supabase y deploy hooks de Render/Vercel después de configurar credenciales Jenkins.')
    }

    environment {
        LOCAL_API_URL = 'http://localhost:5289'
        LOCAL_IMAGE = "delivery-api:${BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps { checkout scm }
        }

        stage('Restore y compilación') {
            steps {
                bat 'dotnet restore delivery.slnx'
                bat 'dotnet build delivery.slnx --configuration Release --no-restore'
            }
        }

        stage('Pruebas automatizadas') {
            steps { bat 'dotnet test delivery.slnx --configuration Release --no-build --logger "console;verbosity=normal"' }
        }

        stage('Construir imagen Docker') {
            steps { bat 'docker build --tag %LOCAL_IMAGE% --tag delivery-api:local .' }
        }

        stage('Desplegar local') {
            when { expression { return params.DEPLOY_LOCAL } }
            steps {
                withCredentials([
                    string(credentialsId: 'delivery-local-postgres-password', variable: 'LOCAL_DB_PASSWORD'),
                    string(credentialsId: 'delivery-ci-jwt-key', variable: 'CI_JWT_KEY')
                ]) {
                    powershell '''
                        $dotenv = @(
                            'POSTGRES_DB=delivery',
                            'POSTGRES_USER=delivery',
                            "POSTGRES_PASSWORD=$env:LOCAL_DB_PASSWORD",
                            "JWT_KEY=$env:CI_JWT_KEY"
                        )
                        Set-Content -LiteralPath .env.jenkins -Value $dotenv -Encoding ascii
                        try {
                            docker compose --env-file .env.jenkins -f compose.yaml up -d postgres
                            if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar PostgreSQL.' }
                            $dbReady = $false
                            for ($i = 0; $i -lt 24; $i++) {
                                docker compose --env-file .env.jenkins -f compose.yaml exec -T postgres pg_isready -U delivery -d delivery *> $null
                                if ($LASTEXITCODE -eq 0) { $dbReady = $true; break }
                                Start-Sleep -Seconds 5
                            }
                            if (-not $dbReady) { throw 'PostgreSQL local no quedó listo.' }
                            $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=54329;Database=delivery;Username=delivery;Password=$env:LOCAL_DB_PASSWORD;Include Error Detail=true"
                            $env:Jwt__Key = $env:CI_JWT_KEY
                            $env:Jwt__Issuer = 'Delivery.Api'
                            $env:Jwt__Audience = 'Delivery.Clientes'
                            dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj
                            if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones locales.' }
                            docker compose --env-file .env.jenkins -f compose.yaml up -d api
                            if ($LASTEXITCODE -ne 0) { throw 'No se pudo desplegar la API local.' }
                            $ready = $false
                            for ($i = 0; $i -lt 24; $i++) {
                                try {
                                    $response = Invoke-WebRequest -Uri "$env:LOCAL_API_URL/health" -TimeoutSec 5
                                    if ($response.StatusCode -eq 200) { $ready = $true; break }
                                } catch { Start-Sleep -Seconds 5 }
                            }
                            if (-not $ready) { docker compose --env-file .env.jenkins -f compose.yaml logs api; throw 'La API local no respondió en /health.' }
                        } finally {
                            Remove-Item -LiteralPath .env.jenkins -Force -ErrorAction SilentlyContinue
                        }
                    '''
                }
            }
        }

        stage('Desplegar nube') {
            when { allOf { branch 'main'; expression { return params.DEPLOY_CLOUD } } }
            steps {
                withCredentials([
                    string(credentialsId: 'delivery-supabase-connection', variable: 'CLOUD_DB_CONNECTION'),
                    string(credentialsId: 'delivery-ci-jwt-key', variable: 'CI_JWT_KEY')
                ]) {
                    powershell '''
                        $env:ConnectionStrings__DefaultConnection = $env:CLOUD_DB_CONNECTION
                        $env:Jwt__Key = $env:CI_JWT_KEY
                        $env:Jwt__Issuer = 'Delivery.Api'
                        $env:Jwt__Audience = 'Delivery.Clientes'
                        dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj
                        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                    '''
                }
                withCredentials([
                    string(credentialsId: 'delivery-render-deploy-hook', variable: 'RENDER_HOOK'),
                    string(credentialsId: 'delivery-vercel-deploy-hook', variable: 'VERCEL_HOOK')
                ]) {
                    powershell '''
                        Invoke-WebRequest -Method Post -Uri $env:RENDER_HOOK -TimeoutSec 30 | Out-Null
                        Invoke-WebRequest -Method Post -Uri $env:VERCEL_HOOK -TimeoutSec 30 | Out-Null
                    '''
                }
            }
        }
    }

    post {
        success { echo 'CI finalizada y los despliegues seleccionados respondieron correctamente.' }
        failure { echo 'Pipeline fallido. Revisar la primera etapa con error antes de reintentar.' }
    }
}
