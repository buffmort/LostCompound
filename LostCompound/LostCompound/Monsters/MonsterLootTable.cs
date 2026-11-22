/**
 * Author:  Hyberty
 * OOP牛逼
 */

using LostCompound.Items;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;
using LostCompound.Monsters.Normal;
using LostCompound.Monsters.Elite;
using LostCompound.Monsters.Boss;

/**
 * 怪物掉落表。
 *
 * 这里**两种判定风格并存**，看到第二种不要以为漏写了概率：
 *   · 「随机数 < 概率」：某样东西掉 / 不掉（岩浆怪、僵尸、龙的追加掉落都属这种）。
 *   · 「权重抽样」：从一张「数量 + 名称 + 权重」表里抽一项（骷髅，见 SkeletonDrops）。
 *     权重只是相对比例，抽中的总概率恒为 1；表里的数字不是概率，加起来也不必是 100。
 *
 * ⚠️ r 在 Drop 开头只摇一次，所有用 r 的 case 共用它：
 * 同一个 case 里多条 `r < x` 判断**不是相互独立**的，
 * 「掉两样」只会发生在两段阈值的重叠区间里（岩浆怪仍保留这个既有行为）。
 */
namespace LostCompound.Monsters
{
    public static class MonsterLootTable
    {
        /// <summary>
        /// 骷髅的掉落表：`(数量, 权重, 构造方式)`，总权重 = 3+2+2+3+1+1 = **12**
        /// （设计文档 §6.2 的「数量 + 名称 + 权重」表）。
        ///
        /// 为什么存 `Func&lt;Item&gt;` 而不是现成实例：掉落物每次击杀都要新造一个进背包，
        /// 数量也各不相同；工厂能把「造几件什么」和「抽中哪一项」分开写清楚。
        /// </summary>
        private static readonly (int Count, int Weight, Func<Item> Create)[] SkeletonDrops =
        [
            (1, 3, () => new Wood(1)),
            (2, 2, () => new Wood(2)),
            (3, 2, () => new WhitePhosphorus(3)),
            (2, 3, () => new WhitePhosphorus(2)),
            (1, 1, () => new Metal(1)),
            (1, 1, () => new Iodine(1)),
        ];

