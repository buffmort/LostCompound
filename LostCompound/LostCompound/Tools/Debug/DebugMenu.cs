// 与 Tools.cs 同样的别名：不加这行时 Environment 会同时命中
// System.Environment（隐式 using）与 LostCompound.MapEnvironment.Environment，产生 CS0104。
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Buffs;   // BuffFactory / Buff：debug 130 的按 ID 挂 buff
using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Maps;    // TreeMazeGenerator：调试战斗也要一张真地图来撑版面
using LostCompound.Rooms;
using LostCompound.Monsters.Normal;
using LostCompound.Monsters.Boss;
using LostCompound.Rooms.Types;
using LostCompound.Tools;

using LostCompound.Tools.Players;

namespace LostCompound.Tools.Debug
{
    /// <summary>
    /// 调试菜单。只在主菜单输入 255 后可用。
    /// 目前提供三件事：按序号挑怪物 + 地形开一场战斗；按 ID 定量发放道具；按 ID 给玩家挂 buff。
    ///
    /// 战斗中的「发道具 / 挂 buff」还有第二个入口（debug 模式的 129 / 130），
    /// 那两个入口把正在战斗的玩家直接传进来，逻辑都收在本类里，不在 Player 里另写一份。
    ///
    /// 怪物和地形都做成可选，所以不用改代码就能复现任意一场战斗。
    ///
    /// 环境既影响战斗、也影响玩家的道具（水瓶回水、硫酸开采、镐子采掘），
    /// 所以开打前会给玩家挂一张 1×1 的假地图（见 <see cref="AttachDebugMap"/>），
    /// 让两条路读到的是同一个环境对象。
    /// </summary>
    public static class DebugMenu
    {
        /// <summary>
        /// 常驻调试玩家。
        ///
        /// 为什么需要它：主菜单的「获取道具」要有人接道具，而主菜单本身没有玩家对象。
        /// 用一个常驻的调试玩家承接，在主菜单发的道具就能跟着他进下一场「调试战斗」，
        /// 而不是「发进一个随即被丢弃的临时玩家」（那样的发放毫无意义）。
        /// 注意它**不影响**「开始游戏」：StartNewRun 每次都自己 new Player。
        /// </summary>
        private static Player _debugPlayer;

        /// <summary>
        /// 怪物清单：显示名 + 造怪方式。
        ///
        /// 凡继承 <see cref="Monster"/> 且非抽象的都是候选，见 Monsters 目录：
        /// Zombie / Skeleton / Dragon / LavaSlime / Dummy / TwistedPairP1 / TwistedPairP2。
        ///
        /// 双子（P1/P2）做成**一个选项、两个一起上**，理由：
        ///   P2 的构造器就要求传 P1（共享 TwistedPairState），它的技能③会给「兄弟」加盾、
        ///   技能④的额外行动判定看的是「P1 本回合有没有攻击」——
        ///   单独放一只 P2 进去，它的兄弟不在场上，技能④会永远判定为「P1 未行动」而无限多动一次，
        ///   技能③也会给一个根本不参战的幽灵兄弟加盾。那是测不出真实行为的假象，不是调试。
        /// </summary>
        private static readonly (string Label, Func<List<Monster>> Build)[] MonsterCatalog =
        [
            ("僵尸", () => [new Zombie()]),
            ("骷髅", () => [new Skeleton()]),
            ("龙", () => [new Dragon()]),
            ("岩浆怪", () => [new LavaSlime()]),
            ("测试假人", () => [new Dummy()]),
            ("作孽双子（P1+P2 一起）", () =>
            {
                TwistedPairState state = new TwistedPairState();
                TwistedPairP1 p1 = new TwistedPairP1(state);
                return [p1, new TwistedPairP2(state, p1)];
            }),
        ];

