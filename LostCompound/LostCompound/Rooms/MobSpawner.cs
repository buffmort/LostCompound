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

        /**
         * 按环境类型与当前层数生成本轮战斗的怪物列表。
         * 熔岩瀑布从普通怪池里按层数过滤后等概率取一只；
         * 其余环境（含水淹洞穴）仍出测试假人（用户明确说先不管）。
         * 设计文档里的食人鱼、洞潜僵尸、穿模骆驼、医生等尚未实现。
         *
         * 参数 layer：当前地图层数（从 1 开始）。防御性处理：layer < 1 时按第 1 层算，
         * 免得将来某个调用方传 0 / 负数把僵尸与骷髅整个过滤掉。
         */
        public static List<Monster> Spawn(EnvType envType, int layer)
        {
            // 层数下限夹到 1：层数系统落地前，任何「未初始化 / 传错」的 0 都等价于第 1 层。
            int currentLayer = Math.Max(layer, 1);

            return envType switch
            {
                EnvType.LavaWaterfall => SpawnFromPool(LavaWaterfallPool, currentLayer),
                // 默认返回普通怪物
                _ => new List<Monster> { new Dummy() },
            };
        }

        /**
         * 从池里按层数过滤后**等权重**取一只。
         * 过滤条件：FromLayer <= layer <= ToLayer（两端都是闭区间端点）。
         * 用整数骰而不是两次 NextDouble 比较，免得出现「某个分支概率是 1/2、
         * 另一个是 1/4」这种不均匀分配；池子长度为 n 时每只恰好 1/n。
         *
         * 池里始终有 ToLayer = int.MaxValue 的岩浆怪，所以过滤后不可能为空：
         * 第 1-3 层是 3 只各 1/3，第 4 层起只剩岩浆怪（必定出岩浆怪，不是空列表）。
         */
        private static List<Monster> SpawnFromPool(
            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] pool, int layer)
        {
            (string Name, Func<Monster> Create, int FromLayer, int ToLayer)[] available =
                pool.Where(m => m.FromLayer <= layer && layer <= m.ToLayer).ToArray();

            return new List<Monster> { available[Random.Shared.Next(available.Length)].Create() };
        }
    }
}