        public static List<Item> Drop(MonsterType? type)
        {
            List<Item> loots = [];
            if (type == null)
            {
                return loots;
            }
            double r = Random.Shared.NextDouble();
            switch (type)
            {
                case MonsterType.LavaSlime:
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(2));
                    }
                    if (r < 0.5)
                    {
                        loots.Add(new Wood(3));
                    }
                    break;
                case MonsterType.SulphurMonster:
                    // 硫磺怪（Hyberty 2026-11-08 定）：**同岩浆怪那一档 —— 70% 掉 2 硫磺**。
                    // 只抄这一档，不抄岩浆怪附带的那条「50% 掉 3 木头」：
                    // 裁定原文只说了「同类：70% 掉硫磺 x2」。
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(2));
                    }
                    break;
                case MonsterType.Leech:
                    // 水蛭（Hyberty 2026-11-08 定）：布料 x1 权重 1、布料 x2 权重 0.5。
                    // 权重表是 int 的（见 SkeletonDrops），0.5 没法直接写 —— 两项**同乘 2**
                    // 得到「x1 : 2 / x2 : 1」，比例与 1 : 0.5 完全一致（66.7% / 33.3%）。
                    // 权重只是相对比例，整体放大不改变任何一个概率。
                    {
                        const int totalWeight = 3;                 // 2 + 1
                        int pick = Random.Shared.Next(totalWeight);
                        loots.Add(pick < 2 ? new Cloth(1) : new Cloth(2));
                    }
                    break;
                case MonsterType.Sandworm:
                    // 沙虫（Hyberty 2026-11-08 定）：**沙子 15~30 个**（含两端）。
                    // 沙漠本来就只出沙子（§5.3），所以这条不破坏「只给沙子」的口径。
                    // 用 Next(min, max + 1) 因为上界是排他的。
                    loots.Add(new Sand(Random.Shared.Next(
                        Sandworm.MinLootSand, Sandworm.MaxLootSand + 1)));
                    break;
                case MonsterType.Mummy:
                    // 木乃伊（Hyberty 2026-11-08 定）：**布料 2~5** + **沙子 5~10**（均含两端）。
                    // 两个数各摇一次、互不影响 —— 不是「二选一」，也不是同一次掷点决定两样。
                    // 上界 +1 是因为 Random.Next 的上界是排他的。
                    loots.Add(new Cloth(Random.Shared.Next(
                        Mummy.MinLootCloth, Mummy.MaxLootCloth + 1)));
                    loots.Add(new Sand(Random.Shared.Next(
                        Mummy.MinLootSand, Mummy.MaxLootSand + 1)));
                    break;
                case MonsterType.Dummy:
                    Console.WriteLine("哇,你杀了一个假人! 你好棒!");
                    break;
                case MonsterType.RustedGolem:
                    // 锈铁傀儡（助手设计，2026-11-08）：**60% 掉 1 个金属**。
                    // 它本来就是铁锈成的，拆下来就是金属；概率写死 60% ——
                    // 通用池怪不该像精英怪那样稳定给材料。
                    if (r < 0.60)
                    {
                        loots.Add(new Metal(1));
                    }
                    break;
                case MonsterType.Zombie:
                    AddZombieLoot(loots, r);
                    break;
                case MonsterType.Skeleton:
                    // 掉落规则（设计文档 §6.2）：每次击杀**抽 1 或 2 次**，每次从权重表里抽一项。
                    // 「1-2 个」的实现：Random.Shared.Next(1, 3) 上界排他 → 1 或 2，各 50%。
                    // 注意抽的是「表项」而不是「件数」：抽到 (2, 木头) 就掉 2 个木头。
                    // 两次抽到同一项是允许的（会分别 AddItem 成两堆，而不是合并成一次掉落）。
                    // 这里不使用开头摇的 r（那是「概率风格」用的），每次抽样都独立摇。
                    {
                        int totalWeight = 0;
                        foreach ((int Count, int Weight, Func<Item> Create) entry in SkeletonDrops)
                        {
                            totalWeight += entry.Weight;
                        }

                        int draws = Random.Shared.Next(1, 3);
                        for (int i = 0; i < draws; i++)
                        {
                            // 权重抽样：先摇 [0, totalWeight)，再按累计权重定位表项。
                            int pick = Random.Shared.Next(totalWeight);
                            foreach ((int Count, int Weight, Func<Item> Create) entry in SkeletonDrops)
                            {
                                if (pick < entry.Weight)
                                {
                                    loots.Add(entry.Create());
                                    break;
                                }
                                pick -= entry.Weight;
                            }
                        }
                    }
                    break;
                case MonsterType.Piranha:
                    // Hyberty裁定：**50% 掉 1 个白磷，20% 掉 2 个**。
                    // 口径是「互斥的两档」，不是「两个独立判定」：先看 20% 拿 2 个，
                    // 没中再看 50% 拿 1 个。若写成两个独立的 `if (r < x)`，
                    // 在 r < 0.2 的区间里会**两档同时命中**（白磷 1+2=3 个），
                    // 那是叠加不是分档 —— 那种写法只在**有意「重叠即掉两样」**时才用
                    // （见类注释里岩浆怪的例子）。
                    if (r < 0.2)
                    {
                        loots.Add(new WhitePhosphorus(2));
                    }
                    else if (r < 0.5)
                    {
                        loots.Add(new WhitePhosphorus(1));
                    }
                    break;
                case MonsterType.DiverZombie:
                    // Hyberty裁定：**必定掉 1-2 个布料**，再 **40% 掉 1 个白磷**，
                    // 剩下那条「合并普通僵尸」—— 也就是普通僵尸那张加权表（木头 5 / 布料 3 / 木炭 2）。
                    // 「合并」直接复用 AddZombieLoot，不再抄一份权重：两处手抄迟早会不一致。
                    loots.Add(new Cloth(Random.Shared.Next(1, 3)));   // 1 或 2

                    if (r < 0.4)
                    {
                        loots.Add(new WhitePhosphorus(1));
                    }

                    AddZombieLoot(loots, r);
                    break;
                case MonsterType.ShieldSoldier:                    // DESIGN §6.2：掉落「1 金属（权重 0.5）」。
                    // ⚠️ 这里的 0.5 是**权重**不是概率 —— 表里只有它一项，抽中率恒为 100%？
                    //   不。按文档的口径「数量 + 名称 + 权重/概率」，单一来源 + 权重 0.5 的写法
                    //   等价于「50% 掉 1 个金属」（权重只有一项时它就是概率）。
                    //   若将来给盾兵加第二个掉落物，必须改成和骷髅一样的权重表，不能直接相加。
                    if (r < 0.5)
                    {
                        loots.Add(new Metal(1));
                    }
                    break;
                case MonsterType.Dragon:
                    loots.Add(new WhitePhosphorus(2));
                    if (Random.Shared.NextDouble() < 0.6)
                    {
                        loots.Add(new FushionSluphur(1));
                    }
                    if (Random.Shared.NextDouble() < 0.3)
                    {
                        loots.Add(new Sand(1));
                    }
                    break;

                case MonsterType.SandBurial:
                    // 沙葬者（Hyberty 2026-11-22 定）：它与沙虫同源，掉的也是沙 ——
                    // 数量比沙虫高一档（沙虫 15~30），因为它是 Boss。
                    // ⚠️ 但沙漠环境有 <c>SuppressMonsterLoot</c>（DESIGN §5.3 第 2 条「怪物不掉落物品」），
                    //    所以这条在沙漠层实际不会被执行到（Monster.Die 连表都不摇）。
                    //    保留它是因为「环境抑制掉落」是环境的能力位、不是永久规则 ——
                    //    将来若给沙漠放行，这里就立刻生效。
                    loots.Add(new Sand(Random.Shared.Next(
                        SandBurial.MinLootSand, SandBurial.MaxLootSand + 1)));
                    break;

                case MonsterType.SecondHandWorm:
                    // 秒针虫（Hyberty 2026-11-22 定）：掉 1 个放射性矿物。
                    // 它本身就是「这层辐射更快」的化身，掉一块放射矿 = 把它身上的东西带走
                    // （而且那块矿会继续按回合侵蚀玩家，代价延续到战斗之外）。
                    loots.Add(new RadioactiveOre(1));
                    break;

                case MonsterType.RadioactiveZombie:
                    // 放射僵尸（Hyberty 2026-11-22 定）：
                    //   2~3 个放射性矿物（权重 1）+ 1 金属（权重 0.5）。
                    // 矿物数量用 Next(2, 4) 取 2 或 3；金属按 50% 掷一次 ——
                    // 「权重 0.5」在这个项目里就是「以 0.5 的概率出场」，
                    // 与池子里的权重（相对概率）不是一个东西，别混。
                    loots.Add(new RadioactiveOre(Random.Shared.Next(2, 4)));

                    if (Random.Shared.NextDouble() < 0.5)
                    {
                        loots.Add(new Metal(1));
                    }
                    break;

                // ── 第 6-12 层通用怪（Hyberty 2026-11-22 设计书）──
                // 三只的掉落都是「若干候选里掷一个」，所以统一用 NextDouble 分档，
                // 而不是把每种候选都塞进 loots —— 设计书写的是三选一。
                case MonsterType.ArmoredHulk:
                    // 装甲：1 金属（权重 3）、2 金属（权重 2）、1 布料（权重 1）。
                    // 权重加起来是 6：3/6 一件金属、2/6 两件金属、1/6 布料。
                    {
                        double roll = Random.Shared.NextDouble() * 6.0;
                        if (roll < 3.0) loots.Add(new Metal(1));
                        else if (roll < 5.0) loots.Add(new Metal(2));
                        else loots.Add(new Cloth(1));
                    }
                    break;

                case MonsterType.Parasite:
                    // 寄生虫：1 白磷（权重 3）、1 布料（权重 2）、1 碘（权重 1）。
                    {
                        double roll = Random.Shared.NextDouble() * 6.0;
                        if (roll < 3.0) loots.Add(new WhitePhosphorus(1));
                        else if (roll < 5.0) loots.Add(new Cloth(1));
                        else loots.Add(new Iodine(1));
                    }
                    break;

                case MonsterType.LivingPrism:
                    // 活棱镜：1 放射矿（权重 2）、1 金属（权重 2）、1 白磷（权重 1）。
                    {
                        double roll = Random.Shared.NextDouble() * 5.0;
                        if (roll < 2.0) loots.Add(new RadioactiveOre(1));
                        else if (roll < 4.0) loots.Add(new Metal(1));
                        else loots.Add(new WhitePhosphorus(1));
                    }
                    break;

                case MonsterType.Tidecaller:
                    // 涨潮者（Hyberty 2026-11-22 定）：Boss 掉落，数量按 Hyberty「掉落数量 X2」翻倍。
                    //
                    // 三样东西都贴着水淹洞穴的既有玩法，不是随手凑数：
                    //   玻璃罐 —— 容器，水淹洞穴的通用产出；
                    //   水瓶   —— 它是「吸水」的 Boss，掉水最直白；
                    //   硫酸   —— 本环境用硫酸采金属矿（<c>FloodedCavern.MineOreWithAcid</c>），
                    //             打完 Boss 给一瓶才对得上「这层能挖矿」。
                    // ⚠️ 与沙葬者一样：水淹洞穴有自己的掉落规则，这条未必跑到 ——
                    //    保留它是「环境抑制掉落是能力位、不是永久规则」的余地。
                    loots.Add(new GlassJar(2));
                    loots.Add(new WaterBottle(2));
                    loots.Add(new SulfuricAcid(1));
                    break;

                case MonsterType.TwistedPair1:
                    loots.Add(new GlassJar(1));
                    if (Random.Shared.NextDouble() < 0.50)
                    {
                        loots.Add(new Sulphur(5));
                    }
                    break;
                case MonsterType.TwistedPair2:
                    loots.Add(new FushionSluphur(3));
                    if (Random.Shared.NextDouble() < 0.60)
                    {
                        loots.Add(new Wood(5));
                    }
                    break;
                default:
                    Console.WriteLine("你获得了一个大大的空集!");
                    break;
            }
            return loots;
        }

        /// <summary>
        /// 「普通僵尸那一套掉落」：70% 掉 1 件，掉的时候按权重选 木头 5 / 布料 3 / 木炭 2。
        ///
        /// 抽出来单独一个方法，是因为**洞潜僵尸要「合并普通僵尸」的掉落**（Hyberty裁定）——
        /// 两处各抄一份权重表，早晚会改了一处忘了另一处（这类「同一份知识写两遍」的坑
        /// 在 MobSpawner / EnvironmentCatalog 都踩过，见 CONVENTIONS 2.5）。
        ///
        /// ⚠️ 概率仍是**替Hyberty定的占位值**：Hyberty只裁定「僵尸掉木炭 / 木头 / 布料」。
        /// 给出正式数值后，改这里的 0.70 与三个权重即可，两个调用点自动跟上。
        /// 用 if/else 而不是嵌套三元，是为了让「哪段区间对应哪件」一眼能看出来。
        /// </summary>
        private static void AddZombieLoot(List<Item> loots, double r)
        {
            if (r >= 0.70)
            {
                return;
            }

            int pick = Random.Shared.Next(10);
            if (pick < 5)
            {
                loots.Add(new Wood(1));
            }
            else if (pick < 8)
            {
                loots.Add(new Cloth(1));
            }
            else
            {
                loots.Add(new Charcoal(1));
            }
        }
    }
}
