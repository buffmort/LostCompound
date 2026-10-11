// 与 Tools.cs 同样的别名：不加这行时 Environment 会同时命中
// System.Environment（隐式 using）与 LostCompound.MapEnvironment.Environment，产生 CS0104。
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 硫酸（配方 1，ID 10）：消耗品，堆叠 10。
    /// 设计文档第九章原文：FloodedCavern 开采金属矿；放射性区域开采放射矿。
    ///
    /// 现状：水淹洞穴这条路径已接上（问 <c>Environment.CanMineOre</c>，不判断具体环境类型）；
    /// 放射性区域（5.6）环境尚未实现，所以「开采放射矿」暂时到不了
    /// —— 等那个环境做出来，由它自己覆写能力位，本文件不用改。
    /// </summary>
    /// <remarks>
    /// 同时实现 Changeable：数量归零后由 Player.HandleChangeableItems 自动移出背包，
    /// 否则会留下一个「硫酸 x0」永远占着背包格（金属、水瓶等消耗品都是这个做法）。
    /// </remarks>
    public sealed class SulfuricAcid(int count) : Item(ItemIds.SulfuricAcid, "硫酸", 10, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.SulfuricAcid;

        /// <summary>在水淹洞穴开采出的金属数量区间（含两端）。文档只写「开采金属矿」，未给数量，取 1-3。</summary>
        private const int MinMetal = 1;
        private const int MaxMetal = 4;   // Random.Shared.Next 左闭右开，+1 后含 3

        public void Use(Player p, List<Monster> m)
        {
            // 只问环境「这里能不能用硫酸开采金属矿」（见 Environment.CanMineOre），
            // 不判断具体是哪种环境 —— 将来放射性区域的「开采放射矿」接上时，
            // 那个环境自己覆写能力位即可，本文件不用改。
            Environment env = p.CurrentEnvironment;
            if (env?.CanMineOre == true)
            {
                Console.WriteLine("你把硫酸灌进岩缝，腐蚀出一块金属矿脉！");
                // 单次产出而不是走 Mining.Mine()：文档对硫酸写的是指定的「金属矿」，
                // 而 Mining 是「全类别随机 + 10% 放射矿」的通用矿道规则，两者不是同一件事。
                p.AddItem(new Metal(Random.Shared.Next(MinMetal, MaxMetal)));
                Count -= 1;
            }
            else
            {
                Console.WriteLine("你不能饮用硫酸。");
                // 环境不对：不消耗，玩家可以留到水淹洞穴再用（与水瓶「没有水源不消耗」一致）
            }
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("硫酸只能在战斗中使用！");
        }

        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
