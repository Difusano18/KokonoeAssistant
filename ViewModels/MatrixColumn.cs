using System.Collections.Generic;
using System.Windows.Controls;

namespace KokonoeAssistant
{
    internal sealed class MatrixColumn
    {
        public double X;
        public double Y;
        public double Speed;
        public List<TextBlock> Cells = new();
        public int Length;
    }
}
