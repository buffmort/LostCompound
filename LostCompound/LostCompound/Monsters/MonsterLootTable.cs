/**
 * Author:  Hyberty
 * OOP牛逼
 */

using LostCompound.Items;

/**
 * 怪物掉落表。
 * 掉落物暂用 "**" 占位，每个怪物只写一条。
 * 写法和 Room 里的掉落判断保持一致：随机数 < 概率就掉。
 */
namespace LostCompound.Monsters
{
    public class MonsterLootTable
    {

        public static List<Item> drop(MonsterType? type)
        {
            List<Item> loots = new List<Item>();
            if (type == null)
            {
                return loots;
            }
            double r = Random.Shared.NextDouble();
            switch (type)
            {
                case MonsterType.LAVA_SLIME:
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(2));
                    }
                    if (r < 0.5)
                    {
                        loots.Add(new Wood(3));
                    }
                    break;
                case MonsterType.DUMMY:
                    Console.WriteLine("哇,你杀了一个假人! 你好棒!");
                    break;
                case MonsterType.ZOMBIE:
                    if (r < 0.70)
                    {
                        loots.Add(new Sulphur(1));
                    }
                    if (r < 0.5)
                    {
                        loots.Add(new Sulphur(1));
                    }
                    break;
                case MonsterType.DRAGON:
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

                case MonsterType.TWISTEDPAIR1:
                    loots.Add(new GlassJar(1));
                    if (Random.Shared.NextDouble() < 0.50)
                    {
                        loots.Add(new Sulphur(5));
                    }
                    break;
                case MonsterType.TWISTEDPAIR2:
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
