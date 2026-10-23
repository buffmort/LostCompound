using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
{
    public sealed class CraftRoom : Room
    {
        public CraftRoom(Environment environment) : base(RoomType.Craft, environment)
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
