using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
{
    public sealed class BlankRoom : Room
    {
        public BlankRoom(Environment environment) : base(RoomType.Blank, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            // 待定房间，默认什么都不做
            Console.WriteLine("这是一个空房间");
        }

        public override void OnLeave(Player p)
        {
        }
    }
}
