namespace ZeniControlSuite.Components.Classes
{
    public class Points
    {
		public static double pointsTotal = 0;
        public static double pointsWhole = 0;

        public static double pointAddStreak = 0;
        public static double pointsPartial = 0;
        public static double pointsPartialFlipped = 100;

        public static void UpdatePoints(double points)
        {
            pointsTotal += points;
            pointAddStreak = Math.Clamp(pointAddStreak+points, -1, 1);
            pointsWhole = Math.Truncate(pointsTotal);
            pointsPartial = Math.Abs((pointsTotal - pointsWhole) * 100);

            if (pointsPartial < 5)
            {
                pointsPartial = 0;
                pointsTotal = pointsWhole;
            }

            if (pointsPartial > 95)
            {
                pointsPartial = 0;
                if (pointsTotal > 0)
                {
                    pointsWhole = pointsWhole + 1;
                }
                else
                {
                    pointsWhole = pointsWhole - 1;
                }
                pointsTotal = pointsWhole;
            }

            //if the points are negative, flip the range of 0 and 100 
            if (pointsTotal < 0)
            {
                pointsPartialFlipped = 100 - pointsPartial;
            }
        }


        


    }
}
