# Checks the code-signing step in build.ps1 without running a build and without a real certificate.
# Run:  pwsh -File Tests/Signing.Tests.ps1
#
# A throwaway self-signed certificate can prove everything except chain trust — its root is not
# trusted, so a signature made with it never reaches status 'Valid'. That is used on purpose here:
# it exercises both halves of the guard, the "signature applied" path and the "signature does not
# validate, so fail the build" path.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$failed = 0

function Check($name, [scriptblock]$body) {
    try { & $body; Write-Host "  PASS  $name" }
    catch { $script:failed++; Write-Host "  FAIL  $name : $_" -ForegroundColor Red }
}

# Load just the signing functions out of build.ps1 — dot-sourcing the file would start a build.
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'build.ps1'), [ref]$null, [ref]$null)
$wanted = 'Get-SigningCertificate', 'Invoke-CodeSigning'
$ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $wanted -contains $n.Name }, $true) |
    ForEach-Object { Invoke-Expression $_.Extent.Text }

# A publish folder look-alike: two executables at the top (ours) and one under Resources
# (third-party, must stay untouched).
$work = Join-Path ([IO.Path]::GetTempPath()) ("signtest_" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path (Join-Path $work 'Resources') | Out-Null
$sample = Get-ChildItem -Path (Join-Path $root 'PowerAim/bin/Release') -Filter *.exe -Recurse -File -ErrorAction SilentlyContinue |
          Sort-Object Length | Select-Object -First 1
if (-not $sample) { Write-Host "No sample .exe under PowerAim/bin/Release — build once, then re-run."; exit 2 }
Copy-Item $sample.FullName (Join-Path $work 'App.exe')
Copy-Item $sample.FullName (Join-Path $work 'Launcher.exe')
Copy-Item $sample.FullName (Join-Path $work 'Resources\ThirdParty.exe')

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=PowerAim Signing Test' `
            -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(1)

$saved = @{}
'SIGN_CERT_THUMBPRINT', 'SIGN_CERT_PFX', 'SIGN_CERT_PASSWORD', 'SIGN_COMMAND', 'SIGN_REQUIRED', 'SIGN_TIMESTAMP_URL' |
    ForEach-Object { $saved[$_] = [Environment]::GetEnvironmentVariable($_) ; Remove-Item "env:$_" -ErrorAction SilentlyContinue }

try {
    Write-Host "Code signing checks"

    Check 'no certificate configured: warns, does not throw' {
        Invoke-CodeSigning -publishDir $work 3>$null
    }

    Check 'no certificate configured + SIGN_REQUIRED: fails the build' {
        $env:SIGN_REQUIRED = '1'
        try { Invoke-CodeSigning -publishDir $work; throw 'expected a throw' }
        catch { if ("$_" -notmatch 'SIGN_REQUIRED') { throw "wrong error: $_" } }
        Remove-Item env:SIGN_REQUIRED
    }

    Check 'unknown thumbprint is reported, not silently skipped' {
        $env:SIGN_CERT_THUMBPRINT = '0000000000000000000000000000000000000000'
        try { Invoke-CodeSigning -publishDir $work; throw 'expected a throw' }
        catch { if ("$_" -notmatch 'no certificate with thumbprint') { throw "wrong error: $_" } }
        Remove-Item env:SIGN_CERT_THUMBPRINT
    }

    $env:SIGN_CERT_THUMBPRINT = $cert.Thumbprint
    Invoke-CodeSigning -publishDir $work 3>$null 6>$null | Out-Null

    Check 'our executables carry a signature from the configured certificate' {
        foreach ($n in 'App.exe', 'Launcher.exe') {
            $sig = Get-AuthenticodeSignature (Join-Path $work $n)
            if ($sig.SignerCertificate.Thumbprint -ne $cert.Thumbprint) { throw "$n was not signed with the configured certificate" }
        }
    }

    Check 'third-party executables under Resources are left alone' {
        $sig = Get-AuthenticodeSignature (Join-Path $work 'Resources\ThirdParty.exe')
        if ($sig.SignerCertificate) { throw 'Resources\ThirdParty.exe was signed, but must not be' }
    }

    Check 'a signature that does not validate fails the build when SIGN_REQUIRED is set' {
        # The self-signed root is untrusted, so verification must reject it — this is the guard that
        # stops an unsigned or broken release from being zipped and uploaded.
        Copy-Item $sample.FullName (Join-Path $work 'App.exe') -Force
        $env:SIGN_REQUIRED = '1'
        try { Invoke-CodeSigning -publishDir $work 3>$null; throw 'expected a throw' }
        catch { if ("$_" -notmatch 'Code signing failed') { throw "wrong error: $_" } }
        Remove-Item env:SIGN_REQUIRED
    }

    Check 'SIGN_COMMAND receives the file path and its exit code is honoured' {
        Remove-Item env:SIGN_CERT_THUMBPRINT
        $log = Join-Path $work 'command.log'
        $env:SIGN_COMMAND = "pwsh -NoProfile -Command `"'{file}' | Add-Content -Path '$log'; exit 1`""
        $env:SIGN_REQUIRED = '1'
        try { Invoke-CodeSigning -publishDir $work 3>$null; throw 'expected a throw' }
        catch { if ("$_" -notmatch 'Code signing failed') { throw "wrong error: $_" } }
        $seen = Get-Content $log
        if (-not ($seen -match 'Launcher\.exe$')) { throw "SIGN_COMMAND never saw the real path: $seen" }
    }
}
finally {
    $saved.GetEnumerator() | ForEach-Object {
        if ($null -eq $_.Value) { Remove-Item "env:$($_.Key)" -ErrorAction SilentlyContinue }
        else { Set-Item "env:$($_.Key)" $_.Value }
    }
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failed -gt 0) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All checks passed."
