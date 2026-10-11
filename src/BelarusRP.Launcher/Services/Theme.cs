using System.Windows;
using System.Windows.Media;

namespace BelarusRP.Launcher
{
    /// <summary>Тёмная / светлая тема. Кисти лежат в Application.Resources как T_Bg, T_Fg, T_Card, T_CardBorder, T_Input.</summary>
    public static class Theme
    {
        static SolidColorBrush B(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }

        public static void Apply(bool light)
        {
            var r = Application.Current.Resources;
            r["T_Bg"] = B(light ? "#F4F6FA" : "#0B0D14");
            r["T_Fg"] = B(light ? "#14171F" : "#FFFFFF");
            r["T_Card"] = B(light ? "#0F000000" : "#1AFFFFFF");
            r["T_CardBorder"] = B(light ? "#33000000" : "#33FFFFFF");
            r["T_Input"] = B(light ? "#0D000000" : "#40000000");
        }
    }
}
