using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class BlankRoom : Room
    {
        public BlankRoom(EnvType envType) : base(RoomType.Blank, envType)
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
