using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Monsters.Normal;

namespace LostCompound.Rooms
{
    public static class MobSpawner
    {
        /**
         * 全地图通用的普通怪池（DESIGN §6.2「普通怪物（只在第 1-3 层出现）」）。
         *
         * 这些怪**任何环境**都可能出现，所以它们不再挂在某一个环境的池子里，
         * 而是所有环境的「公共底子」。每个环境再按 <see cref="EnvPool"/> 叠上自己的专属怪。
         *
         * 元组字段：(名字, 工厂, 起始层, 结束层)。
         *
         * ⚠️ 层数用「区间」而不是「单个上限」：FromLayer 与 ToLayer 都是**闭区间端点**，
         * 第 FromLayer 层和第 ToLayer 层都算「能出现」（闭区间 [FromLayer, ToLayer]）。
         * 为什么不用单个 MaxLayer：单上限只能表达「第 1 层起、到第 N 层止」，
         * 表达不了「只在第 3-5 层出现」这种**两侧都有界**的怪；将来真加这种怪时
         * 就得把数据结构和过滤条件整个推翻重写。区间模型一次到位。
         * 以后要加一只「只在第 3-5 层」的怪，只需在本表加一行：
         *     ("Xxx", () => new Xxx(), 3, 5)
         */
        private static readonly (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] CommonPool =
        [
            // 僵尸与骷髅：只在第 1-3 层（闭区间 [1, 3]，第 1 层和第 3 层都算）。
            ("僵尸", () => new Zombie(), 1, 3),
            ("骷髅", () => new Skeleton(), 1, 3),
        ];

        /**
         * 熔岩瀑布的**环境专属**池（DESIGN §6「地图 1」）。
         *
         * ToLayer = int.MaxValue 表示「环境专属怪，任何层都能出现」。
         * 抽怪时候选 = <see cref="CommonPool"/> ∪ 本表，再按层数过滤后**等权重**抽：
         *   - 第 1-3 层 = 僵尸 / 骷髅 / 岩浆怪，各 1/3（与加层数参数之前完全一致）；
         *   - 第 4 层起 = 僵尸与骷髅被层数过滤掉，只剩岩浆怪（必定出岩浆怪）。
         */
        private static readonly (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] LavaWaterfallPool =
        [
            // 岩浆怪：熔岩瀑布的环境专属怪，不限层。
            ("岩浆怪", () => new LavaSlime(), 1, int.MaxValue),
        ];

        /**
         * 水淹洞穴的**环境专属**池（DESIGN §6「地图 2」：食人鱼 / 洞潜僵尸）。
         *
         * ⚠️ 这两只**还没实现**（代码里没有对应类），所以本表暂时是**空的**。
         * 因此水淹洞穴今天只能靠 <see cref="CommonPool"/> 出怪：
         *   - 第 1-3 层 → 僵尸 / 骷髅各 1/2（**本次修复的重点：不再是测试假人**）；
         *   - 第 4 层起 → 通用池被层数过滤空，走 <see cref="SpawnOne"/> 的兜底
         *     「忽略层数限制」再抽一次，于是**破例**出僵尸 / 骷髅（见该方法的注释）。
         *
         * 一旦实现食人鱼 / 洞潜僵尸，只需在下面加两行，MobSpawner 其它代码一行都不用改：
         *     ("食人鱼", () => new Piranha(), 1, int.MaxValue),
         *     ("洞潜僵尸", () => new DiverZombie(), 1, int.MaxValue),
         * 有了专属怪之后，第 4 层起的候选池就不再为空，**不会**再走到「忽略层数」的兜底。
         */
        private static readonly (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] FloodedCavernPool =
        [
        ];

        /// <summary>
        /// 一场战斗里同时出现的怪物数上限（= 敌人框能显示的 7 行）。
        ///
        /// 与 <see cref="UI.ConsoleScreen.MaxEnemyRows"/> 是同一个数字的两端：
        /// 这里是「生成侧」的封顶，那里是「显示侧」的封顶。两处必须一起改，
        /// 否则会生成第 8 只、而敌人框只画得下 7 行（静默丢一行）。
        /// </summary>
        public const int MaxMonsters = 7;

