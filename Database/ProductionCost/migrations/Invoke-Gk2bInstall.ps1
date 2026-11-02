<#
.SYNOPSIS
  G-K 2B install orchestrator — preflight trước, abort nếu FAIL, không chạy khi placeholder.
.NOTES
  Không kết nối mặc định. Truyền -MysqlArgs hoặc chỉnh bên dưới sau khi DEV được duyệt.
  CẤM production.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MysqlExe = "mysql",

    [Parameter(Mandatory = $true)]
    [string[]]$MysqlArgs,

    [switch]$Rollback
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
Set-Location $here

$approved = Get-Content -Raw (Join-Path $here "20261102_2b_APPROVED_ENV.sql")
if ($approved -match 'SET_AFTER_DEV_APPROVAL') {
    throw "APPROVED_ENV still contains SET_AFTER_DEV_APPROVAL — fill approved DB/host before install."
}

$target = if ($Rollback) { "20261102_2b_rollback_dev.sql" } else { "20261102_2b_00_install_dev.sql" }
Write-Host "Running $target with --abort-source-on-error (cwd=$here)"

& $MysqlExe @MysqlArgs --abort-source-on-error -e "SOURCE $target"
if ($LASTEXITCODE -ne 0) {
    throw "mysql failed with exit $LASTEXITCODE — migration stopped (no further SOURCE)."
}

Write-Host "Done."
