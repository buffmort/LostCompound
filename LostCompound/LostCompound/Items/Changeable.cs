namespace LostCompound.Items
{
    public interface Changeable
    {
        bool ShouldRemove();
        Item Become();
    }
}
