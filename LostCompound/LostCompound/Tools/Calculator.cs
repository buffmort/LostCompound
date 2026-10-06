namespace LostCompound.Tools
{
    public class Calculator
    {
        public static double Attack(double str, double atk)
        {
            double OutPutDmg = 2 + str * (Math.Sqrt(atk));
            return OutPutDmg;
        }

        public static double Define(double def, double dmg)
        {
            double outPutDmg = Math.Max(dmg - def, 1);
            return outPutDmg;
        }

        public static void delay(int time)
        {
            try
            {
                Thread.Sleep(time);
            }
            catch (ThreadInterruptedException e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
