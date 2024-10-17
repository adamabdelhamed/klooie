namespace ScrollSucker;

public class CityScape
{
    public static ConsoleBitmap Create(int w, int h, RGB backgroundColor, params RGB[] buildingColors)
    {
        buildingColors = buildingColors != null && buildingColors.Any() ? buildingColors : new RGB[] { backgroundColor == RGB.Black ? backgroundColor.Brighter : backgroundColor.Darker };

        var ret = new ConsoleBitmap(w, h);
        ret.Fill(backgroundColor);
        var rand = new Random();

        var averageBuildingWidth = 4;
        var x = 0;
        
        while(x < w)
        {
            var buildingWidth = averageBuildingWidth + rand.Next(-1, 2);
            var buildingHeight = rand.Next((int)(h * .25f), (int)(h * .85f));
            var buildingColor = buildingColors[rand.Next(0,buildingColors.Length)];
            var spireHeight = rand.NextDouble() > .8f && buildingWidth % 2 == 1 && buildingWidth > 1 ?  rand.Next(2, 4) : 0;
            var buildingGap = rand.NextDouble() < .9f ? 0 : 1;

            x += buildingGap;
            ret.FillRect(buildingColor, x, h - buildingHeight, buildingWidth, buildingHeight);
            if(spireHeight > 0)
            {
                var spireX = x + buildingWidth / 2;
                ret.FillRect(buildingColor, spireX, h - (buildingHeight + spireHeight), 1, spireHeight);
            }
            x += buildingWidth;
        }
        return ret;
    }
}
