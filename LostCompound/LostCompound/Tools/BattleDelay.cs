namespace LostCompound.Tools
{
    /// <summary>
    /// 战斗日志的节奏控制 —— 让「刷得太快、看不清」的日志慢下来。
    ///
    /// 背景：这一块以前有一整套 `Calculator.Delay`，后来被整体删掉了
    /// （提交 `refactor(battle): 删除全部 Calculator.Delay 调用与方法本身`）。
    /// 删掉之后战斗日志一闪而过，用户反馈「算得太快」。
    /// 现在只加回**极少量、可一键关掉**的停顿，不做成满屏都 Delay。
    ///
    /// 用户裁定：**只在战斗关键节点停，每次 80ms**
    /// （回合分隔、怪物出手、死亡掉落；环境描述与菜单提示不停）。
    ///
    /// ⚠️ 三条纪律：
    ///   ① **默认关闭**（<see cref="Enabled"/> = false），由启动处统一打开 ——
    ///      这样单元/探针测试不受影响，也不会让所有历史行为默认变慢；
    ///   ② 输出被重定向（重定向到文件 / 管道）时**自动不睡**，否则自动化会白白慢；
    ///   ③ 任何异常都吞掉 —— 停顿是体验优化，不能因为它影响一局正在进行的战斗。
    /// </summary>
    public static class BattleDelay
    {
        /// <summary>关键节点停多久（毫秒）。用户裁定 80。</summary>
        public const int NodeDelayMs = 80;

        /// <summary>是否启用停顿。由 <see cref="Enable"/> / <see cref="Disable"/> 控制。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>打开停顿（游戏正常启动时调；探针测试不调）。</summary>
        public static void Enable() => Enabled = true;

        /// <summary>关掉停顿（测试 / 需要极速跑图时用）。</summary>
        public static void Disable() => Enabled = false;

        /// <summary>
        /// 在**战斗关键节点**停顿一下。没启用 / 输出被重定向 / 没有控制台时直接返回。
        /// </summary>
        public static void Tick(int milliseconds = NodeDelayMs)
        {
            if (!Enabled || milliseconds <= 0) return;

            try
            {
                // 输出被重定向时不要睡：自动化跑脚本时每一行都等 80ms 会非常难受，
                // 而且那种场景下玩家本来也看不见「一行一行出现」的效果。
                if (Console.IsOutputRedirected) return;

                Thread.Sleep(milliseconds);
            }
            catch
            {
                // 停顿失败（无控制台 / 被中断）不影响战斗
            }
        }
    }
}
