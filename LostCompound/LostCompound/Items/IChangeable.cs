namespace LostCompound.Items
{
    public interface IChangeable
    {
        bool ShouldRemove();
        Item Become();
    }
}
