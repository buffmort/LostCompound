using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Materials
{
    public sealed class Wood(int count) : Item(ItemIds.Wood, "木头", 99, true, count), ITurnCount, IChangeable
    {
        public int Change { get; set; } = 3;

        public override ItemType? Type => ItemType.Wood;

        public void OnBattleStart(Player p, EnvType envType)
        {
            // 问玩家**当前所在地图的环境对象**「这里的高温会不会把木头烤成炭」，
            // 不再写 `envType == EnvType.LavaWaterfall`：那样每加一个高温环境
            // （岩浆池、火山区……）都要回来改这个道具，而且比的是环境枚举、
            // 与「环境自己声明能力位」这套做法不一致（见 Environment.BurnsWood）。
            //
            // 拿得到环境：ITurnCount 的签名里本来就带着 Player（由 Player.HandleChangeableItems 传入），
            // 而 Player.CurrentEnvironment 读的就是玩家所在房间的环境 —— 战斗中它与
            // Tools.RunBattle 收到的 env 是同一个对象，所以行为与原先「比 EnvType」等价。
            // envType 参数保留只是为了不改接口签名（其它道具还在用它）。
            if (p.CurrentEnvironment?.BurnsWood == true)
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
    }
}
