param(
    [string]$version,
    [string]$incVersion
)

# Call this script with "powershell -ExecutionPolicy Bypass -File .\build.ps1"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$outputDir = Join-Path $scriptDir "PowerAim/bin/Release"

# ---------------------------------------------------------------------------------------------
# Code signing
# ---------------------------------------------------------------------------------------------
# Unsigned binaries are the main reason releases get flagged: Defender's cloud ML and Chrome's
# Safe Browsing both score an unknown, unsigned .exe as "no reputation", and because every build
# ships under a freshly rotated AssemblyName (see Get-RandomAssemblyName below), that score never
# accumulates. A signature moves the reputation onto the CERTIFICATE, which does carry across
# builds and across renames — including the random rename the launcher does at startup.
#
# Configured entirely through environment variables, so no secret ever lives in this file:
#
#   SIGN_CERT_THUMBPRINT  Thumbprint of a certificate in CurrentUser\My or LocalMachine\My. The
#                         usual setup for an OV certificate on a hardware token.
#   SIGN_CERT_PFX         Path to a .pfx file, used with SIGN_CERT_PASSWORD. Alternative to the store.
#   SIGN_CERT_PASSWORD    Password for that .pfx.
#   SIGN_COMMAND          Escape hatch for cloud signing services (Azure Trusted Signing, DigiCert
#                         KeyLocker, SSL.com eSigner, or plain signtool.exe): a command line in which
#                         {file} is replaced by each file to sign. Takes precedence over the above.
#   SIGN_TIMESTAMP_URL    Timestamp server, default http://timestamp.digicert.com. Without a
#                         timestamp every signature expires with the certificate.
#   SIGN_REQUIRED         1/true/yes: a build that cannot sign FAILS instead of quietly shipping
#                         unsigned binaries. Set this in CI for releases.
#
# With nothing configured the build still runs and only warns — local development needs no cert.

function Get-SigningCertificate {
    if ($env:SIGN_CERT_THUMBPRINT) {
        $thumb = ($env:SIGN_CERT_THUMBPRINT -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
        foreach ($store in @('Cert:\CurrentUser\My', 'Cert:\LocalMachine\My')) {
            $found = @(Get-ChildItem $store -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $thumb })
            if ($found.Count -gt 0) { return $found[0] }
        }
        throw "SIGN_CERT_THUMBPRINT is set, but no certificate with thumbprint $thumb exists in CurrentUser\My or LocalMachine\My."
    }

    if ($env:SIGN_CERT_PFX) {
        if (-not (Test-Path $env:SIGN_CERT_PFX)) {
            throw "SIGN_CERT_PFX points to '$($env:SIGN_CERT_PFX)', which does not exist."
        }
        # An empty SecureString rather than $null: passing $null makes the X509Certificate2
        # constructor overload ambiguous on Windows PowerShell 5.1.
        $secure = if ($env:SIGN_CERT_PASSWORD) {
            ConvertTo-SecureString $env:SIGN_CERT_PASSWORD -AsPlainText -Force
        } else {
            New-Object System.Security.SecureString
        }
        return New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($env:SIGN_CERT_PFX, $secure)
    }

    return $null
}

