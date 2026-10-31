using LostCompound.Maps;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Save
{
    /// <summary>
    /// 「当前这一局」的静态把手 —— **给窗口关闭时的自动存档用**。
    ///
    /// 为什么需要它：进程被关掉（点 × / Ctrl+C / 注销）时，操作系统只会回调一个
    /// **静态的**控制台事件处理函数（见 <see cref="ExitSaveHook"/>），它拿不到任何参数，
    /// 更拿不到主循环里的局部变量。所以必须有一份「当前对局在哪」的全局引用。
    ///
    /// ⚠️ 这不是「把状态全局化」的借口 —— 它**只持有引用**，不改任何状态，
    /// 且只在 <see cref="Tools.Players.Player.RunMainLoop"/> 的生命周期内有效
    /// （进对局 Begin、出对局 End）。
    ///
    /// ⚠️ 为什么还要 `MarkSafe`：
    ///   战斗中/房间事件中途被关掉时，存下来的是一份「没有敌人、但玩家已经掉过血」的残缺局面，
    ///   读档会回到那个房间、事件却不会再触发。所以只在**方向菜单等待输入**那个瞬间允许存。
    /// </summary>
    public static class SaveSession
    {
        private static Player _player;
        private static Room[][] _map;
        private static TreeMazeGenerator _tmg;

        /// <summary>当前是否处在「可以安全存档」的瞬间（方向菜单等输入时）。</summary>
        private static volatile bool _safe;

        /// <summary>0 = 空闲，1 = 正在存。用 Interlocked 防重入（Ctrl+C 可能连按两下）。</summary>
        private static int _saving;

        /// <summary>
        /// **最近一个安全点**的快照（见 <see cref="MarkSafe"/>）。
        ///
        /// 为什么需要它：战斗里 / 菜单里按 × 时，当前局面是恢复不了的（敌人列表、回合数、
        /// 半截的房间事件都没有存档位），但玩家的进度不能因此整段丢掉。
        /// 所以在每个安全点顺手拍一张快照；退出时若不是安全点，就存这张**最近的**快照 ——
        /// 语义是「回到本回合开始时」，比「什么都没存」好得多。
        ///
        /// 代价：每个回合多一次 1 KB 的对象分配。换来的是一条永远不会丢整局的退出路径。
        /// </summary>
        private static SaveData _lastSafeSnapshot;

        /// <summary>进入对局：登记当前玩家 / 地图 / 生成器。</summary>
        public static void Begin(Player player, Room[][] map, TreeMazeGenerator tmg)
        {
            _player = player;
            _map = map;
            _tmg = tmg;
            _safe = false;
            _lastSafeSnapshot = null;   // 上一局的快照不能带到这一局（地图/种子都不一样）
        }

        /// <summary>离开对局：清掉引用，避免退出钩子拿一局已经结束的游戏去存档。</summary>
        public static void End()
        {
            _player = null;
            _map = null;
            _tmg = null;
            _safe = false;
            _lastSafeSnapshot = null;
        }

        /// <summary>
        /// 标记「现在可以安全存档」。只在主循环等待玩家输入方向的那一刻为 true。
        /// 置 true 的同时顺手拍一张快照，供「非安全点退出」时使用。
        /// </summary>
        public static void MarkSafe(bool safe)
        {
            _safe = safe;
            if (safe)
            {
                CaptureSnapshot();
            }
        }

        /// <summary>拍一张当前局面快照。**任何异常都吞掉** —— 它跑在正常游戏流程里，不能因为存档失败打断玩家。</summary>
        private static void CaptureSnapshot()
        {
            try
            {
                if (_player == null || _map == null || _tmg == null || !_player.IsAlive) return;
                _lastSafeSnapshot = SaveCollector.Capture(_player, _map, _tmg);
            }
            catch
            {
                // 快照失败只是「这次退出少一层保险」，绝不该影响正在玩的对局
            }
        }

        /// <summary>
        /// 退出时存档（尽力而为）。不满足条件就安静地什么都不做 —— 这是退出路径，
        /// 不能因为保存失败而弹一堆话，更不能抛异常。
        /// </summary>
        /// <param name="reason">给玩家看的触发原因（"窗口被关闭" / "Ctrl+C" / "进程退出"）</param>
        public static void TrySaveOnExit(string reason)
        {
            // ⚠️ 整个方法必须兜住异常：它可能在**控制台控制回调**里执行，
            // 那里抛出去是未定义行为（进程可能直接崩个错误框出来，而玩家只是点了关闭）。
            try
            {
                // 先把引用抓到局部变量：回调可能跑在别的线程上，
                // 万一此时主线程正在 End()，局部快照保证我们不会拿着半空的对象去用。
                Player player = _player;
                Room[][] map = _map;
                TreeMazeGenerator tmg = _tmg;

                if (player == null || map == null || tmg == null) return;   // 不在对局里
                if (!player.IsAlive) return;                               // 死人不存档

                // 在安全点 → 现拍一张（最新）；不在安全点 → 存最近一次安全点的快照。
                // 后者是「回到本回合开始」的语义：战斗里按 × 不会再丢掉整局进度。
                //
                // ⚠️ 顺序：**先取数据、再上防重入锁**。反过来写的话，
                // 「没有快照」这条早退路径会把 _saving 永久留在 1，
                // 之后任何一次 TrySaveOnExit 都会被当成「正在存」而直接返回
                // （探针 probe_exitsave 抓到过：第一次无快照 → 第二次再也存不出来）。
                bool fresh = _safe;
                SaveData data = fresh ? SaveCollector.Capture(player, map, tmg) : _lastSafeSnapshot;
                if (data == null) return;   // 从来没到过安全点（例如刚开局就关窗口）

                // 连按 Ctrl+C 会触发两次回调，第二次直接放弃
                if (Interlocked.Exchange(ref _saving, 1) == 1) return;

                if (SaveFile.Save(data))
                {
                    Console.WriteLine(fresh
                        ? $"[存档] 检测到{reason}，已自动保存本局进度。"
                        : $"[存档] 检测到{reason}，已保存到**最近的安全点**（本回合开始时）。");
                }
            }
            catch
            {
                // 退出路径上不许再抛：丢一次自动存档远好过留一个崩溃框
            }
        }
    }
}
