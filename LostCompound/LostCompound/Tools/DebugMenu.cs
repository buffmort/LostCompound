// 与 Tools.cs 同样的别名：不加这行时 Environment 会同时命中
// System.Environment（隐式 using）与 LostCompound.MapEnvironment.Environment，产生 CS0104。
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Rooms;   // StartRoom：给调试玩家挂假地图用

namespace LostCompound.Tools
{
    /// <summary>
    /// 调试菜单。只在主菜单输入 255 后可用。
    /// 目前提供两件事：按序号挑怪物 + 地形开一场战斗；按 ID 定量发放道具。
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
        /// Zombie / Dragon / LavaSlime / Dummy / TwistedPairP1 / TwistedPairP2。
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
            //   造一张 1×1 的假地图（只放一间起始房、带着同一个 env）就补上了这条路。
            //
            // 为什么这么做是安全的：JoinMap 只调用 StartRoom.MarkArrived()，
            // 不会触发 OnEnter —— 所以不会弹「初始物品选择」、不会重复发初始道具。
            AttachDebugMap(p, env);

            Tools.RunBattle(p, monsters, env);
        }

        /// <summary>
        /// 给调试玩家挂一张 1×1 的假地图，只为了让 <see cref="Player.CurrentEnvironment"/> 能查到环境。
        /// 调完之后玩家侧的环境道具（水瓶 / 硫酸 / 镐子）才和战斗侧看到的是同一个环境对象。
        /// </summary>
        private static void AttachDebugMap(Player p, Environment env)
        {
            Room[][] grid = new Room[1][];
            grid[0] = new Room[1];
            grid[0][0] = new StartRoom(env);
            p.JoinMap(grid);
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

            // ── 1. 列出所有道具：ID + 名称，按 ID 升序 ──
            // 序号就直接用 ID（不另编 0,1,2…）：用户要的是「for 所有道具 id 对应名称」，
            // 多一套序号只会让「选第 3 个」和「ID 3」对不上。
            Console.WriteLine("------------ 获取道具 ------------");
            foreach ((int Id, string Name, Func<int, Item> Create) entry in ItemFactory.All)
            {
                Console.WriteLine("  " + entry.Id + ". " + entry.Name + "  (ID " + entry.Id + ")");
            }
            Console.WriteLine("----------------------------------");

            // ── 2. 选道具 ──
            int? id = ReadIntOrCancel("请输入道具 ID（空行取消）: ");
            if (id == null)
            {
                Console.WriteLine("已取消。\n");
                return;
            }
            if (!ItemFactory.IsKnown(id.Value))
            {
                Console.WriteLine("没有这个道具 ID。\n");
                return;
            }

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
        /// 这里只列**已实现**的环境：EnvironmentFactory.Create 会为其它的返回 null，
        /// 列出来只会得到一个「没有环境」的战斗。这份列表是 GameLobby.SelectEnvironment 的副本
        /// （那个方法是 private，且主流程的文案不能改），新增环境时**两处都要补**。
        /// </summary>
        private static Environment SelectEnvironment()
        {
            while (true)
            {
                Console.WriteLine("------------ 选择战斗地形 ------------");
                Console.WriteLine("  1. 熔岩瀑布");
                Console.WriteLine("  2. 水淹洞穴");
                Console.WriteLine("  （空行取消）");
                Console.WriteLine("--------------------------------------");

                int? choice = ReadIntOrCancel("请选择: ");
                if (choice == null)
                {
                    return null;
                }

                EnvType envType;
                switch (choice.Value)
                {
                    case 1:
                        Console.WriteLine("你选择了：熔岩瀑布");
                        envType = EnvType.LavaWaterfall;
                        break;
                    case 2:
                        Console.WriteLine("你选择了：水淹洞穴");
                        envType = EnvType.FloodedCavern;
                        break;
                    default:
                        Console.WriteLine("没有这个选项。\n");
                        continue;
                }

                Environment env = EnvironmentFactory.Create(envType);
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
        /// 起手配置：默认配方 + 一把磷化剑 + 两个硫酸
        /// （硫酸用来试岩浆怪的高温免疫，也是能就地验证「环境道具」的最小样本）。
        /// </summary>
        private static Player DebugPlayer()
        {
            if (_debugPlayer == null || !_debugPlayer.IsAlive)
            {
                // 战死必须重建：RunBattle 的循环条件是 p.IsAlive，拿着一个 Hp<=0 的玩家
                // 进战斗会立刻走「你死了!」，等于一场空。
                _debugPlayer = new Player();
                _debugPlayer.AddDefaultRecipes();
                _debugPlayer.AddItem(new PhosphorusSword(1));
                _debugPlayer.AddItem(new SulfuricAcid(2));
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
        /// </summary>
        private static int? ReadIntOrCancel(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
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
