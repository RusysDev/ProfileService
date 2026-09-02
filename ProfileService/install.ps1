# Ensure running as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Please run this script as an Administrator."
    exit 1
}

$destDir = "C:\ProgramData\Microsoft\ProfileService"
$exeName = "ProfileService.exe"
$sourcePath = Join-Path $PSScriptRoot $exeName
$destPath = Join-Path $destDir $exeName

# Create directory if it does not exist
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

# Copy executable
if (Test-Path $sourcePath) {
    Copy-Item -Path $sourcePath -Destination $destPath -Force
} else {
    Write-Error "Source file '$sourcePath' not found next to script."
    exit 1
}

# Install and start the service
sc.exe create WinProfService binPath= "`"$destPath`"" start= auto displayname= "Windows Profile Service"
sc.exe description WinProfService "Monitors active user session profile quality service."
sc.exe start WinProfService