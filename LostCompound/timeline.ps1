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
#     1. 先进 debug 模式（主菜单输 255）—— 时间轴会自动开始写 timeline.log
#     2. 双击 / 运行这个脚本，会弹出一个新窗口实时显示
#     3. 关掉游戏后这个窗口也不会自己关（方便回看最后几回合），按 Ctrl+C 退出
#
#  为什么不用 Get-Content -Wait：
#    它遇到「文件被重写（长度变短）」时会卡住不再输出 —— 而时间轴每次开新局
#    都是覆盖写，正好会踩到那个坑。所以这里自己轮询文件长度。
# ============================================================================

param(
    [string]$LogPath = (Join-Path $PSScriptRoot "LostCompound\bin\Debug\net10.0\timeline.log")
)

$ErrorActionPreference = "Stop"

# ---- 等文件出现（游戏可能还没进 debug 模式）----
if (-not (Test-Path $LogPath)) {
    Write-Host "等待 $LogPath 出现..." -ForegroundColor DarkGray
    Write-Host "（先在游戏里输 255 进 debug 模式，时间轴才会开始写）" -ForegroundColor DarkGray
    while (-not (Test-Path $LogPath)) {
        Start-Sleep -Milliseconds 500
    }
}

Clear-Host
Write-Host "=== LostCompound 战斗时间轴（跟读中）===" -ForegroundColor Cyan
Write-Host "文件：$LogPath" -ForegroundColor DarkGray
Write-Host "按 Ctrl+C 退出" -ForegroundColor DarkGray
Write-Host ("-" * 70) -ForegroundColor DarkGray

$reader = $null
$lastLength = -1

while ($true) {
    try {
        if ($null -eq $reader -or $lastLength -gt (Get-Item $LogPath).Length) {
            # 文件被重写过（新一局）或第一次打开 —— 关掉旧的重新读
            $reader?.Dispose()
            $reader = [System.IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
            $lastLength = 0
            Write-Host ""
            Write-Host "--- 检测到新的时间轴文件，从头显示 ---" -ForegroundColor Yellow
        }

        # 只读「上次之后新增的部分」：先看文件有没有变长
        $currentLength = $reader.Length
        if ($currentLength -gt $lastLength) {
            $reader.Position = $lastLength
            $buffer = New-Object byte[] ($currentLength - $lastLength)
            $read = $reader.Read($buffer, 0, $buffer.Length)
            if ($read -gt 0) {
                $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
                # 回合分隔线用亮色，其余用默认色，方便一眼看出回合边界
                foreach ($line in $text -split "`r?`n") {
                    if ($line -match '────────') {
                        Write-Host $line -ForegroundColor Cyan
                    } elseif ($line -match '✕') {
                        Write-Host $line -ForegroundColor Red
                    } elseif ($line -match '→') {
                        Write-Host $line -ForegroundColor DarkGray
                    } else {
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