        /// <summary>
        /// 调试战斗：选怪物 → 选地形 → 开打 → 回到主菜单。
        /// 任一步取消（空行 / 输入流结束）都会直接返回主菜单，不会开一场空战斗。
        /// </summary>
        public static void RunDebugBattle()
        {
            List<Monster> monsters = SelectMonsters();
            if (monsters == null)
            {
                Console.WriteLine("已取消。\n");
                return;
            }

            Environment env = SelectEnvironment();
            if (env == null)
            {
                Console.WriteLine("已取消。\n");
                return;
            }

            // 注意：RunBattle 的第三个参数是 Environment 对象，不是 EnvType，
            // 所以上面必须先过一遍 EnvironmentFactory.Create。
            Player p = DebugPlayer();

            // ⚠️ 必须让玩家"站在地图上"，否则环境只生效一半：
            //   RunBattle(env) 走的是传进去的 env 对象 → 掉水 / 涨水位 / STR 惩罚都正常；
            //   但道具侧（水瓶、硫酸、镐子）用的是 p.CurrentEnvironment，
            //   而它是从 _map[PlayerRow][PlayerCol].Environment 读的 —— 没有地图就恒为 null，
            //   于是「水瓶说这里没有水源」「硫酸说不能饮用」「镐子说没东西可挖」。
            //
            // 现在直接用一张**真地图**（10×10），而不是原来那张 1×1 的假图：
            //   ① 照样满足 CurrentEnvironment；
            //   ② 更重要的：分栏界面的尺寸是按地图文本算的，1×1 的地图会把框压成一条缝。
            //
            // 为什么这么做是安全的：JoinMap 只调用 StartRoom.MarkArrived()，
            // 不会触发 OnEnter —— 所以不会弹「初始物品选择」、不会重复发初始道具。
            // 层数用当前值（调试不推进度）：这样在调试菜单里也能看到「第 N 层的盾兵是 N×10 血」。
            TreeMazeGenerator tmg = new(10, 10, env.Type, Random.Shared.NextInt64(), GameProgress.Layer);
            tmg.Generate();
            Room[][] map = tmg.Grid;
            p.JoinMap(map);

            // 装上分栏界面再打：这样敌人框 / 状态栏 / Buff 框 / 日志区与正局完全一致
            // （以前调试战斗直接往控制台打，框相关的排版问题在调试里根本看不见）。
            p.RunDebugBattleOnScreen(map, tmg, monsters, env);
        }

        /// <summary>
        /// 主菜单入口的「获取道具」：主菜单没有玩家，道具发到常驻调试玩家身上
        /// （见 <see cref="_debugPlayer"/> 的说明）。
        ///
        /// 战斗中还有第二个入口：debug 模式下 <see cref="Player.OnPlayerTurn"/> 的 129，
        /// 那个入口直接把正在战斗的玩家传进来，道具当场到手。
        /// </summary>
        public static void GiveItems() => GiveItems(DebugPlayer());

