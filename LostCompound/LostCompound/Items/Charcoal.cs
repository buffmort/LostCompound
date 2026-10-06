using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public class Charcoal : Item, TurnCount
    {
        public Charcoal(int count)
        {
            this.id = 3;
            this.name = "木炭";
            this.maxStack = 99;
            this.Stackable = true;
            this.count = count;
            this.type = ItemType.CHARCOAL;
        }

        public override ItemType? getType()
        {
            // Java 原为 return null; —— C# 用可空枚举 ItemType? 忠实保留「无类型」语义。
            // 注意：不能写成 default，那会等于 ItemType.SULPHUR，导致木炭被当成硫磺错误堆叠。
            return null;
        }


        public void onBattleStart(Player p, EnvType envType)
        {

        }

        public void onBattleEnd(Player p, EnvType envType)
        {

        }
    }
}
