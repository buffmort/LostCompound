using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Rooms
{
    public static class MobSpawner
    {
        /**
         * 随机返回 3 种不同的起始道具，供玩家选择 1 种。
         * 根据权重分配概率，药和咖啡不参与。
         *
         * @return 包含 3 种道具名称的 ArrayList
         */
        public static List<Monster> Spawn(EnvType envType)
        {
            return envType switch
            {
                EnvType.LavaWaterfall => Random.Shared.NextDouble() < 0.5
                    ? new List<Monster> { new Zombie() }
                    : new List<Monster> { new LavaSlime() },
                // 默认返回普通怪物
                _ => new List<Monster> { new Dummy() },
            };
        }
    }
}