        /// <summary>
        /// 按 ID 定量发放道具给指定玩家。
        ///
        /// 为什么收一个 <see cref="Player"/> 参数而不是自己凭空造一个：
        /// 「给谁发」是这个方法的唯一语义，只有调用方知道当前那个玩家是谁
        /// （战斗中就是正在战斗的那个，主菜单就是常驻调试玩家）。
        /// </summary>
        public static void GiveItems(Player player)
        {
            if (player == null) return;

            // ── 0. 先选 ID 区间（分页）──
            // 为什么不能一次列完：道具已经有 45 个，一次打印 45 行会把日志区刷爆
            // （日志区只有几行，玩家连「请输入道具 ID」这句提示都被顶上去看不到了）。
            // 用户要求：先选 1-10 / 11-20 / 21-30 … 再在里面选 ID。
            const int PageSize = 10;

            int maxId = 0;
            foreach ((int Id, string Name, Func<int, Item> Create) entry in ItemFactory.All)
            {
                if (entry.Id > maxId) maxId = entry.Id;
            }
            int pageCount = Math.Max(1, (maxId + PageSize - 1) / PageSize);

            Console.WriteLine("------------ 获取道具 ------------");

            // page = 0 表示「全部」（保留入口，但明确警告会刷屏）
            int page;
            while (true)
            {
                Console.WriteLine("选择 ID 区间：");
                for (int p = 1; p <= pageCount; p++)
                {
                    Console.WriteLine("  " + p + ". " + ((p - 1) * PageSize + 1) + "-" + (p * PageSize));
                }
                Console.WriteLine("  " + (pageCount + 1) + ". 全部（会刷满日志）");
                Console.WriteLine("  0. 取消");

                int? input = ReadIntOrCancel("请输入区间编号: ");
                if (input == null || input.Value == 0)
                {
                    Console.WriteLine("已取消。\n");
                    return;
                }
                if (input.Value == pageCount + 1)
                {
                    page = 0;          // 全部
                    break;
                }
                if (input.Value >= 1 && input.Value <= pageCount)
                {
                    page = input.Value;
                    break;
                }
                Console.WriteLine("没有这个区间。");
            }

            int low = page == 0 ? int.MinValue : (page - 1) * PageSize + 1;
            int high = page == 0 ? int.MaxValue : page * PageSize;

            // ── 1. 选道具：一页内用**字母**选，n/p 翻页，数字仍可直接填 ID ──
            // 为什么要字母：ID 是稀疏的（1..45 里有不少空号），而且同一个数字既像「编号」
            // 又像「数量」，玩家容易敲混。字母只在本页有效、连续排，屏幕上「a. 硫磺」
            // 和要敲的键一一对应。
            //
            // 为什么不要求「先选页、再看」：原先第一页看完发现没有，得退出去重来一遍。
            // 现在页是循环的：n 下一页、p 上一页（首尾相接），只有选中字母才真正结束。
            int? id = null;
            while (id == null)
            {
                List<(int Id, string Name)> pageItems = [];
                foreach ((int Id, string Name, Func<int, Item> Create) entry in ItemFactory.All)
                {
                    if (entry.Id < low || entry.Id > high) continue;
                    pageItems.Add((entry.Id, entry.Name));
                }

                if (pageItems.Count == 0)
                {
                    Console.WriteLine("这个区间里没有道具。\n");
                    return;
                }

                Console.WriteLine(page == 0
                    ? "------------ 全部道具 ------------"
                    : "------------ 第 " + page + "/" + pageCount + " 页（ID " + low + "-" + high + "）------------");

                for (int i = 0; i < pageItems.Count; i++)
                {
                    char key = (char)('a' + i);   // 一页最多 PageSize(10) 项，字母够用
                    Console.WriteLine("  " + key + ". " + pageItems[i].Name + "  (ID " + pageItems[i].Id + ")");
                }
                Console.WriteLine("  n=下一页  p=上一页  数字=直接填 ID  0=取消");
                Console.WriteLine("----------------------------------");

                Console.WriteLine("请选择（字母 / n / p / 数字）:");
                string line = Console.ReadLine();
                if (line == null || line.Trim().Length == 0)
                {
                    Console.WriteLine("已取消。\n");
                    return;
                }

                string key2 = line.Trim().ToLowerInvariant();
                if (key2 == "0")
                {
                    Console.WriteLine("已取消。\n");
                    return;
                }
                if (key2 == "n")
                {
                    if (page != 0)   // 「全部」没有页可翻
                    {
                        page = page % pageCount + 1;
                        low = (page - 1) * PageSize + 1;
                        high = page * PageSize;
                    }
                    continue;
                }
                if (key2 == "p")
                {
                    if (page != 0)
                    {
                        page = (page - 2 + pageCount) % pageCount + 1;
                        low = (page - 1) * PageSize + 1;
                        high = page * PageSize;
                    }
                    continue;
                }
                if (key2.Length == 1 && key2[0] >= 'a' && key2[0] <= 'z')
                {
                    int index = key2[0] - 'a';
                    if (index < pageItems.Count)
                    {
                        id = pageItems[index].Id;
                        continue;
                    }
                    Console.WriteLine("这一页没有这个字母。");
                    continue;
                }
                if (int.TryParse(key2, out int typed))
                {
                    if (ItemFactory.IsKnown(typed))
                    {
                        id = typed;
                        continue;
                    }
                    if (typed >= 1 && typed <= pageCount)   // 当成「跳到第几页」
                    {
                        page = typed;
                        low = (page - 1) * PageSize + 1;
                        high = page * PageSize;
                        continue;
                    }
                    Console.WriteLine("没有这个 ID。");
                    continue;
                }

                Console.WriteLine("输入无法识别，请输入字母 / n / p / 数字。");
            }

            // 走到这里 id 一定有值且已确认存在（字母只来自上面的 pageItems，数字分支也查过 IsKnown），
            // 所以原先那句 IsKnown 兜底已经冗余 —— 删掉，避免下一个人以为还有第二条失败路径。

            // ── 3. 定数量：<= 0 视为取消 ──
            int? count = ReadIntOrCancel("请输入数量（空行取消）: ");
            if (count == null || count.Value <= 0)
            {
                Console.WriteLine("已取消。\n");
                return;
            }

            // ── 4. 造出来塞进背包 ──
            // 注意 AddItem 自己会播报「你获得了 …」并按堆叠上限/背包容量处理，
            // 所以这里不再重复打印，也不自己夹取数量。
            Item item = ItemFactory.Create(id.Value, count.Value);
            if (item == null)
            {
                // 理论上到不了（上面 IsKnown 已经查过），留着防御「表里有 ID 但工厂返回 null」的将来改动
                Console.WriteLine("这个道具无法创建。\n");
                return;
            }
            player.AddItem(item);
            Console.WriteLine();
        }

