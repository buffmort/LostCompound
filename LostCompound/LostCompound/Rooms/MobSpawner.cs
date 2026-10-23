using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Monsters.Normal;

namespace LostCompound.Rooms
{
    public static class MobSpawner
    {
        /**
         * 按环境类型生成本轮战斗的怪物列表。
         * 目前实现很简单：熔岩瀑布在僵尸 / 岩浆怪 / 骷髅三种普通怪之间等概率三选一，
         * 其余环境（含水淹洞穴）出测试假人。
         * 设计文档里的食人鱼、洞潜僵尸、穿模骆驼、医生等尚未实现。
         */
        public static List<Monster> Spawn(EnvType envType)
        {
            return envType switch
            {
                // 三种各 1/3：用整数骰而不是两次 NextDouble 比较，
                // 免得出现「某个分支概率是 1/2、另一个是 1/4」这种不均匀分配。
                // 骷髅是 §6.2 的「全地图可出现」普通怪，因此放进这个普通怪池。
                EnvType.LavaWaterfall => Random.Shared.Next(3) switch
                {
                    0 => new List<Monster> { new Zombie() },
                    1 => new List<Monster> { new LavaSlime() },
                    _ => new List<Monster> { new Skeleton() },
                },
                // 默认返回普通怪物
                _ => new List<Monster> { new Dummy() },
            };
        }
    }
}
