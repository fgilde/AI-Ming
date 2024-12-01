# Clear the screen and output headers
Clear-Host
Write-Host "CUDA Environment Check and Repair Script" -ForegroundColor Yellow

# Function to check if a path exists and optionally copy missing files
function Test-And-Repair-Path {
    param (
        [string]$path,
        [string]$description,
        [string]$sourcePath = "",
        [switch]$checkAndAddPath
    )
    if (Test-Path $path) {
        Write-Host "[OK] $description found at: $path" -ForegroundColor Green
        # If the file exists but PATH is not set, add it to the system PATH
        if ($checkAndAddPath.IsPresent -and ($env:PATH -notlike "*$path*")) {
            [System.Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";$path", [System.EnvironmentVariableTarget]::Machine)
            Write-Host "[ACTION] Added $description to the system PATH." -ForegroundColor Yellow
        }
    } else {
        Write-Host "[ERROR] $description is missing at: $path" -ForegroundColor Red
        if ($sourcePath -ne "" -and (Test-Path $sourcePath)) {
            Write-Host "Attempting to copy missing $description from $sourcePath to $path..." -ForegroundColor Yellow
            try {
                Copy-Item $sourcePath $path
                Write-Host "[OK] $description successfully copied to $path." -ForegroundColor Green
            } catch {
                Write-Host "[ERROR] Failed to copy $description from $sourcePath. Please copy it manually or reinstall CUDA/cuDNN." -ForegroundColor Red
            }
        } else {
            Write-Host "[ACTION NEEDED] Please manually copy or reinstall $description." -ForegroundColor Yellow
        }
    }
}

# Step 1: Check for CUDA_PATH environment variable
Write-Host "`nStep 1: Checking CUDA_PATH..." -ForegroundColor Yellow
if ($env:CUDA_PATH) {
    Write-Host "[OK] CUDA_PATH is set to: $env:CUDA_PATH" -ForegroundColor Green
} else {
    Write-Host "[ERROR] CUDA_PATH is not set!" -ForegroundColor Red
    Write-Host "Please reinstall CUDA or set the CUDA_PATH variable manually in system settings." -ForegroundColor Yellow
}

# Step 2: Check if necessary CUDA binaries are in the PATH
Write-Host "`nStep 2: Checking if CUDA binaries are in the system PATH..." -ForegroundColor Yellow
$cudaBinPath = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6\bin"
if ($env:PATH -contains $cudaBinPath) {
    Write-Host "[OK] CUDA bin directory is present in PATH." -ForegroundColor Green
} else {
    Write-Host "[ACTION] CUDA bin directory is NOT in the system PATH. Adding it to PATH..." -ForegroundColor Yellow
    [System.Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";$cudaBinPath", [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] Added CUDA bin directory to system PATH." -ForegroundColor Green
}

# Step 3: Check for specific CUDA DLLs and attempt to copy if missing
Write-Host "`nStep 3: Checking for essential CUDA DLLs..." -ForegroundColor Yellow
$cudaSourceDir = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6\bin"
Test-And-Repair-Path "$cudaBinPath\cudart64_120.dll" "cudart64_120.dll" "$cudaSourceDir\cudart64_120.dll"
Test-And-Repair-Path "$cudaBinPath\nvcc.exe" "nvcc.exe"
Test-And-Repair-Path "$cudaBinPath\cublas64_12.dll" "cublas64_12.dll" "$cudaSourceDir\cublas64_12.dll"

# Step 4: Check for cuDNN DLLs and attempt to copy if missing
Write-Host "`nStep 4: Checking for cuDNN DLLs..." -ForegroundColor Yellow
$cuDnnSourceDir = "C:\tools\cuda\bin" # Adjust this path based on where cuDNN is installed
Test-And-Repair-Path "$cudaBinPath\cudnn64_8.dll" "cuDNN cudnn64_8.dll" "$cuDnnSourceDir\cudnn64_8.dll"

# Step 5: Check GPU information with nvidia-smi
Write-Host "`nStep 5: Checking GPU information using nvidia-smi..." -ForegroundColor Yellow
$nvidiaSmiPath = "C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"
if (Test-Path $nvidiaSmiPath) {
    & $nvidiaSmiPath
    Write-Host "[OK] NVIDIA driver and GPU information retrieved successfully." -ForegroundColor Green
} else {
    Write-Host "[ERROR] nvidia-smi.exe not found! Please install or update your NVIDIA drivers from the official site." -ForegroundColor Red
    Write-Host "Download the latest drivers from: https://www.nvidia.com/Download/index.aspx" -ForegroundColor Yellow
}

Write-Host "`nCUDA environment check and repair process completed!" -ForegroundColor Yellow
