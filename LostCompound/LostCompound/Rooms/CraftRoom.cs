using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class CraftRoom : Room
    {
        public CraftRoom(EnvType envType) : base(RoomType.Craft, envType)
        {
        }

        public override void OnEnter(Player p)
        {
            Console.WriteLine("这里有一个合成台。");
            // 可以添加合成逻辑
        }

        public override void OnLeave(Player p)
        {
        }
    }
}
