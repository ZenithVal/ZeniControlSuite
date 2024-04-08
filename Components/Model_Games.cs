using MudBlazor;

public class Game
{
    public string Name { get; set; }
    public List<DescriptionLine> Description { get; set; }

    public class DescriptionLine
    {
        public Typo typo { get; set; } = Typo.body1;
        public string text { get; set; } = "";
    }

    public bool AutoGameCapable { get; set; }
}

public class GameType_TerrorsOfNowhere
{
    
}

public class GameType_Billiards
{
    
}

