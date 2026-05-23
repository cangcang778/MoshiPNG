# IMA KnowledgeBase - Batch Sync Script
# Scan all .md files in this folder and upload them to the "unity" KB.
# Files whose content has not changed will be skipped at check_repeated.

$ErrorActionPreference = "Stop"

# --- Config ---
$NODE   = "C:\Users\yejunli\.workbuddy\binaries\node\versions\22.12.0\node.exe"
$UPLOAD = "C:\Users\yejunli\.codebuddy\skills\ima-skills\upload_one.cjs"
$KB     = "pxdS_LZF3G9xdmjr2y3kyKo6OZIX_s8Dmo3SHmBzI6Y="
# --- End config ---

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

if (-not (Test-Path $NODE))   { Write-Host "[ERR] node.exe not found: $NODE"     -ForegroundColor Red; exit 1 }
if (-not (Test-Path $UPLOAD)) { Write-Host "[ERR] upload_one.cjs not found: $UPLOAD" -ForegroundColor Red; exit 1 }

$mdFiles = Get-ChildItem -Path $scriptDir -Filter *.md -File | Sort-Object Name
if ($mdFiles.Count -eq 0) {
    Write-Host "[INFO] No .md files found." -ForegroundColor Yellow
    exit 0
}

$total = $mdFiles.Count
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " IMA Sync -> KB: unity" -ForegroundColor Cyan
Write-Host " Files to process: $total" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$idx      = 0
$uploaded = 0
$skipped  = 0
$failed   = 0
$results  = @()

foreach ($f in $mdFiles) {
    $idx++
    $name = $f.Name
    $size = "{0:N1} KB" -f ($f.Length / 1KB)
    Write-Host ""
    Write-Host ("[{0}/{1}] {2}  ({3})" -f $idx, $total, $name, $size) -ForegroundColor White

    $tmpId = [Guid]::NewGuid().ToString("N")
    $out = Join-Path $env:TEMP ("ima_sync_" + $tmpId + ".out.txt")
    $err = Join-Path $env:TEMP ("ima_sync_" + $tmpId + ".err.txt")

    try {
        $p = Start-Process -FilePath $NODE `
            -ArgumentList "`"$UPLOAD`"", "--file", "`"$($f.FullName)`"", "--kb-id", "`"$KB`"" `
            -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $out -RedirectStandardError $err

        $stdout = ""
        $stderr = ""
        if (Test-Path $out) { $stdout = Get-Content $out -Raw -Encoding UTF8 }
        if (Test-Path $err) { $stderr = Get-Content $err -Raw -Encoding UTF8 }

        $status = "FAIL"
        if ($stdout -match "duplicate") {
            $status = "SKIP"
            $skipped++
            Write-Host "  -> duplicate, skipped" -ForegroundColor DarkGray
        }
        elseif ($p.ExitCode -eq 0 -and ($stdout -match "\[5/5\]|success|done")) {
            $status = "OK"
            $uploaded++
            Write-Host "  -> uploaded" -ForegroundColor Green
        }
        else {
            $failed++
            Write-Host ("  -> FAILED (exit={0})" -f $p.ExitCode) -ForegroundColor Red
            if ($stderr) { Write-Host ("     stderr: {0}" -f $stderr) -ForegroundColor DarkRed }
            if ($stdout) {
                Write-Host "     stdout tail:" -ForegroundColor DarkRed
                ($stdout -split "`n" | Select-Object -Last 6) | ForEach-Object {
                    Write-Host ("       {0}" -f $_) -ForegroundColor DarkRed
                }
            }
        }

        $results += [pscustomobject]@{ Name = $name; Size = $size; Status = $status }
    }
    catch {
        $failed++
        Write-Host ("  -> EXCEPTION: {0}" -f $_.Exception.Message) -ForegroundColor Red
        $results += [pscustomobject]@{ Name = $name; Size = $size; Status = "EXCEPTION" }
    }
    finally {
        Remove-Item $out -ErrorAction SilentlyContinue
        Remove-Item $err -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Summary" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
$results | Format-Table -AutoSize
Write-Host ("Uploaded: {0}   Skipped: {1}   Failed: {2}" -f $uploaded, $skipped, $failed) -ForegroundColor Cyan

if ($failed -gt 0) { exit 1 } else { exit 0 }