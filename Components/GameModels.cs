using MudBlazor;

public class Game
{
    public string Name { get; set; }
    public List<Line> Description { get; set; }

    public class Line
    {
        public Typo typo { get; set; } = Typo.body1;
        public string text { get; set; } = "";
    }

    public bool AutoGameCapable { get; set; }
}

