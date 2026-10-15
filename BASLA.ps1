$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
Write-Host "FiloKontrol - ilk kurulum" -ForegroundColor Green
dotnet --list-sdks
if ($LASTEXITCODE -ne 0) { throw ".NET 9 SDK bulunamadi." }
$demoPassword = Read-Host "Demo hesaplar icin sifre (en az 10 karakter, buyuk/kucuk harf, rakam, sembol)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($demoPassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    dotnet user-secrets set "Demo:Password" "$plainPassword" --project src/TrafficFineManager.Web
    if ($LASTEXITCODE -ne 0) { throw "Demo sifresi ayarlanamadi." }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $plainPassword = $null
}
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Paketler yuklenemedi." }
dotnet build --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz." }
dotnet test --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Testler basarisiz." }
Write-Host "Tarayicida https://localhost:7180 adresini acin." -ForegroundColor Green
dotnet run --project src/TrafficFineManager.Web
