using System.Collections.Concurrent;
using System.Threading;
using LostCompound.Tools;
using LostCompound.Tools.Players;
using Environment = LostCompound.MapEnvironment.Environment;

namespace LostCompound.MapEnvironment.Environments
{
    /// <summary>
    /// 放射性区域（设计文档 §5.6 + 辐射系统 §7.4）。
    ///
    /// ## 玩法
    /// 站在辐射区里**每过 N 秒掉 1 血**，N 是这间房自己的值（5~30，柏林噪声铺出来的，
    /// 见 <see cref="Rooms.Room.RadiationSeconds"/>）。相邻房间的 N 接近，所以地图上能
    /// 看出「毒气带」和「安全区」，玩家可以靠走位绕开最毒的一片。
    ///
    /// ## Hyberty 2026-11-22 定下的四条口径
    ///   1. **换房清零**：计时量各房不同，走进新房就从 0 起算（不能"续着上一间的进度"，
    ///      否则高速穿过一间 5 秒房会白送一次扣血）。
    ///   2. **空房间不暂停**：符合现实 —— 辐射不因为你站着不动就等你。
    ///   3. **战斗中不暂停**：同上，战斗里也在挨辐。
    ///   4. 播报写在**信息框**里（不是战斗日志），因为它是"环境正在发生的事"，
    ///      而不是"这一回合你干了什么"。
    ///
    /// ## 为什么是后台线程
    /// 秒不能靠回合推进：回合是被玩家按出来的，卡在菜单上发呆就永远不会到时间。
    /// 所以这里起一条线程自己数秒 —— 但**它绝不往控制台写**（见下面 ⚠️）。
    ///
    /// ⚠️ 线程安全的三条纪律：
    ///   · 线程只做「累加时间 + 到点扣血 + 往队列塞一句播报」，不碰控制台、不碰盾；
    ///   · 主线程在 <see cref="OnScreenRefresh"/> 里把队列排空、画进信息框；
    ///   · 共享字段全部在 `_gate` 锁里读写（`_running` 用 volatile）。
    /// </summary>
    public sealed class RadioactiveArea : Environment
    {
        // ── 数值（改这里就够，别散到调用点）──

        /// <summary>噪声值 0 → 每 5 秒扣 1 血（与生成器的下限必须一致）。</summary>
        public const double MinSeconds = 5.0;

        /// <summary>噪声值 1 → 每 30 秒扣 1 血。</summary>
        public const double MaxSeconds = 30.0;

        /// <summary>每次辐射扣多少血。</summary>
        public const double DamagePerTick = 1.0;

        /// <summary>
        /// 盖革计数器的乘数：**间隔 ×1.25**（Hyberty 2026-11-22 把原先的「+5 秒」改成了这个）。
        ///
        /// 为什么是乘不是加：乘在区间两端都给同样的体感（5→6.25、30→37.5），
        /// 而 +5 会让最毒的房间（5 秒）直接翻倍、最安全的房间几乎没变 ——
        /// 那等于逼玩家只在毒气带里才想起戴它。
        ///
        /// 多个只乘一次（设计文档 §7.4「不可堆叠」）。
        /// </summary>
        public const double GeigerMultiplier = 1.25;

        // ── 线程参数 ──

        /// <summary>线程的轮询间隔。100ms 足够精确（误差 ≤0.1 秒），又几乎不吃 CPU。</summary>
        private const int PollIntervalMs = 100;

        // ── 状态 ──

        /// <summary>所有跨线程共享的字段都由它保护。</summary>
        private readonly object _gate = new();

        /// <summary>后台攒下的播报，等主线程来取。</summary>
        private readonly ConcurrentQueue<string> _pendingInfo = new();

        private Thread _clock;
        private volatile bool _running;
        private Player _player;

        /// <summary>当前房间的辐射间隔（秒，已乘过盖革）。0 = 这一格不辐射。</summary>
        private double _intervalSeconds;

        /// <summary>距离下一次扣血已经攒了多少秒。</summary>
        private double _elapsedSeconds;

        /// <summary>当前所在的格子，用来判定「换房了 → 清零」。</summary>
        private int _roomRow = -1;
        private int _roomCol = -1;

        /// <summary>本层累计挨了几次辐射（存档要用的那一个数）。</summary>
        private int _ticksTaken;

        public RadioactiveArea() : base(EnvType.RadioactiveArea) { }

        /// <summary>
        /// 回合推进：**故意什么都不做**。
        ///
        /// 这个环境的时间轴是**秒**，不是回合 —— 回合是玩家按出来的，
        /// 卡在菜单上发呆就永远不走回合；而辐射的现实语义是「站着也在挨」。
        /// 所以计时全部交给后台线程（见 <see cref="ClockLoop"/>），
        /// 这里留空不是忘了写，是这条环境本来就没有「每回合推进一次」的状态。
        /// </summary>
        public override void OnBattleTurn(bool hotRoom) { }

        // ─────────────────────────── 主线程入口 ───────────────────────────

