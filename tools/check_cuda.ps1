# ============================================================
#  check_cuda.ps1  —  CUDA / cuDNN environment check
#  Reports what's missing and where to get it.
# ============================================================

[CmdletBinding()]
param(
    # If set, opens each download page in your default browser
    # when the corresponding component is missing.
    [switch]$OpenLinks
)

$ErrorActionPreference = 'Continue'

# --- URLs ---------------------------------------------------
$UrlDriver  = 'https://www.nvidia.com/Download/index.aspx'
$UrlCuda    = 'https://developer.nvidia.com/cuda-downloads'
$UrlCuDnn   = 'https://developer.nvidia.com/cudnn'
$UrlPyTorch = 'https://pytorch.org/get-started/locally/'

# --- Target versions (edit if your project needs different) -
$CudaVersion = 'v12.6'
$CudaRoot    = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\$CudaVersion"
$CudaBin     = Join-Path $CudaRoot 'bin'

# --- Helpers ------------------------------------------------
function Write-Head($text) {
    Write-Host ""
    Write-Host $text -ForegroundColor Yellow
}

function Write-Ok($text)   { Write-Host "[OK]     $text" -ForegroundColor Green }
function Write-Warn2($text){ Write-Host "[WARN]   $text" -ForegroundColor Yellow }
function Write-Err($text)  { Write-Host "[MISSING] $text" -ForegroundColor Red }

function Show-Download($label, $url) {
    Write-Host "         Download $label -> $url" -ForegroundColor Cyan
    if ($OpenLinks) {
        try { Start-Process $url } catch {
            Write-Warn2 "Could not open browser for $url"
        }
    }
}

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
}

# Tracks whether anything was missing overall
$script:Missing = $false

Clear-Host
Write-Host "CUDA Environment Check" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Yellow

if (-not (Test-IsAdmin)) {
    Write-Warn2 "Not running as Administrator. PATH edits will be skipped."
    Write-Host "         Re-run in an elevated PowerShell if you need to modify PATH." -ForegroundColor DarkGray
}

# ------------------------------------------------------------
Write-Head "Step 1: NVIDIA driver (nvidia-smi)"

$smi = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
if ($smi) {
    Write-Ok "nvidia-smi found at: $($smi.Source)"
    try {
        $smiOut = & $smi.Source --query-gpu=name,driver_version --format=csv,noheader 2>$null
        Write-Host "         $smiOut" -ForegroundColor DarkGray
    } catch {
        Write-Warn2 "nvidia-smi ran but did not return GPU info."
    }
} else {
    $script:Missing = $true
    Write-Err "nvidia-smi.exe not found on PATH."
    Write-Host "         Install or update the NVIDIA driver for your GPU." -ForegroundColor Gray
    Show-Download "NVIDIA driver" $UrlDriver
}

# ------------------------------------------------------------
Write-Head "Step 2: CUDA_PATH environment variable"

if ($env:CUDA_PATH) {
    Write-Ok "CUDA_PATH = $env:CUDA_PATH"
} else {
    $script:Missing = $true
    Write-Err "CUDA_PATH is not set."
    Write-Host "         The CUDA Toolkit installer normally sets this." -ForegroundColor Gray
    Write-Host "         If you have CUDA installed elsewhere, set it manually." -ForegroundColor Gray
    Show-Download "CUDA Toolkit" $UrlCuda
}

# ------------------------------------------------------------
Write-Head "Step 3: CUDA Toolkit directory ($CudaVersion)"

if (Test-Path $CudaBin) {
    Write-Ok "CUDA bin directory exists: $CudaBin"
} else {
    $script:Missing = $true
    Write-Err "CUDA bin directory not found: $CudaBin"
    Write-Host "         Either install CUDA $CudaVersion, or edit `$CudaVersion" -ForegroundColor Gray
    Write-Host "         at the top of this script to match what you have." -ForegroundColor Gray
    Show-Download "CUDA Toolkit" $UrlCuda
}

# ------------------------------------------------------------
Write-Head "Step 4: CUDA bin directory on system PATH"