function Invoke-CodeSigning {
    param([string]$publishDir)

    $required = $env:SIGN_REQUIRED -match '^(1|true|yes)$'
    $timestamp = if ($env:SIGN_TIMESTAMP_URL) { $env:SIGN_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }

    $cert = $null
    if (-not $env:SIGN_COMMAND) {
        $cert = Get-SigningCertificate
        if (-not $cert) {
            $msg = "No code signing certificate configured (SIGN_CERT_THUMBPRINT / SIGN_CERT_PFX / SIGN_COMMAND) — the output stays UNSIGNED."
            if ($required) { throw "$msg SIGN_REQUIRED is set, so this build fails." }
            Write-Warning $msg
            return
        }
    }

    # Only the executables this build produced, which are the ones at the top of $publishDir.
    # Everything under Resources\ is a third-party installer that ships with its own signature —
    # signing those with our certificate would mean vouching for someone else's binary. The
    # already-signed filter is a second safety net for the same reason.
    $targets = @(Get-ChildItem -Path $publishDir -Filter *.exe -File |
                 Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' })
    if ($targets.Count -eq 0) {
        Write-Host "Code signing: nothing to sign in $publishDir."
        return
    }

    Write-Host ""
    Write-Host "Signing $($targets.Count) executable(s) in $publishDir ..."
    $failures = @()
    foreach ($file in $targets) {
        try {
            if ($env:SIGN_COMMAND) {
                # Plain string replace, not -replace: a regex would mangle backslashes in the path.
                $command = $env:SIGN_COMMAND.Replace('{file}', $file.FullName)
                Invoke-Expression $command
                if ($LASTEXITCODE -ne 0) { throw "SIGN_COMMAND exited with code $LASTEXITCODE" }
            } else {
                $null = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert `
                    -HashAlgorithm SHA256 -TimestampServer $timestamp -ErrorAction Stop
            }

            # Verify rather than trust: a signature that does not validate is worth nothing, and a
            # silent failure here would ship exactly the unsigned binary this whole step exists to avoid.
            $sig = Get-AuthenticodeSignature $file.FullName
            if ($sig.Status -ne 'Valid') {
                throw "signature check says '$($sig.Status)' ($($sig.StatusMessage))"
            }
            Write-Host "  signed: $($file.Name)  [$($sig.SignerCertificate.Subject)]"
        } catch {
            $failures += "$($file.Name): $_"
            Write-Warning "  FAILED: $($file.Name): $_"
        }
    }

    if ($failures.Count -gt 0 -and $required) {
        throw "Code signing failed for $($failures.Count) file(s):`n" + ($failures -join "`n")
    }
}

# Funktion für das Erstellen mit oder ohne CUDA.
# Note: we publish each executable project EXPLICITLY (PowerAim + Launcher) instead of running
# `dotnet publish` from the solution root. The solution-wide publish picks up Core.csproj too,
# which is a library — `PublishSingleFile=true` on a library produces error NETSDK1099 and
# pollutes the build output. Targeting projects explicitly is the supported approach.
function Build-ProjectWithCuda {
    param(
        [bool]$isCuda,
        [string]$publishDir
    )

    $isCudaValue = if ($isCuda) { "true" } else { "false" }

    # Common publish args. Self-contained=false keeps the .exe small (relies on the user's installed .NET
    # runtime). Each variant publishes into its OWN folder ($publishDir via -o), so the CUDA build's
    # native onnxruntime.dll can NEVER leak into the DirectML build (issue #20 — a shared publish/ folder
    # made the "DirectML" release ship the CUDA onnxruntime.dll, which has no DirectML entry point, so it
    # silently ran on CPU). IsCuda is passed as an MSBuild property; the csproj derives the `IsCuda`
    # compile symbol from it, so no separate -p:DefineConstants override is needed (that one wiped TRACE).
    $commonArgs = @(
        '-c', 'Release',
        '-r', 'win-x64',
        '-p:PublishSingleFile=true',
        '--self-contained', 'false',
        '-p:DebugType=None',
        "-p:IsCuda=$isCudaValue",
        '-o', $publishDir
    )

    dotnet clean --configuration Release

    # Fresh, empty per-variant output folder every run.
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
        Write-Host "Cleared previous output at $publishDir"
    }

    Write-Host ""
    Write-Host "Publishing PowerAim (IsCuda=$isCudaValue) -> $publishDir ..."
    dotnet publish PowerAim/PowerAim.csproj @commonArgs
    if ($LASTEXITCODE -ne 0) { throw "PowerAim publish failed with exit code $LASTEXITCODE" }

    Write-Host ""
    Write-Host "Publishing Launcher (IsCuda=$isCudaValue) -> $publishDir ..."
    dotnet publish Launcher/Launcher.csproj @commonArgs
    if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed with exit code $LASTEXITCODE" }

    # Sign before anything is zipped or copied: Installer.exe is a byte copy of Launcher.exe, and
    # Authenticode covers the file content, not its name — so the copy inherits a valid signature.
    Invoke-CodeSigning -publishDir $publishDir
}

# Check if the output directory exists and delete it
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
    Write-Host "Output directory $outputDir deleted."
} else {
    Write-Host "Output directory $outputDir does not exist, no need to delete."
}

# Clean the project
dotnet clean --configuration Release

# Get the Assembly name and version from the .csproj file
$csprojPath = Join-Path $scriptDir "PowerAim/PowerAim.csproj"
[xml]$csproj = Get-Content $csprojPath

$assemblyName = [string]$csproj.Project.PropertyGroup.AssemblyName
$currentVersion  = [string]$csproj.Project.PropertyGroup.Version

# Function to increment version
function Increment-Version {
    param(
        [string]$version,
        [string]$component = "build"
    )
    
    $versionParts = $version.Split('.')
    
    switch ($component) {
        "major" {
            $versionParts[0] = [int]$versionParts[0] + 1
            $versionParts[1] = 0
            $versionParts[2] = 0
            $versionParts[3] = 0
        }
        "minor" {
            $versionParts[1] = [int]$versionParts[1] + 1
            $versionParts[2] = 0
            $versionParts[3] = 0
        }
        "patch" {
            $versionParts[2] = [int]$versionParts[2] + 1
            $versionParts[3] = 0
        }
        default {
            $versionParts[3] = [int]$versionParts[3] + 1
        }
    }
    
    return "$($versionParts[0]).$($versionParts[1]).$($versionParts[2]).$($versionParts[3])"
}

# Function to replace version in the .csproj file
function Replace-Version {
    param(
        [string]$oldVersion,
        [string]$newVersion,
        [string]$filePath
    )

    # Read the .csproj file as a string
    $csprojContent = Get-Content $filePath -Raw

    # Replace the old version with the new version
    $newCsprojContent = $csprojContent -replace "<Version>$oldVersion</Version>", "<Version>$newVersion</Version>"

    # Save the updated content back to the .csproj file without adding an extra newline
    $newCsprojContent | Out-File -FilePath $filePath -Encoding utf8 -NoNewline
}

# Update the version if needed
$versionUpdated = $false
$currentVersion = $currentVersion -replace '(^\s+|\s+$)','' -replace '\s+',' '
$assemblyName = $assemblyName -replace '(^\s+|\s+$)','' -replace '\s+',' '
$oldversion = $currentVersion
if ($version) {
    $currentVersion = $version
    $versionUpdated = $true
    Write-Host "Version updated to $version"
} elseif ($incVersion) {    
    Write-Host "Incrementing version by $incVersion"
    $newVersion = Increment-Version -version $currentVersion -component $incVersion
    $currentVersion = $newVersion
    $versionUpdated = $true
    Write-Host "Version incremented to $newVersion"
}

# Save the updated .csproj file if the version was changed
if ($versionUpdated) {
    Write-Host "old version: $oldversion |"
    Write-Host "Saving the updated .csproj file."
    Write-Host "Current version: $currentVersion"

    Replace-Version -oldVersion $oldversion -newVersion $currentVersion -filePath $csprojPath
    Write-Host "Updated .csproj file saved with version $currentVersion"
}

# ------------------------------------------------------------
# Rotate AssemblyName from the central Names array in Core/Constants.cs
# ------------------------------------------------------------
function Get-RandomAssemblyName {
    $constantsPath = Join-Path $scriptDir "Core/Constants.cs"
    if (-not (Test-Path $constantsPath)) {
        return "PowerAim"
    }
    $content = Get-Content $constantsPath -Raw
    $match = [regex]::Match($content, 'Names\s*=\s*new\[\]\s*\{([^}]+)\}|Names\s*=\s*\{([^}]+)\}', 'Singleline')
    if (-not $match.Success) {
        return "PowerAim"
    }
    $block = if ($match.Groups[1].Value) { $match.Groups[1].Value } else { $match.Groups[2].Value }
    $entries = [regex]::Matches($block, '"([^"]+)"')
    if ($entries.Count -eq 0) { return "PowerAim" }
    $pick = $entries[(Get-Random -Maximum $entries.Count)].Groups[1].Value
    # Strip spaces and quotes so the produced .exe filename stays clean
    return ($pick -replace '\s+', '' -replace "[''""`]", '')
}

function Replace-AssemblyName {
    param([string]$newName, [string]$filePath)
    $content = Get-Content $filePath -Raw
    $new = $content -replace '<AssemblyName>[^<]+</AssemblyName>', "<AssemblyName>$newName</AssemblyName>"
    $new | Out-File -FilePath $filePath -Encoding utf8 -NoNewline
}

$pickedAssemblyName = Get-RandomAssemblyName
Write-Host ""
Write-Host "Picked random AssemblyName for this build: $pickedAssemblyName"
Replace-AssemblyName -newName $pickedAssemblyName -filePath $csprojPath

# Each variant lands in its OWN dedicated folder under bin/Release so the two builds can never mix
# native DLLs. The release .zips are written next to them in $outputDir.
$cudaDir = Join-Path $outputDir "cuda"
$dmlDir  = Join-Path $outputDir "directml"

# ---- CUDA variant ----
$buildSucceeded = $true
try {
    Write-Host "Building the CUDA variant -> $cudaDir ..."
    Build-ProjectWithCuda -isCuda:$true -publishDir $cudaDir
} catch {
    $buildSucceeded = $false
    Write-Host "CUDA build failed: $_. Reverting .csproj file to the old version."
    Replace-Version -oldVersion $currentVersion -newVersion $oldversion -filePath $csprojPath
}

if ($buildSucceeded) {
    # ZIP the CUDA build (Release_<v>_cuda.zip) from the cuda\ folder.
    $zipFileName = ("Release_${currentVersion}_cuda.zip" -replace '(^\s+|\s+$)','' -replace '\s+',' ')
    Compress-Archive -Path "$cudaDir\*" -DestinationPath (Join-Path $outputDir $zipFileName) -Force
    Write-Host "CUDA build compressed into $zipFileName."

    # Rename and copy Launcher.exe to Installer.exe (from the CUDA folder).
    $launcherPath = Join-Path $cudaDir "Launcher.exe"
    $installerPath = Join-Path $outputDir "Installer.exe"
    if (Test-Path $launcherPath) {
        Copy-Item -Path $launcherPath -Destination $installerPath -Force
        Write-Host "Launcher.exe copied and renamed to Installer.exe in the Release folder."
    } else {
        Write-Host "Launcher.exe not found, skipping the copy operation."
    }

    # ---- DirectML variant ----
    try {
        Write-Host "Building the DirectML variant -> $dmlDir ..."
        Build-ProjectWithCuda -isCuda:$false -publishDir $dmlDir
    } catch {
        $buildSucceeded = $false
        Write-Host "DirectML build failed: $_"
    }
}

if ($buildSucceeded) {
    # ZIP the DirectML build (plain Release_<v>.zip — the "normal" download) from the directml\ folder.
    $zipFileName = ("Release_${currentVersion}.zip" -replace '(^\s+|\s+$)','' -replace '\s+',' ')
    Compress-Archive -Path "$dmlDir\*" -DestinationPath (Join-Path $outputDir $zipFileName) -Force
    Write-Host "DirectML build compressed into $zipFileName."

    if ($versionUpdated) {
        # Checkout to master branch
        git checkout main
        # Commit and push the changes
        git add $csprojPath
        git commit -m "Updated version to $currentVersion"
        git push origin main
        Write-Host "Changes committed and pushed to main."
    }
}
