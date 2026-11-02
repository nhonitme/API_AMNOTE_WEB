# G-K 2B static check — no database connection
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$failed = New-Object System.Collections.Generic.List[string]

function Add-Fail([string]$msg) {
    [void]$failed.Add($msg)
    Write-Host ("FAIL: " + $msg) -ForegroundColor Red
}

foreach ($f in @("20261102_2b_00_preflight_dev.sql", "20261102_2b_00_guard_inline.sql", "20261102_2b_APPROVED_ENV.sql")) {
    $c = Get-Content -Raw (Join-Path $here $f)
    if ($c -match '(?i)\b(DROP|CREATE)\s+(PROCEDURE|TABLE|FUNCTION|TRIGGER)\b') {
        Add-Fail "$f contains DROP/CREATE DDL (must be DDL-free)"
    }
}

$install = Get-Content -Raw (Join-Path $here "20261102_2b_00_install_dev.sql")
if ($install -notmatch 'SOURCE\s+20261102_2b_00_preflight_dev\.sql') {
    Add-Fail "install missing SOURCE preflight"
}
$idxPf = $install.IndexOf("20261102_2b_00_preflight_dev.sql")
$idx01 = $install.IndexOf("20261102_2b_01_tables.sql")
if ($idxPf -lt 0 -or $idx01 -lt 0 -or $idxPf -gt $idx01) {
    Add-Fail "install must SOURCE preflight before 01_tables"
}

$rollback = Get-Content -Raw (Join-Path $here "20261102_2b_rollback_dev.sql")
if ($rollback -notmatch 'SOURCE\s+20261102_2b_00_preflight_dev\.sql') {
    Add-Fail "rollback missing SOURCE preflight"
}
$rPf = $rollback.IndexOf("20261102_2b_00_preflight_dev.sql")
$rDrop = $rollback.IndexOf("DROP PROCEDURE")
if ($rPf -lt 0 -or $rDrop -lt 0 -or $rPf -gt $rDrop) {
    Add-Fail "rollback must SOURCE preflight before DROP PROCEDURE"
}

$cogs = Get-Content -Raw (Join-Path $here "20261102_2b_04_sp_period_lock_cogs.sql")
$m = [regex]::Match($cogs, '(?s)DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_create_voucher`.+?END proc\$\$')
if (-not $m.Success) {
    Add-Fail "cannot locate create_voucher body"
}
else {
    $body = $m.Value
    $h = [regex]::Match($body, '(?s)DECLARE EXIT HANDLER FOR SQLEXCEPTION\s*BEGIN(.*?)END;')
    if (-not $h.Success) {
        Add-Fail "create_voucher missing EXIT HANDLER"
    }
    elseif ($h.Groups[1].Value -match 'rollback_period|delete_lock_vouchers') {
        Add-Fail "create_voucher handler must not delete vouchers on error"
    }
    elseif ($h.Groups[1].Value -notmatch 'RESIGNAL') {
        Add-Fail "create_voucher handler must RESIGNAL"
    }
}
if ($cogs -notmatch "@GK_2B_FAIL_POINT") {
    Add-Fail "create_voucher missing FAIL_POINT test hooks"
}
if ($cogs -notmatch "AFTER_DELETE" -or $cogs -notmatch "DURING_POST") {
    Add-Fail "create_voucher missing AFTER_DELETE / DURING_POST hooks"
}

$envSql = Get-Content -Raw (Join-Path $here "20261102_2b_APPROVED_ENV.sql")
if ($envSql -match "SET_AFTER_DEV_APPROVAL") {
    Write-Host "NOTE: APPROVED_ENV still placeholder - install will hard-stop until filled." -ForegroundColor Yellow
}

foreach ($f in @(
    "20261102_2b_01_tables.sql",
    "20261102_2b_02_sp_engine.sql",
    "20261102_2b_03_sp_nvl_basis.sql",
    "20261102_2b_04_sp_period_lock_cogs.sql"
)) {
    $c = Get-Content -Raw (Join-Path $here $f)
    if ($c -notmatch 'SOURCE\s+20261102_2b_00_guard_inline\.sql') {
        Add-Fail "$f missing SOURCE guard_inline"
    }
}

$test = Get-Content -Raw (Join-Path $here "20261102_2b_98_test_scenarios_dev.sql")
foreach ($t in @("T9_BI_REJECTED", "T10_AFTER_DELETE", "T11_DURING_POST")) {
    if ($test -notmatch [regex]::Escape($t)) {
        Add-Fail "98_test missing $t"
    }
}

# C# transaction path
$repo = Join-Path $here "..\..\..\Repositories\PeriodLockRepository.cs"
if (Test-Path $repo) {
    $cs = Get-Content -Raw $repo
    if ($cs -notmatch "RunCogsCreateVoucherAndCompleteAsync") {
        Add-Fail "PeriodLockRepository missing RunCogsCreateVoucherAndCompleteAsync"
    }
    if ($cs -notmatch "session\.Commit\(\)") {
        Add-Fail "COGS complete path missing session.Commit"
    }
    if ($cs -notmatch "session\.Rollback\(\)") {
        Add-Fail "COGS complete path missing session.Rollback"
    }
}

if ($failed.Count -gt 0) {
    Write-Host ("Static check FAILED: " + $failed.Count + " issue(s)") -ForegroundColor Red
    exit 1
}

Write-Host "Static check PASS" -ForegroundColor Green
exit 0
