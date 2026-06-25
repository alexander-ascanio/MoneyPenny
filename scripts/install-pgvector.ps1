# Instala pgvector en PostgreSQL 18 (Windows).
# Ejecutar: clic derecho en PowerShell -> "Ejecutar como administrador"
#   cd c:\Proyectos\MoneyPenny\scripts
#   .\install-pgvector.ps1

$ErrorActionPreference = 'Stop'

$zipUrl = 'https://github.com/andreiramani/pgvector_pgsql_windows/releases/download/0.8.2_18.0.2/vector.v0.8.2-pg18.zip'
$workDir = Join-Path $env:TEMP 'pgvector-install'
$zipPath = Join-Path $workDir 'vector.zip'
$pgRoot = 'C:\Program Files\PostgreSQL\18'

Write-Host 'Descargando pgvector para PostgreSQL 18...'
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
Expand-Archive -Path $zipPath -DestinationPath $workDir -Force

Write-Host "Copiando archivos a $pgRoot ..."
Copy-Item "$workDir\lib\vector.dll" "$pgRoot\lib\vector.dll" -Force
Copy-Item "$workDir\share\extension\*" "$pgRoot\share\extension\" -Force

Write-Host 'Reiniciando PostgreSQL...'
Restart-Service postgresql-x64-18

Write-Host ''
Write-Host 'Listo. Ahora ejecuta (sin admin):'
Write-Host '  dotnet ef database update --context VectorDbContext --project c:\Proyectos\MoneyPenny\MoneyPenny.csproj'
Write-Host ''
Write-Host 'Opcional, activar extension en la BD:'
Write-Host "  & `"$pgRoot\bin\psql.exe`" -U postgres -d moneypenny_vectors_db -c `"CREATE EXTENSION IF NOT EXISTS vector;`""
