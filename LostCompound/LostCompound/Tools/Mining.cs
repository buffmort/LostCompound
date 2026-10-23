using LostCompound.Items;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;

namespace LostCompound.Tools
{
    /// <summary>
    /// 开采系统。
    /// 设计文档 5.4：矿道可开采 3-5 个矿物，全类别随机，10% 概率放射矿。
    /// 目前只有「水淹洞穴 + 硫酸」这条路径接上了；矿洞（5.4）与放射性区域（5.6）
    /// 的环境尚未实现，所以那里暂时调不到这里。
    /// </summary>
    /// <remarks>
    /// 为什么做成 static class 而不是写在某个道具/环境里：
    ///   开采的产物规则（数量 3-5、全类别随机、10% 放射矿）是设计文档写死的通用规则，
    ///   将来矿洞的矿道、放射性区域的指定房间、镐子、硫酸都要用同一套规则。
    ///   集中在这里，各处只负责「谁触发开采、扣什么代价」，产物规则只有一份。
    /// </remarks>
    public static class Mining
    {
        /// <summary>每个矿物成为放射性矿物的概率（设计文档 5.4：10% 概率放射矿）。</summary>
        private const double RadioactiveChance = 0.10;

        /// <summary>
        /// 开采一次，返回获得的矿物。
        ///
        /// 数量：Random.Shared.Next(minCount, maxCount + 1)（左闭右开，+1 才含上限）。
        /// 每一个矿物单独掷点：10% 概率是放射性矿物（11），否则从「其它可用矿物」里等概率随机。
        ///
        /// 为什么逐颗掷点而不是「整批掷一次」：
        ///   文档的「10% 概率放射矿」是描述单颗矿物的成色，逐颗掷点才能出现
        ///   「一趟里有 0 颗 / 1 颗 / 多颗放射矿」的自然分布；整批掷一次会变成
        ///   「要么全放射、要么全不放射」，与文档描述不符。
        ///
        /// 返回值交给调用方 p.AddItem(...)：这里不直接接触 Player，
        /// 保持 Tools.Mining 与 Player 解耦（和 Environment 不引用 Player 是同一个思路）。
        /// </summary>
        public static List<Item> Mine(int minCount = 3, int maxCount = 5)
        {
            int count = Random.Shared.Next(minCount, maxCount + 1);
            List<Item> result = [];

            for (int i = 0; i < count; i++)
            {
                result.Add(Random.Shared.NextDouble() < RadioactiveChance
                    ? new RadioactiveOre(1)
                    : CreateRandomMaterial());
            }

            return result;
        }

        /// <summary>
        /// 「全类别随机」里的一项：从可开采的普通矿物里等概率取一种，各 1 个。
        ///
        /// 选材依据（设计文档没列全，这是推测，已写进实现报告）：
        ///   取 ID 表中**材料类**的道具：金属(9) / 硫磺(1) / 木炭(3) / 沙子(6) / 木头(2) / 碘(14)，
        ///   外加红磷(31)。
        ///
        ///   为什么红磷虽然带主动效果也要放进来：
        ///     设计文档里红磷的获取途径**只有 Mine 一项**（不像白磷还写着「杀怪」），
        ///     不放进来它就成了永远拿不到的死道具。现实里磷也确实靠开采磷矿取得。
        ///
        ///   为什么白磷(5)仍然不放：
        ///     文档给白磷写了另一个来源「杀怪」，所以它不是死道具；
        ///     而它在代码里是消耗品，放进普通矿池会明显拉高开局强度。
        ///     等矿洞(5.4) 实现时若需要，再按文档给矿洞单独配权重表，而不是改这里。
        ///
        ///   为什么等概率：文档只说「全类别随机」，没说权重，等概率是最小假设。
        /// </summary>
        private static Item CreateRandomMaterial()
        {
            // 每项 new 一份 count=1 的堆叠：AddItem 会自动把同种矿物并进同一格，
            // 所以这里不必先做聚合，规则改动时也不用动合并逻辑。
            return Random.Shared.Next(7) switch
            {
                0 => new Metal(1),
                1 => new Sulphur(1),
                2 => new Charcoal(1),
                3 => new Sand(1),
                4 => new Wood(1),
                5 => new Iodine(1),
                _ => new RedPhosphorus(1),
            };
        }
    }
}
