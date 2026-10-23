using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Monsters.Normal;

namespace LostCompound.Rooms
{
    public static class MobSpawner
    {
        /**
         * 熔岩瀑布的普通怪池：(名字, 工厂, 起始层, 结束层)。
         *
         * ⚠️ 层数用「区间」而不是「单个上限」：FromLayer 与 ToLayer 都是**闭区间端点**，
         * 第 FromLayer 层和第 ToLayer 层都算「能出现」（闭区间 [FromLayer, ToLayer]）。
         * 为什么不用单个 MaxLayer：单上限只能表达「第 1 层起、到第 N 层止」，
         * 表达不了「只在第 3-5 层出现」这种**两侧都有界**的怪；将来真加这种怪时
         * 就得把数据结构和过滤条件整个推翻重写。区间模型一次到位。
         * 以后要加一只「只在第 3-5 层」的怪，只需在本表加一行：
         *     ("Xxx", () => new Xxx(), 3, 5)
         *
         * ToLayer = int.MaxValue 表示「环境专属怪，任何层都能出现」（岩浆怪）。
         * 池按层数过滤后**等权重**抽取，所以第 1-3 层是僵尸 / 骷髅 / 岩浆怪各 1/3，
         * 与「加层数参数之前」的行为完全一致。
         */
        private static readonly (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] LavaWaterfallPool =
        [
            // 僵尸与骷髅：只在第 1-3 层（闭区间 [1, 3]，第 1 层和第 3 层都算）。
            ("僵尸", () => new Zombie(), 1, 3),
            ("骷髅", () => new Skeleton(), 1, 3),
            // 岩浆怪：熔岩瀑布的环境专属怪，不限层。
            ("岩浆怪", () => new LavaSlime(), 1, int.MaxValue),
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
         * 并且每次都重新按层数过滤 —— 所以僵尸 / 骷髅只会在第 1-3 层出现。
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
         * 抽一只怪：
         *   熔岩瀑布 → 从普通怪池里按层数过滤后等权重取一只；
         *   其余环境（含水淹洞穴）→ 仍出测试假人（用户明确说先不管）。
         * 设计文档里的食人鱼、洞潜僵尸、穿模骆驼、医生等尚未实现。
         */
        private static Monster SpawnOne(EnvType envType, int layer) => envType switch
        {
            EnvType.LavaWaterfall => SpawnFromPool(LavaWaterfallPool, layer),
            // 默认返回普通怪物（水淹洞穴的测试假人）
            _ => new Dummy(),
        };

        /**
         * 从池里按层数过滤后**等权重**取一只。
         * 过滤条件：FromLayer <= layer <= ToLayer（两端都是闭区间端点）。
         * 用整数骰而不是两次 NextDouble 比较，免得出现「某个分支概率是 1/2、
         * 另一个是 1/4」这种不均匀分配；池子长度为 n 时每只恰好 1/n。
         *
         * 池里始终有 ToLayer = int.MaxValue 的岩浆怪，所以过滤后不可能为空：
         * 第 1-3 层是 3 只各 1/3，第 4 层起只剩岩浆怪（必定出岩浆怪，不是空列表）。
         */
        private static Monster SpawnFromPool(
            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] pool, int layer)
        {
            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] available =
                pool.Where(m => m.FromLayer <= layer && layer <= m.ToLayer).ToArray();

            return available[Random.Shared.Next(available.Length)].Create();
        }
    }
}