        /// <summary>
        /// 按 ID 给指定玩家挂一个 buff（debug 指令 130）。
        ///
        /// 为什么收一个 <see cref="Player"/> 参数而不是自己凭空造一个：
        /// 与 <see cref="GiveItems(Player)"/> 同理 ——「给谁挂」是这个方法的唯一语义，
        /// 只有调用方知道当前那个玩家是谁（战斗中就是正在战斗的那个）。
        ///
        /// 交互顺序与 129 一致：列清单 → 选 ID → 收参数 → 生效。
        /// 区别只有一处：**选 ID 这一步未登记时会重问**（129 是直接返回）。
        /// 理由：buff 清单只有个位数行，手上多半就是照着清单敲的，
        /// 敲错一个数字就把整条指令退出去、要重新从菜单走一遍，不好用。
        ///
        /// 「剩余回合」这一步可以输入 <see cref="Buff.Permanent"/>（-1）表示**永久**
        /// —— 值直接复用 Buff 的哨兵，不在这里另定一套暗号，免得两边对不上。
        /// </summary>
        public static void AddBuffTo(Player player)
        {
            if (player == null) return;

            // ── 1. 列出所有 buff：ID + 名称，按 ID 升序 ──
            // 序号直接用 ID（与 129 同一口径）：多一套 0,1,2… 只会让「选第 3 个」和「ID 3」对不上。
            Console.WriteLine("------------ 添加 Buff ------------");
            foreach ((int Id, string Name, Func<int, int, Buff> Create) entry in BuffFactory.All)
            {
                Console.WriteLine("  " + entry.Id + ". " + entry.Name);
            }
            Console.WriteLine("----------------------------------");

            // ── 2. 选 ID：未登记就重问（空行 / 输入流结束仍然取消）──
            int id;
            while (true)
            {
                int? choice = ReadIntOrCancel("请输入 Buff ID（空行取消）: ");
                if (choice == null)
                {
                    Console.WriteLine("已取消。\n");
                    return;
                }
                if (BuffFactory.IsKnown(choice.Value))
                {
                    id = choice.Value;
                    break;
                }
                Console.WriteLine("没有这个 Buff ID。");
            }

            // ── 3. 效果量：<= 0 视为取消（与 129 的数量同一口径）──
            int? stacks = ReadIntOrCancel("请输入效果量（空行取消）: ");
            if (stacks == null || stacks.Value <= 0)
            {
                Console.WriteLine("已取消。\n");
                return;
            }

            // ── 4. 剩余回合：-1 = 永久（就是 Buff.Permanent 这个值，不另设一套暗号）──
            // 为什么不像上面的「效果量」那样「<= 0 一律取消」：永久是挂 buff 时真正想测的一种情况
            // （例如「永久 +1000 STR」），而 0 已经是「已到期」的含义、不能复用，
            // 所以复用 Buff 自己的哨兵 -1，并在提示里写清楚。
            int? turns = ReadIntOrCancel("请输入剩余回合（-1 = 永久，空行取消）: ");
            if (turns == null || (turns.Value <= 0 && turns.Value != Buff.Permanent))
            {
                Console.WriteLine("已取消。\n");
                return;
            }

            // ── 5. 造出来挂上去 ──
            Buff buff = BuffFactory.Create(id, stacks.Value, turns.Value);
            if (buff == null)
            {
                // 理论上到不了（上面 IsKnown 已经查过），留着防御「表里有 ID 但工厂返回 null」的将来改动
                Console.WriteLine("这个 buff 无法创建。\n");
                return;
            }
            player.AddBuff(buff);

            // 注意：AddBuff 对「同类 buff」走的是 MergeFrom 叠加，不是替换 ——
            // 所以玩家身上已经有一个同类 buff 时，这里报的是**本次提交的量**，
            // 而不是叠加后的实际总量（那要读 player.Buffs 里那个对象，属于显示层的事）。
            // 时长这里同样不能直接印 turns.Value：永久时它是哨兵 -1，会印出「持续-1」。
            string turnsText = turns.Value == Buff.Permanent ? "永久" : turns.Value.ToString();
            Console.WriteLine("已添加 " + buff.Name + "（lvl." + stacks.Value
                              + "，持续" + turnsText + "）。\n");
        }

