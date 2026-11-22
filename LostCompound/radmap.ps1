param(
    # 不写死默认路径：先看传进来的，再自己按 $PSScriptRoot 往上/往下找。
    # 游戏通过 Process.Start 启动时会显式传绝对路径（那时工作目录是 bin\Debug\net10.0，
    # 跟仓库根对不上，靠猜必错）；玩家手动双击时走下面的 Find-MapPath。
    [string]$MapPath = ""
)

$ErrorActionPreference = "Stop"

function Find-MapPath {
    param([string]$Hint)

    if ($Hint -and (Test-Path $Hint)) { return $Hint }

    $name = "radmap.log"
    $roots = @($PSScriptRoot, (Get-Location).Path)

    foreach ($root in $roots) {
        if (-not $root) { continue }
        # 常见位置：仓库根的 LostCompound\bin\Debug\net10.0\，以及任意深度下的 bin\**
        $direct = Join-Path $root ("LostCompound\bin\Debug\net10.0\" + $name)
        if (Test-Path $direct) { return $direct }
        $direct2 = Join-Path $root $name
        if (Test-Path $direct2) { return $direct2 }
        $found = Get-ChildItem -Path $root -Filter $name -Recurse -File -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    # 还找不到就返回「我以为应该在的地方」，主循环会一直等它出现
    if ($Hint) { return $Hint }
    return (Join-Path $PSScriptRoot ("LostCompound\bin\Debug\net10.0\" + $name))
}

$SW = New-Object System.Diagnostics.Stopwatch
$SW.Start()

# ---------------------------------------------------------------------------
# 颜色映射
#
#   t=0（最安全）-> 深绿    t=1（最致命）-> 亮红
#   为了让「成片」看得出来，故意做成离散色阶（而不是连续渐变）：
#   TUI 里一格只有两三个字符宽，连续渐变的差异人眼根本分不出，
#   反而是「同一档颜色」会把整条毒气带糊成一片，噪点则一格格跳档。
#
#   色阶顺序按观众的直觉：绿 -> 黄绿 -> 黄 -> 橙 -> 红 -> 暗红（越红越危险）。
# ---------------------------------------------------------------------------
$Palette = @(
    @{ Max = 0.10; Name = "FgDarkGreen"; Fg = [ConsoleColor]::DarkGreen },
    @{ Max = 0.25; Name = "FgGreen";     Fg = [ConsoleColor]::Green },
    @{ Max = 0.42; Name = "FgDarkYellow";Fg = [ConsoleColor]::DarkYellow },
    @{ Max = 0.58; Name = "FgYellow";    Fg = [ConsoleColor]::Yellow },
    @{ Max = 0.72; Name = "FgDarkRed";   Fg = [ConsoleColor]::DarkRed },
    @{ Max = 0.86; Name = "FgRed";       Fg = [ConsoleColor]::Red },
    @{ Max = 2.00; Name = "FgWhiteOnRed"; Fg = [ConsoleColor]::Red }   # 最毒一档：加个标记
)

function Get-Bucket($t) {
    foreach ($p in $Palette) {
        if ($t -lt $p.Max) { return $p }
    }
    return $Palette[$Palette.Count - 1]
}

function Read-Map($path) {
    $state = "waiting"; $title = "辐射地图"; $note = ""; $layer = 0
    $roomRow = -1; $roomCol = -1; $rows = 0; $cols = 0; $min = 0.0; $max = 1.0
    $cells = @()

    foreach ($line in [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)) {
        if ($line.Length -eq 0) { continue }
        $sp = $line.IndexOf(' ')
        if ($sp -lt 0) { continue }
        $key = $line.Substring(0, $sp)
        $rest = $line.Substring($sp + 1).Trim()
        switch ($key) {
            "STATE" { $state = $rest }
            "TITLE" { $title = $rest }
            "NOTE"  { $note = $rest }
            "LAYER" { $layer = [int]$rest }
            "ROOM"  { $p = $rest.Split(' '); $roomRow = [int]$p[0]; $roomCol = [int]$p[1] }
            "RANGE" { $p = $rest.Split(' '); $min = [double]$p[0]; $max = [double]$p[1] }
            "GRID"  { $p = $rest.Split(' '); $rows = [int]$p[0]; $cols = [int]$p[1] }
            "CELL"  {
                $p = $rest.Split(' ')
                $cells += ,@([int]$p[0], [int]$p[1], [double]$p[2], $(if ($p.Count -gt 3) { $p[3] } else { "" }))
            }
        }
    }

    return @{
        State = $state; Title = $title; Note = $note; Layer = $layer
        RoomRow = $roomRow; RoomCol = $roomCol
        Rows = $rows; Cols = $cols; Min = $min; Max = $max; Cells = $cells
    }
}

function Show-Header($m) {
    Clear-Host
    Write-Host ""
    Write-Host ($m.Title) -ForegroundColor Cyan -NoNewline
    if ($m.State -eq "ready") {
        Write-Host ("   第 " + $m.Layer + " 层   " + $m.Rows + "x" + $m.Cols) -ForegroundColor DarkGray
    } else {
        Write-Host ""
    }

    if ($m.State -eq "ready") {
        # 图例：左绿右红，中间标出两端对应的秒数
        Write-Host "" -NoNewline
        for ($i = 0; $i -lt $Palette.Count; $i++) {
            $pf = $Palette[$i].Fg
            Write-Host ([string][char]0x2588 + [string][char]0x2588) -ForegroundColor $pf -NoNewline
        }
        Write-Host ("   安全 " + $m.Max.ToString("F1") + " 秒/次") -ForegroundColor Green -NoNewline
        Write-Host " -> " -NoNewline
        Write-Host ("致命 " + $m.Min.ToString("F1") + " 秒/次") -ForegroundColor Red
        Write-Host ($m.Note) -ForegroundColor DarkGray
        Write-Host ""
    }
    else {
        Write-Host ""
        Write-Host ($m.Note) -ForegroundColor Yellow
        Write-Host ""
        Write-Host "进放射性区域、并带上盖革计数器后，这里会自动出现热力图。" -ForegroundColor DarkGray
        Write-Host "（主菜单输 255 可以开 debug，再用 129 拿一个盖革计数器）" -ForegroundColor DarkGray
    }
}

$EmptyMark = [string][char]0x00B7   # 空房间标记 · （与游戏侧 RadiationMapExport.EmptyCellLabel 一致）

function Show-Grid($m) {
    if ($m.State -ne "ready") { return }

    # 每格占 3 个字符（2 个色块 + 1 个间隔）：格子有标签时把标签压在**中间那格**上，
    # 这样即使一格 3 字符，也还能看清 F / B / T / C / E / S。
    #
    # 行首不留缩进（Hyberty 要求「列之前不要有空格」）：
    # 缩进会让整个网格整体右移，和上面的图例/标题对不齐，看起来像没对齐的表格。
    for ($r = 0; $r -lt $m.Rows; $r++) {
        for ($c = 0; $c -lt $m.Cols; $c++) {
            $cell = $null
            foreach ($x in $m.Cells) {
                if ($x[0] -eq $r -and $x[1] -eq $c) { $cell = $x; break }
            }

            $t = 0.0
            $label = ""
            if ($null -ne $cell) { $t = $cell[2]; $label = $cell[3] }

            $bucket = Get-Bucket $t
            $isPlayer = ($r -eq $m.RoomRow -and $c -eq $m.RoomCol)

            if ($isPlayer) {
                Write-Host " * " -ForegroundColor Black -BackgroundColor White -NoNewline
            }
            elseif ($label -eq $EmptyMark) {
                # 没有房间的格子（Hyberty：空房间用 · 表示）—— 仍然上色，
                # 否则只剩房间时玩家以为格子是随机跳着排的，看不出这是一张连续的数组。
                Write-Host (" " + $EmptyMark + " ") -ForegroundColor DarkGray -BackgroundColor $bucket.Fg -NoNewline
            }
            elseif ($label.Length -gt 0) {
                Write-Host (" " + $label + " ") -ForegroundColor Black -BackgroundColor $bucket.Fg -NoNewline
            }
            else {
                Write-Host ([string][char]0x2588 + [string][char]0x2588 + " ") -ForegroundColor $bucket.Fg -NoNewline
            }
        }
        Write-Host ""
    }
}

function Show-Footer($m) {
    if ($m.State -eq "ready") {
        Write-Host ""
        Write-Host "颜色 = 这一格的辐射强度（柏林噪声铺出来的，成片出现的部分就是毒气带）" -ForegroundColor DarkGray
        Write-Host "* = 你现在的房间   每格 3 个字符，标签 F/B/T/C/E/S 是房型" -ForegroundColor DarkGray
    }
    Write-Host ""
    Write-Host ("跟读中： " + $MapPath) -ForegroundColor DarkGray
    Write-Host "Ctrl+C 退出" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# 主循环：等文件 -> 有变化就重画 -> 200ms 一次
# ---------------------------------------------------------------------------
$MapPath = Find-MapPath -Hint $MapPath
Write-Host ""
Write-Host "等待游戏写出辐射地图……" -ForegroundColor DarkGray
Write-Host ($MapPath) -ForegroundColor DarkGray
while (-not (Test-Path $MapPath)) { Start-Sleep -Milliseconds 400 }

$lastStamp = ""

while ($true) {
    try {
        $stamp = (Get-Item $MapPath).LastWriteTimeUtc.Ticks
        if ($stamp -ne $lastStamp) {
            $lastStamp = $stamp
            $map = Read-Map $MapPath
            Show-Header $map
            Show-Grid $map
            Show-Footer $map
        }
    }
    catch {
        # 游戏正在覆盖写、文件短暂不可读 —— 忽略，下一轮再来
    }
    Start-Sleep -Milliseconds 200
}