        /// <summary>基础「多出一只」概率（0 点战斗计数时）。</summary>
        private const double BaseExtraChance = 0.1;

        /// <summary>每 1 点战斗计数增加的「多出一只」概率。</summary>
        private const double ChancePerFightCount = 0.01;

        /// <summary>Boss 战的概率系数（用户裁定：Boss 战算计数，但概率降为原来的 0.5）。</summary>
        private const double BossChanceMultiplier = 0.5;

        /**
         * 按环境类型、当前层数与战斗计数生成本轮战斗的怪物列表（普通战斗入口）。
         *
         * 多怪公式（用户原话换算）：
         *     chance    = 0.1 + 0.01 × fightCount      // Boss 战再 × 0.5
         *     guaranteed = floor(chance)               // 必然多出的只数
         *     remainder  = chance - guaranteed         // 余数 → 概率多出 1 只
         *     extra      = guaranteed + (rand < remainder ? 1 : 0)
         *     total      = min(1 + extra, MaxMonsters)
         *
         * 自查：
         *     fightCount = 0   → 0.10 → 1~2 只
         *     fightCount = 90  → 1.00 → 必定 2 只
         *     fightCount = 110 → 1.20 → 必定 2 只 + 20% 第 3 只
         *     fightCount = 600 → 7.00 → 封顶 7 只
         *
         * 每多出的一只都**独立**从池里抽（可以重复抽到同一种怪），
         * 并且每次都重新按层数过滤 —— 所以僵尸 / 骷髅默认只会在第 1-3 层出现
         * （唯一例外是「过滤后为空」的兜底，见 <see cref="SpawnOne"/>）。
         *
         * 参数：
         *   layer     当前地图层数（从 1 开始）。防御性处理：layer < 1 时按第 1 层算，
         *             免得将来某个调用方传 0 / 负数把僵尸与骷髅整个过滤掉。
         *   fightCount 本场战斗开始前的累计战斗次数（Player.FightCount）。
         *   isBoss     Boss 战：概率 ×0.5。
         */
        public static List<Monster> Spawn(EnvType envType, int layer, int fightCount, bool isBoss = false)
        {
            int total = RollSpawnCount(fightCount, isBoss);
            return SpawnMany(envType, layer, total);
        }

        /**
         * Boss 房的「额外小怪」入口：Boss 本身固定只有一只，额外小怪从普通池里抽。
         *
         * 为什么单独开一个入口而不是让 BossRoom 调 Spawn 再拼：
         *   Spawn 的返回值包含「本场战斗的基础 1 只」，Boss 房里那 1 只已经被龙占了；
         *   直接拼会得到「龙 + 1~7 只小怪」= 最多 8 只，超出敌人框的 7 行。
         *   这里明确返回 **total - 1** 只额外小怪，且再夹到 MaxMonsters - 1（= 6），
         *   保证「龙 + 额外小怪」总数不超过 7。
         *
         * 概率仍用 Boss 系数（×0.5），与普通战斗的多怪公式同一份实现。
         * 额外小怪走**同一套**「通用池 ∪ 环境专属池」逻辑 —— Boss 房不会另立规则。
         */
        public static List<Monster> SpawnExtras(EnvType envType, int layer, int fightCount)
        {
            int total = RollSpawnCount(fightCount, isBoss: true);
            int extras = Math.Min(total - 1, MaxMonsters - 1);
            return SpawnMany(envType, layer, extras);
        }

        /**
         * 纯函数：按战斗计数算出这一战总共生成几只怪（含基础 1 只）。
         *
         * 独立成一个方法，是为了让「概率 → 只数」这段公式只有一份实现：
         * 普通战斗（<see cref="Spawn"/>）与 Boss 额外怪（<see cref="SpawnExtras"/>）
         * 都走它，不会出现「两处各写一遍、改一处忘另一处」。
         *
         * fightCount 为负数时按 0 算（防御性：调用方传错不该把概率压到 0.1 以下）。
         */
        public static int RollSpawnCount(int fightCount, bool isBoss = false)
        {
            int safeFightCount = Math.Max(fightCount, 0);
            double chance = BaseExtraChance + ChancePerFightCount * safeFightCount;
            if (isBoss) chance *= BossChanceMultiplier;

            int guaranteed = (int)Math.Floor(chance);
            double remainder = chance - guaranteed;
            int extra = guaranteed + (Random.Shared.NextDouble() < remainder ? 1 : 0);

            return Math.Min(1 + extra, MaxMonsters);
        }