        /// <summary>
        /// 让玩家选一只怪，返回怪物列表；取消时返回 <c>null</c>。
        /// </summary>
        private static List<Monster> SelectMonsters()
        {
            while (true)
            {
                Console.WriteLine("------------ 选择怪物 ------------");
                for (int i = 0; i < MonsterCatalog.Length; i++)
                {
                    Console.WriteLine("  " + i + ". " + MonsterCatalog[i].Label);
                }
                Console.WriteLine("  （空行取消）");
                Console.WriteLine("----------------------------------");

                int? choice = ReadIntOrCancel("请选择: ");
                if (choice == null)
                {
                    return null;
                }
                if (choice.Value < 0 || choice.Value >= MonsterCatalog.Length)
                {
                    // 与主菜单同一套容错风格：越界就重问，不崩
                    Console.WriteLine("没有这个选项。\n");
                    continue;
                }
                return MonsterCatalog[choice.Value].Build();
            }
        }

        /// <summary>
        /// 让玩家选战斗地形，返回环境对象；取消时返回 <c>null</c>。
        ///
        /// 清单来自 <see cref="EnvironmentCatalog"/>（只列**已实现**的环境：
        /// EnvironmentFactory.Create 会为其它的返回 null，列出来只会得到一个「没有环境」的战斗）。
        /// 原先这里手抄了一份与 GameLobby.SelectEnvironment 相同的列表，
        /// 现在两处共用一份数据，新增环境只改 EnvironmentCatalog。
        /// 文案与交互完全不变：编号从 1 开始、越界重问、空行取消。
        /// </summary>
        private static Environment SelectEnvironment()
        {
            while (true)
            {
                Console.WriteLine("------------ 选择战斗地形 ------------");
                for (int i = 0; i < EnvironmentCatalog.All.Count; i++)
                {
                    Console.WriteLine("  " + (i + 1) + ". " + EnvironmentCatalog.All[i].Name);
                }
                Console.WriteLine("  （空行取消）");
                Console.WriteLine("--------------------------------------");

                int? choice = ReadIntOrCancel("请选择: ");
                if (choice == null)
                {
                    return null;
                }

                if (choice.Value < 1 || choice.Value > EnvironmentCatalog.All.Count)
                {
                    Console.WriteLine("没有这个选项。\n");
                    continue;
                }

                (string Name, EnvType Type) pick = EnvironmentCatalog.All[choice.Value - 1];
                Console.WriteLine("你选择了：" + pick.Name);

                Environment env = EnvironmentFactory.Create(pick.Type);
                if (env != null)
                {
                    return env;
                }
                // 防御性分支：上面只列了已实现的两种，正常到不了这里
                Console.WriteLine("这个环境尚未实现。\n");
            }
        }

