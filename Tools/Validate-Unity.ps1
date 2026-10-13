param(
    [string]$EditorPath = '',
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).ProviderPath
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
$version = [regex]::Match([IO.File]::ReadAllText($versionFile), 'm_EditorVersion: (\S+)').Groups[1].Value
if (!$EditorPath) {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"),
        (Join-Path ([Environment]::GetFolderPath('MyDocuments')) "Codex\UnityEditors\$version\Editor\Unity.exe")
    )
    $EditorPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$EditorPath -or !(Test-Path -LiteralPath $EditorPath)) {
    throw 'Unity editor not found. Pass -EditorPath with the full path to Editor\Unity.exe.'
}

# Unity 6000.6.0f1's bundled render pipeline contains converter partial
# scripts without .meta files. Unity ignores them in immutable packages.
# Add only missing metadata in the disposable project cache; keep vendor code intact.
function Repair-ConverterMetadata {
    $cache = Join-Path $ProjectPath 'Library\PackageCache'
    $count = 0
    if (!(Test-Path -LiteralPath $cache)) { return $count }
    foreach ($package in Get-ChildItem -LiteralPath $cache -Directory -Filter 'com.unity.render-pipelines.*') {
        foreach ($script in Get-ChildItem -LiteralPath $package.FullName -Recurse -File -Filter 'ReadonlyMaterialConverter.*.cs') {
            $meta = $script.FullName + '.meta'
            if (Test-Path -LiteralPath $meta) { continue }
            $relative = $script.FullName.Substring($package.FullName.Length).Replace('\','/')
            $key = ($package.Name -split '@')[0] + $relative
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key)) }
            finally { $sha.Dispose() }
            $guid = ([BitConverter]::ToString($bytes)).Replace('-','').Substring(0,32).ToLowerInvariant()
            [IO.File]::WriteAllText($meta, "fileFormatVersion: 2`nguid: $guid`n", (New-Object Text.UTF8Encoding($false)))
            $count++
        }
    }
    return $count
}

# Use the existing Windows proxy only for this process and its Unity child.
if (!$env:HTTPS_PROXY) {
    $registry = [Uri]'https://packages.unity.com'
    $proxy = [Net.WebRequest]::DefaultWebProxy.GetProxy($registry)
    if ($proxy -and $proxy.AbsoluteUri -ne $registry.AbsoluteUri) {
        $env:HTTPS_PROXY = $proxy.AbsoluteUri
        $env:HTTP_PROXY = $proxy.AbsoluteUri
    }
}
if (!$env:NO_PROXY) { $env:NO_PROXY = 'localhost,127.0.0.1' }
$reports = Join-Path $ProjectPath 'ValidationReports'
$null = New-Item -ItemType Directory -Path $reports -Force
$log = Join-Path $reports 'unity-validation.log'
$report = Join-Path $reports 'conveyor-validation.json'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
function Invoke-Validation {
    $arguments = '-batchmode -projectPath "' + $ProjectPath + '" -executeMethod ConveyorValidation.RunBatch -logFile "' + $log + '"'
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    return $process.ExitCode
}
$repaired = Repair-ConverterMetadata
Write-Host "Prepared $repaired missing converter metadata files."
$code = Invoke-Validation
if ($code -ne 0 -and (Test-Path -LiteralPath $log)) {
    $text = Get-Content -Raw -LiteralPath $log
    if ($text -match 'MaterialReferenceChanger.*could not be found|ReadonlyMaterialConverter\.[^\r\n]+has no meta file') {
        $repaired = Repair-ConverterMetadata
        if ($repaired -gt 0) {
            Write-Host "Repaired $repaired converter metadata files after the initial package import; retrying."
            $code = Invoke-Validation
        }
    }
}
Write-Host "Unity exit code: $code. Log: $log"
if (Test-Path -LiteralPath $report) { Get-Content -Raw -LiteralPath $report }
exit $code
