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
    /// 现状：产物**由环境给出**（<c>Environment.MineOreWithAcid()</c>），硫酸只负责
    /// 「在哪儿能倒」和「拿到什么发什么」，不认识具体矿种。
    /// 水淹洞穴这条路径已接上；放射性区域（5.6）环境尚未实现，所以「开采放射矿」暂时到不了
    /// —— 等那个环境做出来，由它自己覆写那个产物槽位，本文件不用改。
    /// </summary>
    /// <remarks>
    /// 同时实现 Changeable：数量归零后由 Player.HandleChangeableItems 自动移出背包，
    /// 否则会留下一个「硫酸 x0」永远占着背包格（金属、水瓶等消耗品都是这个做法）。
    /// </remarks>
    public sealed class SulfuricAcid(int count) : Item(ItemIds.SulfuricAcid, "硫酸", 10, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.SulfuricAcid;

        public void Use(Player p, List<Monster> m)
        {
            // 只问环境「这里能腐蚀出什么矿脉」——拿得到就发，拿不到就当成「这里没用」。
            // 为什么不再问 Environment.CanMineOre 的布尔值、然后自己 new Metal：
            // 那个写法把产物写死在道具里，放射性区域（5.6）接上开采后会让硫酸静默产出金属矿。
            // 数量区间也随之搬到环境那边（水淹洞穴 1-3）。
            Environment env = p.CurrentEnvironment;
            Item ore = env?.MineOreWithAcid();
            if (ore != null)
            {
                // ⚠️ 文案暂时还写着「金属矿脉」：目前唯一会给产物的环境（水淹洞穴）给的就是金属。
                //    等放射性区域接上后，这句话得跟着产物改（那时应该由环境连文案一起给，
                //    或者按 ore.Name 拼）—— 记在这里，免得将来漏掉。
                Console.WriteLine("你把硫酸灌进岩缝，腐蚀出一块金属矿脉！");
                // 单次产出而不是走 Mining.Mine()：文档对硫酸写的是指定的「金属矿」，
                // 而 Mining 是「全类别随机 + 10% 放射矿」的通用矿道规则，两者不是同一件事。
                p.AddItem(ore);
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