if (Test-Path $CudaBin) {
    # -contains works on arrays; $env:PATH is one string, so split first.
    $pathEntries = $env:PATH -split ';' | Where-Object { $_ }
    if ($pathEntries -contains $CudaBin) {
        Write-Ok "CUDA bin is on PATH."
    } else {
        Write-Warn2 "CUDA bin is NOT on PATH: $CudaBin"
        if (Test-IsAdmin) {
            try {
                $sysPath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
                $sysEntries = $sysPath -split ';' | Where-Object { $_ }
                if ($sysEntries -notcontains $CudaBin) {
                    [Environment]::SetEnvironmentVariable(
                        'Path', "$sysPath;$CudaBin", 'Machine'
                    )
                    Write-Ok "Added to Machine PATH. Open a NEW terminal to pick it up."
                } else {
                    Write-Ok "Already present in Machine PATH (current shell is stale)."
                }
            } catch {
                Write-Err "Could not write Machine PATH: $($_.Exception.Message)"
            }
        } else {
            Write-Host "         Re-run as Administrator to add it automatically," -ForegroundColor Gray
            Write-Host "         or add it via System Properties -> Environment Variables." -ForegroundColor Gray
        }
    }
} else {
    Write-Host "         Skipped (CUDA bin directory does not exist)." -ForegroundColor DarkGray
}

# ------------------------------------------------------------
Write-Head "Step 5: Core CUDA files"

if (Test-Path $CudaBin) {
    $coreFiles = @(
        @{ Name = 'nvcc.exe';          Desc = 'CUDA compiler' },
        @{ Name = 'cudart64_12.dll';   Desc = 'CUDA runtime' },
        @{ Name = 'cublas64_12.dll';   Desc = 'cuBLAS' }
    )

    $coreMissing = @()
    foreach ($f in $coreFiles) {
        $full = Join-Path $CudaBin $f.Name
        if (Test-Path $full) {
            Write-Ok "$($f.Name)  ($($f.Desc))"
        } else {
            Write-Err "$($f.Name)  ($($f.Desc))"
            $coreMissing += $f.Name
        }
    }

    if ($coreMissing.Count -gt 0) {
        $script:Missing = $true
        Write-Host "         These ship with the CUDA Toolkit. Reinstall or repair it." -ForegroundColor Gray
        Show-Download "CUDA Toolkit" $UrlCuda
    }
} else {
    Write-Host "         Skipped (no CUDA bin directory)." -ForegroundColor DarkGray
}

# ------------------------------------------------------------
Write-Head "Step 6: cuDNN"

# cuDNN version numbering: 8.x -> cudnn64_8.dll, 9.x -> cudnn64_9.dll, etc.
$cudnnCandidates = @('cudnn64_9.dll', 'cudnn64_8.dll')
$cudnnFound = $null

# Search the CUDA bin dir first, then common install locations.
$searchDirs = @()
if (Test-Path $CudaBin) { $searchDirs += $CudaBin }
$searchDirs += @(
    "C:\tools\cuda\bin",
    "C:\Program Files\NVIDIA\CUDNN\bin"
)

foreach ($dir in $searchDirs) {
    if (-not (Test-Path $dir)) { continue }
    foreach ($cand in $cudnnCandidates) {
        $full = Join-Path $dir $cand
        if (Test-Path $full) {
            $cudnnFound = $full
            break
        }
    }
    if ($cudnnFound) { break }
}

if ($cudnnFound) {
    Write-Ok "cuDNN found: $cudnnFound"
} else {
    Write-Warn2 "cuDNN not found (may be fine if your project bundles it)."
    Write-Host "         PyTorch and TensorFlow usually ship their own cuDNN." -ForegroundColor Gray
    Write-Host "         Only install it if something specifically asks for it." -ForegroundColor Gray
    Show-Download "cuDNN" $UrlCuDnn
}

# ------------------------------------------------------------
Write-Head "Summary"

if ($script:Missing) {
    Write-Host "One or more components are missing. See the links above." -ForegroundColor Yellow
} else {
    Write-Host "All checked components are present." -ForegroundColor Green
}

Write-Host ""
Write-Host "Tip: if you use PyTorch, install the CUDA build matching your toolkit:" -ForegroundColor DarkGray
Write-Host "     $UrlPyTorch" -ForegroundColor DarkGray
Write-Host ""