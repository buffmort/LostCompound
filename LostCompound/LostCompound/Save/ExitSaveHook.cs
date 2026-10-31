using System.Runtime.InteropServices;

namespace LostCompound.Save
{
    /// <summary>
    /// 把「窗口被关掉 / Ctrl+C / 注销 / 关机」这几种退出方式接到自动存档上。
    ///
    /// ─────────────────────────────────────────────────────────────
    /// 【为什么不能只用托管事件】
    ///
    /// `Console.CancelKeyPress` 只覆盖 **Ctrl+C / Ctrl+Break**。
    /// 点窗口右上角的 × 发的是 **CTRL_CLOSE_EVENT**，.NET 没有对应的托管事件 ——
    /// 那一条只能靠 Win32 的 `SetConsoleCtrlHandler` 注册回调。
    ///
    /// `AppDomain.CurrentDomain.ProcessExit` 在「Main 正常返回」时可靠，
    /// 但它对 × / 关机**不保证**（系统可能直接杀进程）。所以两条都挂上：
    /// 控制台回调负责异常终止，ProcessExit 作为正常退出的兜底。
    ///
    /// 【时间预算】CTRL_CLOSE_EVENT 之后系统只给大约 **5 秒**，超时就强杀。
    /// 所以回调里**只能做一件事**：存盘。不要弹确认框、不要等输入、不要做耗时清理。
    /// 本项目的存档只有 1 KB 左右，够用。
    ///
    /// 【返回值】回调一律返回 `false` = 「我没处理，交给默认行为」。
    /// 返回 true 会把系统默认的终止动作也吞掉，那样点 × 反而关不掉窗口。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public static class ExitSaveHook
    {
        // SetConsoleCtrlHandler 的 ctrlType 取值（来自 WinCon.h）
        private const uint CtrlCEvent = 0;
        private const uint CtrlBreakEvent = 1;
        private const uint CtrlCloseEvent = 2;
        private const uint CtrlLogoffEvent = 5;
        private const uint CtrlShutdownEvent = 6;

        /// <summary>回调签名。**必须与 kernel32 的 PHANDLER_ROUTINE 完全一致**。</summary>
        private delegate bool HandlerRoutine(uint ctrlType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

        /// <summary>
        /// ⚠️ 必须用静态字段**保活**这个委托。
        /// 直接把 lambda 传给 SetConsoleCtrlHandler，GC 之后系统手里就是一个野指针,
        /// 回调触发时程序会直接崩（而且只在"关窗口"这种路径上复现，极难查）。
        /// </summary>
        private static readonly HandlerRoutine Handler = OnConsoleControl;

        /// <summary>
        /// 安装钩子。在 Main 里、进主菜单之前调用一次即可（重复调用是幂等的）。
        /// 非 Windows 平台直接跳过：那些平台没有控制台控制事件，硬调 P/Invoke 会找不到 DLL。
        /// </summary>
        public static void Install()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                SetConsoleCtrlHandler(Handler, true);
            }
            catch
            {
                // 注册失败不影响游戏本身：只是「关窗口自动存档」这个便利功能不可用
            }

            // Ctrl+C / Ctrl+Break 再挂一道托管事件。
            // 与上面的 Win32 回调**重复覆盖**是有意的：控制台回调的触发顺序在不同宿主
            // （conhost / Windows Terminal）里并不完全一致，两条都挂最稳。
            // 重复触发不会存两次 —— SaveSession 里有 Interlocked 防重入。
            Console.CancelKeyPress += (_, _) => SaveSession.TrySaveOnExit("按下 Ctrl+C");

            // 正常退出（Main 返回）的兜底。此时 SaveSession 往往已经 End()，
            // TrySaveOnExit 会安静地什么都不做 —— 这是预期的。
            AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveSession.TrySaveOnExit("进程退出");
        }

        private static bool OnConsoleControl(uint ctrlType)
        {
            string reason = ctrlType switch
            {
                CtrlCEvent => "按下 Ctrl+C",
                CtrlBreakEvent => "按下 Ctrl+Break",
                CtrlCloseEvent => "窗口被关闭",
                CtrlLogoffEvent => "用户注销",
                CtrlShutdownEvent => "系统关机",
                _ => "进程终止",
            };

            SaveSession.TrySaveOnExit(reason);

            // 返回 false：让系统继续走默认流程（该终止就终止）。
            // 返回 true 会吞掉默认行为 —— 那样点 × 就关不掉窗口了。
            return false;
        }
    }
}
