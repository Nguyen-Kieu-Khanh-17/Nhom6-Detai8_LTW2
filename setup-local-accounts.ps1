Set-Location $PSScriptRoot

$adminPassword = Read-Host "Nhap mat khau cho tai khoan admin" -AsSecureString
$doctorPassword = Read-Host "Nhap mat khau cho tai khoan doctor" -AsSecureString
$receptionistPassword = Read-Host "Nhap mat khau cho tai khoan letu" -AsSecureString
$supportPassword = Read-Host "Nhap mat khau cho tai khoan support" -AsSecureString

function Set-LocalSecret($key, $securePassword) {
	$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
		[Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))
	dotnet user-secrets set $key $plainPassword | Out-Null
}

Set-LocalSecret "AdminPassword" $adminPassword
Set-LocalSecret "DoctorPassword" $doctorPassword
Set-LocalSecret "ReceptionistPassword" $receptionistPassword
Set-LocalSecret "SupportPassword" $supportPassword

Write-Host "Da cau hinh tai khoan local trong .NET User Secrets."
Write-Host "Username admin: admin"
Write-Host "Username doctor: doctor"