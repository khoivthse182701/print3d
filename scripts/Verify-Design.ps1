param([string]$Server = 'localhost')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$schemaFile = Join-Path $repoRoot 'database/design/vnext.sql'
$checksFile = Join-Path $repoRoot 'database/design/constraint-checks.sql'
$scratchName = 'TheBoys_DesignCheck_' + [Guid]::NewGuid().ToString('N')
if ($scratchName -notmatch '^TheBoys_DesignCheck_[a-f0-9]{32}$') { throw 'Invalid scratch database name' }
Get-Command sqlcmd -ErrorAction Stop | Out-Null
$created = $false
try {
    & sqlcmd -S $Server -E -C -b -Q "CREATE DATABASE [$scratchName]"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create scratch database' }
    $created = $true
    & sqlcmd -S $Server -E -C -b -d $scratchName -i $schemaFile
    if ($LASTEXITCODE -ne 0) { throw 'Target schema failed to compile' }
    & sqlcmd -S $Server -E -C -b -d $scratchName -i $checksFile
    if ($LASTEXITCODE -ne 0) { throw 'Constraint checks failed' }
    Write-Output "Design verified in isolated database $scratchName"
} finally {
    if ($created) {
        # Only the generated database above can be removed. Never use app configuration.
        if ($scratchName -notmatch '^TheBoys_DesignCheck_[a-f0-9]{32}$') { throw 'Unsafe cleanup target' }
        & sqlcmd -S $Server -E -C -b -Q "ALTER DATABASE [$scratchName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$scratchName]"
        if ($LASTEXITCODE -ne 0) { throw "Scratch cleanup failed: $scratchName" }
    }
}
