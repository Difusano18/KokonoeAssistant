using WPoint = System.Windows.Point;
using WVector = System.Windows.Vector;

namespace KokonoeAssistant.Controls
{
    public sealed class Particle
    {
        public WPoint Position { get; set; }
        public WVector Velocity { get; set; }
        public double Radius { get; set; }
        public double DriftPhase { get; set; }
    }
}