        /**
         * 生成 count 只怪。每只都**独立**调用 SpawnOne（独立抽池、独立过滤层数），
         * 所以可以抽到同一种怪 —— 这正是「多只」的语义，不是 bug。
         */
        private static List<Monster> SpawnMany(EnvType envType, int layer, int count)
        {
            // 层数下限夹到 1：层数系统落地前，任何「未初始化 / 传错」的 0 都等价于第 1 层。
            int currentLayer = Math.Max(layer, 1);

            List<Monster> monsters = [];
            for (int i = 0; i < count; i++)
            {
                monsters.Add(SpawnOne(envType, currentLayer));
            }
            return monsters;
        }

        /**
         * 按环境类型取「环境专属池」。未实现的环境返回空表 —— 它们就只用通用池。
         *
         * 这样加新环境时只需在这里加一个 case + 一张专属池表；
         * 专属池为空的含义是「该环境还没有原生怪」，不是「该环境没有怪」。
         */
        private static (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] EnvPool(EnvType envType) =>
            envType switch
            {
                EnvType.LavaWaterfall => LavaWaterfallPool,
                EnvType.FloodedCavern => FloodedCavernPool,
                // 未实现的环境（含 None）：没有专属怪，只用通用池。
                _ => [],
            };

        /**
         * 抽一只怪：候选 = 通用池 ∪ 环境专属池，合并后按层数过滤，再**等权重**取一只。
         *
         * 空池兜底（硬要求：绝不能因为池空而崩溃）：
         *   1. 第一优先：过滤后为空 → **忽略层数限制**，用「通用池 + 专属池」的全部成员再抽一次。
         *      今天只有「水淹洞穴第 4 层起」会走到这里（专属池为空、通用池 [1,3] 全被滤掉），
         *      于是会**破例**出僵尸 / 骷髅 —— 这是**临时状态**：该环境还没有原生怪，
         *      只能借用通用池。实现食人鱼 / 洞潜僵尸（填进 <see cref="FloodedCavernPool"/>）后，
         *      第 4 层起的候选池不再为空，这条兜底就不会再被走到。
         *   2. 第二优先：如果连「全部成员」都是空的（两张表都空，理论上不该发生），
         *      打印一行醒目的警告并回退到**岩浆怪** —— 它的区间是 [1, ∞]，任何层都可用，
         *      所以这个回退分支自身**不可能**再抽到空池。
         *
         * 为什么第 1 步不直接回退到岩浆怪：水淹洞穴第 4 层起出岩浆怪（熔岩专属怪）
         * 比出通用池的僵尸 / 骷髅更不符合环境设定；借用通用池的怪是偏差最小的临时解。
         */
        private static Monster SpawnOne(EnvType envType, int layer)
        {
            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] pool =
                [.. CommonPool, .. EnvPool(envType)];

            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] available =
                pool.Where(m => m.FromLayer <= layer && layer <= m.ToLayer).ToArray();

            if (available.Length > 0)
            {
                return available[Random.Shared.Next(available.Length)].Create();
            }

            // 兜底 1：忽略层数限制，从「通用池 + 专属池」的全部成员里抽。
            if (pool.Length > 0)
            {
                Console.WriteLine("【警告】" + envType + " 第 " + layer +
                                  " 层没有符合层数区间的怪（专属池为空、通用池又被层数滤掉），" +
                                  "临时忽略层数限制，从全部候选里抽一只。实现该环境的原生怪后不会再走到这里。");
                return pool[Random.Shared.Next(pool.Length)].Create();
            }

            // 兜底 2：两张表都空（理论上不该发生）—— 醒目警告 + 岩浆怪（[1, ∞]，永远可用）。
            Console.WriteLine("【警告】" + envType + " 第 " + layer +
                              " 层的候选池完全为空（通用池与环境专属池都没有成员），已回退到岩浆怪。请检查 MobSpawner 的池表！");
            return new LavaSlime();
        }
    }
}