        /// <summary>
        /// 常驻调试玩家（首次或上一次战死时创建）。
        ///
        /// 起手**不带任何道具** —— 想要什么自己用「获取道具」领。
        /// 早先版本会静默附赠一把磷化剑和两个硫酸，结果第一次进来领道具时
        /// 背包里凭空多出两样东西，看起来像是"获得道具"发错了。
        /// 需要测某件装备时，先领再用就是了，不必预置。
        ///
        /// AddDefaultRecipes 保留：它只解锁配方（不产生道具），
        /// 与正式开局（Player.StartGame 里也调它）保持一致，
        /// 否则调试战斗里「4.徒手合成」永远是空的。
        /// </summary>
        private static Player DebugPlayer()
        {
            if (_debugPlayer == null || !_debugPlayer.IsAlive)
            {
                // 战死必须重建：RunBattle 的循环条件是 p.IsAlive，拿着一个 Hp<=0 的玩家
                // 进战斗会立刻走「你死了!」，等于一场空。
                _debugPlayer = new Player();
                _debugPlayer.AddDefaultRecipes();
            }
            return _debugPlayer;
        }

        /// <summary>
        /// 读一个整数；返回 <c>null</c> 表示取消。
        ///
        /// 取消的三种情况：空行、输入流结束（<c>Console.ReadLine()</c> 返回 null，
        /// 管道关闭 / Ctrl+Z / 重定向时会发生，直接 <c>.Trim()</c> 会 NullReferenceException）、
        /// 以及调用方约定的非法值。
        /// 非法输入（非数字 / 超出 int 范围）不取消，只提示后重问 —— 与 GameLobby.ReadChoice、
        /// Player.ReadInt 同一种写法：TryParse 而不是 Parse，绝不抛异常。
        ///
        /// ⚠️ 提示用 <c>WriteLine</c> 而不是 <c>Write</c>：
        ///   带换行的输出会经 LogRegionWriter 走 <c>AppendLog</c> 进**日志区**。
        ///   现在全项目的输入框都只画一个「> 」（见 ConsoleScreen.RedrawInputBox），
        ///   `SetPartial` / `SetPrompt` 已经删除 —— 提示一律走日志，
        ///   不再有「提示文字与终端回显叠在同一个框里」的错位问题。
        /// </summary>
        private static int? ReadIntOrCancel(string prompt)
        {
            while (true)
            {
                Console.WriteLine(prompt);
                string line = Console.ReadLine();
                if (line == null)
                {
                    // 输入流结束：当作取消，否则外层重问循环会永不退出（空转）
                    return null;
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    return null;
                }
                if (int.TryParse(line, out int value))
                {
                    return value;
                }
                Console.WriteLine("请输入数字。");
            }
        }
    }
}