        /// <summary>
        /// 每刷新一屏调用一次：启动/更新计时、把后台的播报画到信息框。
        /// 见 <see cref="Environment.OnScreenRefresh"/>（一定在主线程）。
        /// </summary>
        public override void OnScreenRefresh(Player p)
        {
            if (p == null) return;

            UpdateTracking(p);
            EnsureThreadRunning(p);
            DrainPendingInfo(p);
        }

        /// <summary>把「玩家现在站在哪、这间房多少秒」同步给后台线程。</summary>
        private void UpdateTracking(Player p)
        {
            Rooms.Room room = p.CurrentRoom;

            double seconds = room?.RadiationSeconds ?? 0.0;
            if (seconds > 0)
            {
                // 盖革计数器：戴了就乘 1.25（只乘一次，不可堆叠）
                if (HasGeigerCounter(p)) seconds *= GeigerMultiplier;
                if (seconds < MinSeconds) seconds = MinSeconds;
            }

            lock (_gate)
            {
                _player = p;

                // 换房清零（Hyberty 第 1 条）：计时量各房不同，不能把上一间的进度带过来。
                // 只在**真的换了格子**时清 —— OnScreenRefresh 一屏会调好几次，
                // 每次都清的话永远攒不满一次扣血。
                int row = p.PlayerRow;
                int col = p.PlayerCol;
                if (row != _roomRow || col != _roomCol)
                {
                    _roomRow = row;
                    _roomCol = col;
                    _elapsedSeconds = 0.0;
                }

                _intervalSeconds = seconds;
            }
        }

        /// <summary>玩家饰品栏里有没有盖革计数器。</summary>
        private static bool HasGeigerCounter(Player p)
        {
            foreach (Items.Item item in p.Accessories)
            {
                if (item != null && item.Id == Items.ItemIds.GeigerCounter) return true;
            }
            return false;
        }

        /// <summary>第一次调用时起线程；已经起来就什么都不做。</summary>
        private void EnsureThreadRunning(Player p)
        {
            if (_running) return;

            _running = true;
            _clock = new Thread(ClockLoop)
            {
                IsBackground = true,   // 进程退出时不被它拖住
                Name = "RadioactiveClock",
            };
            _clock.Start();
        }

        /// <summary>把后台排队的播报画进信息框（主线程）。</summary>
        private void DrainPendingInfo(Player p)
        {
            while (_pendingInfo.TryDequeue(out string line))
            {
                p.NotifyInfo(line);
            }
        }

        // ─────────────────────────── 后台线程 ───────────────────────────

        /// <summary>
        /// 计时循环。**只做三件事**：睡觉、累加时间、到点扣血并排队播报。
        /// 绝不碰控制台、绝不碰护盾、绝不动装备回调（见类注释的 ⚠️）。
        /// </summary>
        private void ClockLoop()
        {
            double sliceSeconds = PollIntervalMs / 1000.0;

            while (_running)
            {
                Thread.Sleep(PollIntervalMs);

                Player p;
                double interval;
                lock (_gate)
                {
                    p = _player;
                    interval = _intervalSeconds;

                    // 间隔 0 = 这个格子不辐射（例如还没进图 / 站到空地）
                    if (p == null || interval <= 0)
                    {
                        continue;
                    }

                    _elapsedSeconds += sliceSeconds;
                    if (_elapsedSeconds < interval) continue;

                    // 攒够了：扣一次。用减法而不是清零，避免长时间卡顿后少算；
                    // 但这意味着如果主线程被挂住很久，会连扣好几次 —— 那是对的（辐射不等你）。
                    _elapsedSeconds -= interval;
                }

                double remainingHp = p.ApplyRadiationDamage(DamagePerTick);

                lock (_gate) _ticksTaken++;

                _pendingInfo.Enqueue(
                    "辐射：你受到 " + DamagePerTick.ToString("F0") + " 点伤害（剩余 "
                    + remainingHp.ToString("F1") + "）");
            }
        }

        // ─────────────────────────── 状态栏 / 存档 ───────────────────────────

        /// <summary>状态栏那一行：本房间的辐射间隔 + 已经挨了几次。</summary>
        public override string DescribeStatus()
        {
            double interval;
            int ticks;
            lock (_gate)
            {
                interval = _intervalSeconds;
                ticks = _ticksTaken;
            }

            if (interval <= 0) return "辐射：这里没有辐射源";
            return "辐射：" + interval.ToString("F1") + " 秒/次（已受 " + ticks + " 次）";
        }

        /// <summary>
        /// 只存「挨了几次」。每间房的间隔**不存** —— 它由地图种子推导出来，
        /// 读档时地图重建，值自然就回来了。存了反而会在旧档里变成 0。
        /// </summary>
        public override Dictionary<string, int> SaveState() => new()
        {
            ["radiationTicks"] = _ticksTaken,
        };

        public override void LoadState(IReadOnlyDictionary<string, int> state)
        {
            if (state == null) return;
            if (state.TryGetValue("radiationTicks", out int ticks)) _ticksTaken = ticks;
        }
    }
}
