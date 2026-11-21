# ============================================================================
#  LostCompound 战斗时间轴 —— 独立窗口实时跟读
#
#  Hyberty 2026-11-21：想要一个单独窗口看回合顺序。
#
#  为什么是「另起一个进程」而不是游戏自己开窗口：
#    实测过 FreeConsole + AllocConsole。两个调用都会"成功"，但主控制台会被
#    立刻切走 —— 游戏主界面已经画在屏幕上的东西变成孤儿，而后续所有绘制都
#    跑到新窗口去。本项目的 TUI 每帧靠真实输出句柄重绘全屏，句柄失效就不刷新了。
#    所以「另开窗口」只能由另一个进程做，游戏只管写文件。
#
#  用法：
#     1. 进 debug 模式（主菜单输 255）—— 时间轴自动开始写 timeline.log
#     2. 双击 / 运行这个脚本，弹出新窗口实时显示
#     3. 关掉游戏后这个窗口不会自己关（方便回看），按 Ctrl+C 退出
#
#  ⚠️ 编码：本文件必须带 UTF-8 BOM。Windows PowerShell 5.1 在无 BOM 时按 GBK 读，
#     中文和箭头符号会被读成乱码、甚至把引号一起吃掉导致语法错。
#     另外这里的源码**刻意全用 ASCII 匹配**（不写箭头/叉这类符号字面量），
#     双保险 —— 万一 BOM 又丢了，脚本至少还能跑。
# ============================================================================

param(
    [string]$LogPath = (Join-Path $PSScriptRoot "LostCompound\bin\Debug\net10.0\timeline.log")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $LogPath)) {
    Write-Host ("等待文件出现: " + $LogPath) -ForegroundColor DarkGray
    Write-Host "（先在游戏里输 255 进 debug 模式，时间轴才会开始写）" -ForegroundColor DarkGray
    while (-not (Test-Path $LogPath)) {
        Start-Sleep -Milliseconds 500
    }
}

Clear-Host
Write-Host "=== LostCompound 战斗时间轴（跟读中）===" -ForegroundColor Cyan
Write-Host ("文件: " + $LogPath) -ForegroundColor DarkGray
Write-Host "按 Ctrl+C 退出" -ForegroundColor DarkGray
Write-Host ("-" * 70) -ForegroundColor DarkGray

$stream = $null
$lastLength = -1

while ($true) {
    try {
        $currentLength = (Get-Item $LogPath).Length

        # 文件被重写过（新一局）或第一次打开 -> 关掉旧的重新读
        if ($null -eq $stream -or $currentLength -lt $lastLength) {
            if ($null -ne $stream) { $stream.Dispose() }
            $stream = [System.IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
            $lastLength = 0
            Write-Host ""
            Write-Host "--- 检测到新的时间轴文件，从头显示 ---" -ForegroundColor Yellow
            $currentLength = $stream.Length
        }

        if ($currentLength -gt $lastLength) {
            $stream.Position = $lastLength
            $buffer = New-Object byte[] ($currentLength - $lastLength)
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -gt 0) {
                $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
                foreach ($line in $text -split "`r?`n") {
                    # 用码点判断，不写符号字面量 —— 见文件头的编码说明
                    if ($line -match [char]0x2500) {
                        Write-Host $line -ForegroundColor Cyan
                    } elseif ($line -match [char]0x2715) {
                        Write-Host $line -ForegroundColor Red
                    } elseif ($line -match [char]0x2192) {
                        Write-Host $line -ForegroundColor DarkGray
                    } elseif ($line.Trim().Length -gt 0) {
                        Write-Host $line
                    }
                }
            }
            $lastLength = $currentLength
        }
    }
    catch {
        # 文件被游戏独占的那一瞬间读会失败 —— 忽略，下一轮再试
    }

    Start-Sleep -Milliseconds 200
}
