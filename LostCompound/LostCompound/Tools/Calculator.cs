namespace LostCompound.Tools
{
    public static class Calculator
    {
        public static double CalculateAttack(double str, double atk) => 2 + str * Math.Sqrt(atk);

        public static double ApplyDefense(double def, double dmg) => Math.Max(dmg - def, 1);

        public static void Delay(int milliseconds)
        {
            try
            {
                Thread.Sleep(milliseconds);
            }
            catch (ThreadInterruptedException e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
