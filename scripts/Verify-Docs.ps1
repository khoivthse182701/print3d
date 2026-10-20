$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$files = @(Get-Item (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'CONTRIBUTING.md'))
$files += Get-ChildItem (Join-Path $repoRoot 'docs') -Recurse -Filter '*.md' -File |
    Where-Object { $_.FullName -notmatch '[\\/]archive[\\/]' }
$files += Get-ChildItem (Join-Path $repoRoot 'database') -Recurse -Filter '*.md' -File
$errors = @()
$linkCount = 0
foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
    foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim('<','>')
        if ($target -match '^(https?://|mailto:|#)') { continue }
        $target = [uri]::UnescapeDataString(($target -split '#')[0])
        if (-not $target) { continue }
        $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        $linkCount++
        if (-not (Test-Path -LiteralPath $resolved)) { $errors += "$($file.Name): missing $target" }
    }
}
$sourceDir = Join-Path $repoRoot 'docs/architecture/diagrams'
$gallery = Get-Content -LiteralPath (Join-Path $sourceDir 'README.md') -Raw -Encoding utf8
foreach ($source in (Get-ChildItem -LiteralPath $sourceDir -Filter '*.mmd')) {
    $body = (Get-Content -LiteralPath $source.FullName -Raw -Encoding utf8).Trim().Replace("`r`n","`n")
    if (-not $gallery.Replace("`r`n","`n").Contains($body)) { $errors += "Gallery stale: $($source.Name)" }
}
if ($errors.Count) { throw ($errors -join "`n") }
Write-Output "PASS: $($files.Count) documents, $linkCount local links, Mermaid source/gallery consistency"
