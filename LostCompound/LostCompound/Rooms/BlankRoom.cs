using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public class BlankRoom : Room
    {
        public BlankRoom(EnvType envType) : base(RoomType.BLANK, envType)
        {
        }

        public override void onEnter(Player p)
        {
            // 待定房间，默认什么都不做
            Console.WriteLine("这是一个空房间");
        }
        public override void onLeave(Player p)
        {
        }
    }
}
