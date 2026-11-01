using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Materials
{
    public sealed class Wood(int count) : Item(ItemIds.Wood, "木头", 99, true, count), ITurnCount, IChangeable, IItemState
    {
        public int Change { get; set; } = 3;

        public override ItemType? Type => ItemType.Wood;

        public void OnBattleStart(Player p, EnvType envType)
        {
            // 高温现在是**房间级**的（<see cref="Room.IsHot"/>，设计文档 §5.1）：
            // 不再是「整个熔岩瀑布都烤木头」，而是「只有高温房间烤」。
            // 判定仍然只问一个能力位，不在道具里比环境枚举。
            if (p.CurrentRoom?.IsHot == true)
            {
                Change -= 1;
                Console.WriteLine("木头:" + Change + "回合之后...");
            }
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {

        }

        public bool ShouldRemove()
        {
            return this.Change <= 0;
        }

        public Item Become()
        {
            Console.Write("木头变成了碳!");
            return new Charcoal(Count);
        }

        /// <summary>存档键名。**发布后不能改**（改了等于把旧存档里的倒计时清零）。</summary>
        private const string StateKeyChange = "change";

        /// <summary>木头要存的是「还有几回合被烤成炭」（<see cref="Change"/>）。</summary>
        public Dictionary<string, int> SaveState() => new() { [StateKeyChange] = Change };

        public void LoadState(IReadOnlyDictionary<string, int> state)
        {
            // 缺键 = 旧存档没这一项，保持构造时的默认值 3，不要当成 0
            // （当成 0 会让读档瞬间「木头变炭」）。
            if (state.TryGetValue(StateKeyChange, out int change))
            {
                Change = change;
            }
        }
    }
}
