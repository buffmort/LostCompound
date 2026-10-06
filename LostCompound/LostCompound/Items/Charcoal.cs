using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public sealed class Charcoal(int count) : Item(3, "木炭", 99, true, count), TurnCount
    {
        // Java 原版这里返回 null，与构造函数里设的 type = ItemType.CHARCOAL 自相矛盾，属笔误。
        // 已修正为返回自身类型，与其它道具一致。
        // 注意：不能写成 default，那会等于 ItemType.Sulphur，导致木炭被当成硫磺错误堆叠。
        public override ItemType? Type => ItemType.Charcoal;

        public void OnBattleStart(Player p, EnvType envType)
        {

        }

        public void OnBattleEnd(Player p, EnvType envType)
        {

        }
    }
}
