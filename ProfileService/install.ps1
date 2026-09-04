# Ensure running as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Please run this script as an Administrator."
    exit 1
}

$destDir = "C:\ProgramData\Microsoft\ProfileService"
$exeName = "ProfileService.exe"
$sourcePath = Join-Path $PSScriptRoot $exeName
$destPath = Join-Path $destDir $exeName
$serviceName = "WinProfService"

$laikoDestDir = "C:\ProgramData\LaikoLimitas"
$laikoExeName = "LaikoLimitas.exe"
$laikoSourcePath = Join-Path $PSScriptRoot $laikoExeName
$laikoDestPath = Join-Path $laikoDestDir $laikoExeName

Write-Host "[1/6] Checking installation directories..." -ForegroundColor Cyan
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force -ErrorAction Stop | Out-Null
}
if (-not (Test-Path $laikoDestDir)) {
    New-Item -ItemType Directory -Path $laikoDestDir -Force -ErrorAction Stop | Out-Null
}

Write-Host "[2/6] Stopping existing service if running..." -ForegroundColor Cyan
Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $laikoDestPath } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "[3/6] Copying application files..." -ForegroundColor Cyan
if (Test-Path $sourcePath) {
    Copy-Item -Path $sourcePath -Destination $destPath -Force -ErrorAction Stop
} else {
    throw "Source file '$sourcePath' not found next to script."
}

if (Test-Path $laikoSourcePath) {
    Copy-Item -Path $laikoSourcePath -Destination $laikoDestPath -Force -ErrorAction Stop
} else {
    throw "Source file '$laikoSourcePath' not found next to script."
}

Write-Host "[4/6] Registering or updating Windows Service..." -ForegroundColor Cyan
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    sc.exe config $serviceName binPath= "`"$destPath`"" start= disabled displayname= "Windows Profile Service" *> $null
} else {
    sc.exe create $serviceName binPath= "`"$destPath`"" start= disabled displayname= "Windows Profile Service" *> $null
}
if ($LASTEXITCODE -ne 0) { throw "Failed to register Windows service." }

$RegistryPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$ApprovedPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
$ValueName = "LaikoLimitasAgent"
Set-ItemProperty -Path $RegistryPath -Name $ValueName -Value $laikoDestPath -Force | Out-Null
if (Test-Path "$ApprovedPath") {
    $enabledBinary = [byte[]](0x02, 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
    if (Get-ItemProperty -Path $ApprovedPath -Name $ValueName -ErrorAction SilentlyContinue) {
        Set-ItemProperty -Path $ApprovedPath -Name $ValueName -Value $enabledBinary -Force | Out-Null
    }
}
sc.exe description $serviceName "Monitors active user session profile quality service." *> $null

Write-Host "[5/6] Service installed/updated successfully." -ForegroundColor Green

$response = Read-Host "[6/6] Do you want to Enable and Start the service now? (y/N)"
if ($response -eq 'y' -or $response -eq 'yes') {
    Write-Host "Enabling and starting service..." -ForegroundColor Cyan
    sc.exe config $serviceName start= auto *> $null
    sc.exe start $serviceName *> $null
    if ($LASTEXITCODE -ne 0) { throw "Failed to start Windows service." }
    Write-Host "Service enabled and started successfully." -ForegroundColor Green
} else {
    Write-Host "Service left in Disabled state." -ForegroundColor Yellow
}