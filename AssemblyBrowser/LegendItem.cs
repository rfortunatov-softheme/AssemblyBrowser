using System.Windows.Media;

namespace AssemblyBrowser
{
    public class LegendItem
    {
        public LegendItem()
        {
            ShouldShow = true;
        }

        public Brush Color { get; set; }

        public string AssemblyName { get; set; }

        public bool ShouldShow { get; set; }
    }
}