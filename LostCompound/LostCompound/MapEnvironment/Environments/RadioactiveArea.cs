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

        /// <summary>
        /// 玩家身上现在有没有盖革计数器（背包或饰品栏）。
        /// 由 <see cref="UpdateTracking"/> 在主线程算好、后台线程只读，
        /// 用途是决定状态栏**要不要把「几秒一次」写出来**（见 <see cref="DescribeStatus"/>）。
        /// 换层时环境对象整个重建，所以这个值不必存档。
        /// </summary>
        private bool _hasGeigerCounter;

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

        // ─────────────────────────── 开采（§5.6）───────────────────────────

        /// <summary>
        /// 这层能不能用镐子挖。镐子（<see cref="Items.Equipment.Pickaxe"/>）问的就是这个。
        ///
        /// 返回 true 的条件是「**脚下这间房有矿脉**」—— 见 <see cref="Rooms.Room.HasOreVein"/>。
        /// 设计文档 §5.6 写的是「最少 3 个房间可采」，所以可采性挂在房间上，
        /// 而不是「这一层整体可采」。
        ///
        /// ⚠️ 参数 `p` 是必需的，**不能**改用 `_player` 字段：
        ///   那个字段只在 OnScreenRefresh 里同步，环境对象刚建好、还没刷过一屏时是 null，
        ///   用它会让镐子在这一层「静默失效」（玩家只看到「这里没有可以开采的东西」）。
        /// </summary>
        public override bool CanMine(Player p) => p?.CurrentRoom?.HasOreVein == true;

        /// <summary>
        /// 挖完之后消耗掉脚下的矿脉。一间房只能挖一次。
        ///
        /// 为什么挖一次就没了：设计文档写的是「3 个房间可采 3-5 个矿物」，
        /// 是**房间数**限定总产出，挖完就换下一间 —— 不消耗的话一间房能刷到天荒地老。
        ///
        /// 同样从参数拿 Player（理由见 <see cref="CanMine"/>）：`Player.CurrentRoom` 只读，
        /// 但 `Room.HasOreVein` 可写，所以拿到房间之后直接改它。
        /// </summary>
        public override void ConsumeOreVein(Player p)
        {
            Rooms.Room room = p?.CurrentRoom;
            if (room != null) room.HasOreVein = false;
        }

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
                _hasGeigerCounter = HasGeigerCounter(p);

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

        /// <summary>
        /// 玩家身上有没有盖革计数器（背包或饰品栏都算）。
        ///
        /// 为什么背包也算：它的作用是「**测**辐射」，不一定要戴在身上才能读表 ——
        /// 文档只对「饰品栏」追加了额外的 ×1.25 效果（见 <see cref="UpdateTracking"/>），
        /// 但从没说过「放背包里就完全没用」。
        /// 状态栏的显示规则见 <see cref="DescribeStatus"/>。
        /// </summary>
        private static bool HasGeigerCounter(Player p)
        {
            foreach (Items.Item item in p.Inventory)
            {
                if (item != null && item.Id == Items.ItemIds.GeigerCounter) return true;
            }

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

                // 碘片挡下时不累计次数、不报「受到伤害」，只报「挡下了」——
                // 否则玩家会看到「受到 1 点伤害（剩余 100.0）」这种自相矛盾的播报。
                if (p.RadiationBlocked)
                {
                    _pendingInfo.Enqueue("辐射：碘片挡下了一次（" + IodineLeftText(p) + "）");
                    continue;
                }

                lock (_gate) _ticksTaken++;

                _pendingInfo.Enqueue(
                    "辐射：你受到 " + DamagePerTick.ToString("F0") + " 点伤害（剩余 "
                    + remainingHp.ToString("F1") + "）");
            }
        }

        /// <summary>播报里那句「还剩几片碘片」。</summary>
        private static string IodineLeftText(Player p)
        {
            int left = 0;
            foreach (Items.Item item in p.Inventory)
            {
                if (item is Items.Consumables.IodineTablet tablet) left += tablet.Durability;
            }

            return left > 0 ? "碘片还能挡 " + left + " 次" : "碘片用完了";
        }

        // ─────────────────────────── Boss 死亡（§7.4）───────────────────────────

        /// <summary>
        /// 本层 Boss 被打死 → 撤掉这一层的辐射状态（设计文档 §7.4：
        /// 「EnvRadioactive —— 击杀 Boss 后删除」）。
        ///
        /// 为什么直接把间隔置 0，而不是只删 buff：
        ///   这个环境的「辐射状态」就是 `_intervalSeconds`（后台线程每 100ms 读它决定扣不扣）。
        ///   buff 体系是另一条线（§7.4 的 NonEnvRadioactive 才用 buff），
        ///   这里删的是环境自己的秒表 —— 置 0 之后 ClockLoop 下一拍就会走「不辐射」分支。
        ///
        /// 为什么还要清 `_elapsedSeconds`：不清的话，玩家要是以后被放回这个环境
        /// （理论上不会，但留个干净的初始状态更省心），会带着上一段的进度复活。
        /// </summary>
        public override void OnBossDefeated(Player p)
        {
            lock (_gate)
            {
                _intervalSeconds = 0.0;
                _elapsedSeconds = 0.0;
                _bossDefeated = true;
            }

            _pendingInfo.Enqueue("辐射源已经沉寂——这一层不再有辐射。");
        }

        /// <summary>Boss 已经打过了（状态栏据此显示「已解除」而不是「这里没有辐射源」）。</summary>
        private bool _bossDefeated;

        // ─────────────────────────── 状态栏 / 存档 ───────────────────────────

        /// <summary>状态栏那一行：本房间的辐射间隔 + 已经挨了几次。</summary>
        public override string DescribeStatus()
        {
            double interval;
            int ticks;
            bool defeated;
            bool hasGeiger;
            lock (_gate)
            {
                interval = _intervalSeconds;
                ticks = _ticksTaken;
                defeated = _bossDefeated;
                hasGeiger = _hasGeigerCounter;
            }

            if (interval <= 0)
            {
                // Boss 打完之后间隔也会归 0，但含义不同 —— 别让玩家以为「跑到了安全房」。
                return defeated ? "辐射：Boss 已死，这一层不再有辐射" : "辐射：这里没有辐射源";
            }

            // Hyberty 2026-11-22：**没有盖革计数器就不显示扣血间隔**。
            //   理由：盖革计数器就是「测量辐射」的那件仪器 —— 手上没有仪器，
            //   玩家就不该凭空知道这里是 10 秒还是 25 秒一次（应该自己去摸索或靠挨打感受）。
            //   只要**身上有**计数器（背包或饰品栏；饰品栏会额外 ×1.25）就显示，
            //   因为「有仪器 = 读得出读数」。
            if (!hasGeiger)
            {
                return "辐射：这里有辐射";
            }

            return "辐射：" + interval.ToString("F1") + " 秒/次（已受 " + ticks + " 次）";
        }

        /// <summary>
        /// 只存「挨了几次」。每间房的间隔**不存** —— 它由地图种子推导出来，
        /// 读档时地图重建，值自然就回来了。存了反而会在旧档里变成 0。
        /// </summary>
        public override Dictionary<string, int> SaveState() => new()
        {
            ["radiationTicks"] = _ticksTaken,
            // Boss 打没打过要存：读档回到第 10 层时，地图是按种子重建的，
            // 但「这一层的 Boss 已经死了」这种进度不在种子里的。
            ["radiationBossDefeated"] = _bossDefeated ? 1 : 0,
        };

        public override void LoadState(IReadOnlyDictionary<string, int> state)
        {
            if (state == null) return;
            if (state.TryGetValue("radiationTicks", out int ticks)) _ticksTaken = ticks;
            if (state.TryGetValue("radiationBossDefeated", out int defeated) && defeated != 0)
            {
                _bossDefeated = true;
            }
        }
    }
}
