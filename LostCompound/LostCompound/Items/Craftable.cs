using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface Craftable
    {
        string getName(); //获得配方叫什么
        int[][] getMaterials(); //获取配方二维数组
        Item getProduct(); //获取产物
        bool canCraft(Player player); //看看能不能合成
        //
        void craft(Player player); //合成核心方法
        //
        string getMaterialsStr(Player player);//返回给玩家的字符串
    }
}
