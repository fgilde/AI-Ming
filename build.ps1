param(
    [string]$version,
    [string]$incVersion
)

# Call this script with "powershell -ExecutionPolicy Bypass -File .\build.ps1"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$outputDir = Join-Path $scriptDir "Aimmy2/bin/Release"

# Funktion f�r das Erstellen mit oder ohne CUDA
function Build-ProjectWithCuda {
    param(
        [bool]$isCuda
    )

    # Setze die IsCuda Variable
    $isCudaValue = if ($isCuda) { "true" } else { "false" }

    # Clean and Build das Projekt mit der IsCuda Variable
    dotnet clean --configuration Release
    if($isCuda) {
        dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue -p:DefineConstants="IsCuda"
	    #dotnet publish Aimmy2/Aimmy2.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue -p:DefineConstants="IsCuda"
	    #dotnet publish Launcher/Launcher.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue -p:DefineConstants="IsCuda"
	} else {
		dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue
		#dotnet publish Aimmy2/Aimmy2.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue
		#dotnet publish Launcher/Launcher.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -p:DebugType=None -p:IsCuda=$isCudaValue
	}       
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
$csprojPath = Join-Path $scriptDir "Aimmy2/Aimmy2.csproj"
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

# Normal Build
$buildSucceeded = $true
try {
    Write-Host "Building the project with CUDA..."
    Build-ProjectWithCuda -isCuda:$true
} catch {
    $buildSucceeded = $false
    Write-Host "Build failed. Reverting .csproj file to the old version."
    Replace-Version -oldVersion $currentVersion -newVersion $oldversion -filePath $csprojPath
}

if ($buildSucceeded) {
    $paths = @("net8.0-windows", "win-x64", "publish")
    $zipContent = $outputDir
    foreach ($path in $paths) {
        $zipContent = Join-Path $zipContent $path
    }

    # Define the zip file name and path    
    $zipFileName = "Release_${currentVersion}_cuda.zip"
    $zipFileName  = $zipFileName -replace '(^\s+|\s+$)','' -replace '\s+',' '

    $zipFilePath = Join-Path $outputDir $zipFileName

    # Compress the output directory into a zip file
    Compress-Archive -Path $zipContent\* -DestinationPath $zipFilePath

    Write-Host "Output directory compressed into $zipFileName in the Release folder."

    # Rename and copy Launcher.exe to Installer.exe
    $launcherPath = Join-Path $zipContent "Launcher.exe"
    $installerPath = Join-Path $outputDir "Installer.exe"

    if (Test-Path $launcherPath) {
        Copy-Item -Path $launcherPath -Destination $installerPath
        Write-Host "Launcher.exe copied and renamed to Installer.exe in the Release folder."
    } else {
        Write-Host "Launcher.exe not found, skipping the copy operation."
    }

    # CUDA Build
    Write-Host "Building the project without CUDA..."
    Build-ProjectWithCuda -isCuda:$false

    # ZIP CUDA build
    $zipFileName = "Release_${currentVersion}.zip"
    $zipFileName  = $zipFileName -replace '(^\s+|\s+$)','' -replace '\s+',' '

    $cudaZipFilePath = Join-Path $outputDir $zipFileName

    # Compress the output directory into a CUDA zip file
    Compress-Archive -Path $zipContent\* -DestinationPath $cudaZipFilePath

    Write-Host "Output directory compressed into $zipFileName in the Release folder."

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
