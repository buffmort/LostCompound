namespace LostCompound.Monsters
{
    public enum MonsterType
    {
        Dragon,
        Dummy,
        LavaSlime,
        TwistedPair1,
        TwistedPair2,
        Zombie,
        Skeleton,
        ShieldSoldier,   // 只追加在末尾（enum 成员的值不落库，但仍按项目约定避免改动已有成员）

        // 水淹洞穴专属（DESIGN §6 地图 2）
        Piranha,
        DiverZombie,

        // 沙漠专属（DESIGN §6 地图 3）
        Camel,

        /// <summary>硫磺怪（熔岩瀑布专属，Hyberty 2026-11-08 定）。</summary>
        SulphurMonster,

        /// <summary>水蛭（水淹洞穴专属，Hyberty 2026-11-08 定）。</summary>
        Leech,
    }
}
