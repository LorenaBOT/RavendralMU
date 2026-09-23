$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$stage = Join-Path $env:RUNNER_TEMP "ravendral-payload"
$payload = Join-Path $env:RUNNER_TEMP "ravendral-client.zip"
$stub = Join-Path $env:RUNNER_TEMP "RavendralInstallerStub.exe"
$outDir = Join-Path $repo "dist"
$outExe = Join-Path $outDir "MU-Ravendral-Season6E3-Installer.exe"
$outZip = Join-Path $outDir "MU-Ravendral-Season6E3-Installer.zip"

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stage,$outDir | Out-Null

# Copy the public client only. Never package repository/build metadata.
Get-ChildItem -LiteralPath $repo -Force | Where-Object {
    $_.Name -notin @(".git", ".github", "installer", "dist", ".gitattributes", ".gitignore")
} | ForEach-Object {
    if ($_.Name -eq "MU Ravendral.lnk") { return }
    Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse -Force
}


# Repair MU checksum-protected filter files before packaging.
function Repair-MuChecksumFile([string]$Path, [UInt16]$Key) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 8) { throw "Invalid MU data file: $Path" }

    $dataLength = $bytes.Length - 4
    $dwKey = [UInt32]$Key
    $result = [UInt32](($dwKey -shl 9) -band 0xFFFFFFFF)

    for ($checked = 0; $checked -le ($dataLength - 4); $checked += 4) {
        $temp = [BitConverter]::ToUInt32($bytes, $checked)
        if (((($checked / 4) + $Key) % 2) -eq 0) {
            $result = [UInt32](($result -bxor $temp) -band 0xFFFFFFFF)
        } else {
            $result = [UInt32](([UInt64]$result + [UInt64]$temp) % 4294967296)
        }

        if (($checked % 16) -eq 0) {
            $shift = (($checked / 4) % 8) + 1
            $mix = [UInt32](([UInt64]$dwKey + [UInt64]$result) % 4294967296)
            $result = [UInt32](($result -bxor ($mix -shr $shift)) -band 0xFFFFFFFF)
        }
    }

    $check = [BitConverter]::GetBytes([UInt32]$result)
    [Array]::Copy($check, 0, $bytes, $dataLength, 4)
    [IO.File]::WriteAllBytes($Path, $bytes)

    $verify = [BitConverter]::ToUInt32([IO.File]::ReadAllBytes($Path), $dataLength)
    if ($verify -ne $result) { throw "Checksum repair verification failed: $Path" }
}

$filter = Join-Path $stage "Data\Local\Filter.bmd"
$filterName = Join-Path $stage "Data\Local\FilterName.bmd"
if (!(Test-Path $filter)) { throw "Missing Data\Local\Filter.bmd" }
if (!(Test-Path $filterName)) { throw "Missing Data\Local\FilterName.bmd" }
if ((Get-Item $filter).Length -ne 20004) { throw "Unexpected Filter.bmd size: $((Get-Item $filter).Length)" }
if ((Get-Item $filterName).Length -ne 10004) { throw "Unexpected FilterName.bmd size: $((Get-Item $filterName).Length)" }

Repair-MuChecksumFile $filter 0x3E7D
Repair-MuChecksumFile $filterName 0x2BC1
Write-Host "MU filter checksums repaired and verified."

# Ensure Git LFS pointers were replaced by real executables.
$required = @("MU Client Ravendral.exe", "main.exe", "Mu.exe")
foreach ($name in $required) {
    $p = Join-Path $stage $name
    if (!(Test-Path $p)) { throw "Missing required client file: $name" }
    if ((Get-Item $p).Length -lt 1024) { throw "Invalid/LFS pointer instead of real executable: $name" }
}

# Create SHA-256 integrity manifest using ZIP-style forward-slash paths.
$manifestPath = Join-Path $stage "_ravendral_manifest.sha256"
$lines = New-Object System.Collections.Generic.List[string]
Get-ChildItem $stage -File -Recurse | Where-Object { $_.FullName -ne $manifestPath } | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1).Replace("\", "/")
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    $lines.Add("$hash|$rel")
}
[IO.File]::WriteAllLines($manifestPath, $lines, (New-Object Text.UTF8Encoding($false)))

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $payload) { Remove-Item $payload -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $payload, [IO.Compression.CompressionLevel]::Optimal, $false)

# Verify archive integrity before building installer.
$za = [IO.Compression.ZipFile]::OpenRead($payload)
try {
    if ($null -eq $za.GetEntry("_ravendral_manifest.sha256")) { throw "Integrity manifest missing from payload." }
    if ($null -eq $za.GetEntry("MU Client Ravendral.exe")) { throw "Main Ravendral executable missing from payload." }
    foreach ($entry in $za.Entries) {
        if ($entry.FullName.StartsWith(".github/", [StringComparison]::OrdinalIgnoreCase) -or
            $entry.FullName.StartsWith("installer/", [StringComparison]::OrdinalIgnoreCase)) {
            throw "Forbidden path packaged: $($entry.FullName)"
        }
    }
} finally { $za.Dispose() }

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "C# compiler not found." }

& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:"$PSScriptRoot\app.manifest" /out:$stub /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\RavendralInstaller.cs"
if ($LASTEXITCODE -ne 0) { throw "Custom installer compilation failed." }

# Build a single self-contained EXE: PE stub + payload ZIP + 16-byte marker + Int64 payload length.
$payloadLength = (Get-Item $payload).Length
$out = [IO.File]::Open($outExe, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    foreach ($part in @($stub, $payload)) {
        $input = [IO.File]::OpenRead($part)
        try { $input.CopyTo($out) } finally { $input.Dispose() }
    }
    $marker = [Text.Encoding]::ASCII.GetBytes("RAVENDRALPAYLOAD")
    if ($marker.Length -ne 16) { throw "Invalid payload marker." }
    $out.Write($marker, 0, $marker.Length)
    $lenBytes = [BitConverter]::GetBytes([Int64]$payloadLength)
    $out.Write($lenBytes, 0, $lenBytes.Length)
} finally { $out.Dispose() }

# Final structural validation of the self-extracting EXE.
$fs = [IO.File]::OpenRead($outExe)
try {
    if ($fs.Length -le ($payloadLength + 24)) { throw "Final installer size is invalid." }
    $fs.Seek(-24, [IO.SeekOrigin]::End) | Out-Null
    $m = New-Object byte[] 16
    [void]$fs.Read($m, 0, 16)
    if ([Text.Encoding]::ASCII.GetString($m) -ne "RAVENDRALPAYLOAD") { throw "Final installer marker verification failed." }
    $l = New-Object byte[] 8
    [void]$fs.Read($l, 0, 8)
    if ([BitConverter]::ToInt64($l, 0) -ne $payloadLength) { throw "Final installer payload length verification failed." }
} finally { $fs.Dispose() }

Compress-Archive -LiteralPath $outExe -DestinationPath $outZip -CompressionLevel Optimal -Force
Write-Host "CUSTOM INSTALLER BUILT: $outZip"
Write-Host "EXE SHA256: $((Get-FileHash $outExe -Algorithm SHA256).Hash)"
Write-Host "ZIP SHA256: $((Get-FileHash $outZip -Algorithm SHA256).Hash)"
