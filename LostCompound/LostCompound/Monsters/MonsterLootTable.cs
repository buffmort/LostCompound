/**
 * Author:  Hyberty
 * OOP牛逼
 */

using LostCompound.Items;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;

/**
 * 怪物掉落表。
 * 每个怪物一条 case，内部用「随机数 < 概率就掉」的写法决定是否掉落。
 * 注意：岩浆怪与僵尸只摇一次随机数 r，两条掉落判断共用同一个 r，
 * 所以「掉两样」只在 r 落在两段概率之间的窄区间里发生，并不是各自独立判定。
 * 这一行为与移植前一致，暂不改动。
 */
namespace LostCompound.Monsters
{
    public static class MonsterLootTable
    {

        public static List<Item> Drop(MonsterType? type)
        {
            List<Item> loots = [];
            if (type == null)
            {
                return loots;
            }
            double r = Random.Shared.NextDouble();
            switch (type)
            {
                case MonsterType.LavaSlime:
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(2));
                    }
                    if (r < 0.5)
                    {
                        loots.Add(new Wood(3));
                    }
                    break;
                case MonsterType.Dummy:
                    Console.WriteLine("哇,你杀了一个假人! 你好棒!");
                    break;
                case MonsterType.Zombie:
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(1));
                    }
                    if (r < 0.5)
                    {
                        loots.Add(new Sulphur(1));
                    }
                    break;
                case MonsterType.Dragon:
                    loots.Add(new WhitePhosphorus(2));
                    if (Random.Shared.NextDouble() < 0.6)
                    {
                        loots.Add(new FushionSluphur(1));
                    }
                    if (Random.Shared.NextDouble() < 0.3)
                    {
                        loots.Add(new Sand(1));
                    }
                    break;

                case MonsterType.TwistedPair1:
                    loots.Add(new GlassJar(1));
                    if (Random.Shared.NextDouble() < 0.50)
                    {
                        loots.Add(new Sulphur(5));
                    }
                    break;
                case MonsterType.TwistedPair2:
                    loots.Add(new FushionSluphur(3));
                    if (Random.Shared.NextDouble() < 0.60)
                    {
                        loots.Add(new Wood(5));
                    }
                    break;
                default:
                    Console.WriteLine("你获得了一个大大的空集!");
                    break;
            }
            return loots;
        }
    }
}
