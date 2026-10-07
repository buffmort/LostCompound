using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Rooms
{
    public static class MobSpawner
    {
        /**
         * 按环境类型生成本轮战斗的怪物列表。
         * 目前实现很简单：熔岩瀑布在僵尸与岩浆怪之间二选一，其余环境出测试假人。
         * 设计文档里的食人鱼、洞潜僵尸、穿模骆驼、医生等尚未实现。
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
