# Erzeugt database/setup_all.sql aus setup_all.header.sql und den nummerierten
# Skripten in database/setup/. Nach jeder Aenderung an einem Einzelskript ausfuehren:
#   powershell -ExecutionPolicy Bypass -File database/build-setup-all.ps1
# (Datei bewusst nur ASCII, da Windows PowerShell 5.1 Skripte ohne BOM als ANSI liest.)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Utf8($path) { [System.IO.File]::ReadAllText($path, $utf8).Replace("`r`n", "`n").TrimEnd() }

$parts = @(Read-Utf8 (Join-Path $root 'setup_all.header.sql'))
$scripts = Get-ChildItem (Join-Path $root 'setup') -Filter '*.sql' | Sort-Object Name
foreach ($file in $scripts) {
    $parts += "-- >>> setup/$($file.Name)`n" + (Read-Utf8 $file.FullName)
}
$parts += 'commit;'

$target = Join-Path $root 'setup_all.sql'
[System.IO.File]::WriteAllText($target, ($parts -join "`n`n") + "`n", $utf8)
Write-Host "Geschrieben: $target ($($scripts.Count) Skripte)"
