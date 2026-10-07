using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface ICraftable
    {
        string Name { get; } //获得配方叫什么
        int[][] Materials { get; } //获取配方二维数组
        Item Product { get; } //获取产物
        bool CanCraft(Player player); //看看能不能合成
        //
        void Craft(Player player); //合成核心方法
        //
        string DescribeMaterials(Player player);//返回给玩家的字符串
    }
}
