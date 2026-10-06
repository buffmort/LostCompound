using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public sealed class Charcoal(int count) : Item(3, "木炭", 99, true, count), TurnCount
    {
        // 保留 Java 原本的「无类型」语义：返回 null。
        // 注意：不能写成 default，那会等于 ItemType.Sulphur，导致木炭被当成硫磺错误堆叠。
        public override ItemType? Type => null;

        public void OnBattleStart(Player p, EnvType envType)
        {

        }

        public void OnBattleEnd(Player p, EnvType envType)
        {

        }
    }
}
