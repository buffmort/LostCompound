using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public class CraftRoom : Room
    {
        public CraftRoom(EnvType envType) : base(RoomType.CRAFT, envType)
        {
        }

        public override void onEnter(Player p)
        {
            Console.WriteLine("这里有一个合成台。");
            // 可以添加合成逻辑
        }
        public override void onLeave(Player p)
        {
        }
    }
}
