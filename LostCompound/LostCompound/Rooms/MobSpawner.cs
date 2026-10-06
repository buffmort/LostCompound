using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Rooms
{
    public class MobSpawner
    {
        private static readonly Random random = new Random();
        /**
         * 随机返回 3 种不同的起始道具，供玩家选择 1 种。
         * 根据权重分配概率，药和咖啡不参与。
         *
         * @return 包含 3 种道具名称的 ArrayList
         */
        public static Random rd = new Random();
        public static List<Monster> spawn(EnvType envType)
        {
            switch (envType)
            {
                case EnvType.LAVA_WATERFALL:
                {
                    double num = rd.NextDouble();
                    if (num < 0.5)
                    {
                        List<Monster> m = new List<Monster>();
                        m.Add(new Zombie());
                        return m;
                    }
                    else
                    {
                        List<Monster> m = new List<Monster>();
                        m.Add(new LavaSlime());
                        return m;
                    }
                }
                default:
                {
                    // 默认返回空数组或普通怪物
                    List<Monster> m = new List<Monster>();
                    m.Add(new Dummy());
                    return m;
                }
            }
        }

    }
}
