using SkiaSharp;

namespace KokonoeAssistant
{
    internal sealed class MemoryCortexNodeVm
    {
        public string Id { get; init; } = "";
        public string Text { get; init; } = "";
        public string Category { get; init; } = "general";
        public float Importance { get; init; }
        public int ConfirmCount { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; }
        public SKColor Color { get; init; } = SKColors.White;
    }
}
