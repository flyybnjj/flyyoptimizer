namespace FlyyOptimizer.Windows;

internal static class VisualEffects
{
    // Documented SPI_GET/SET pairs: menus, combo boxes, smooth lists, menu fades, tooltips.
    internal static readonly uint[] Actions = [0x1002, 0x1004, 0x1006, 0x1012, 0x1016];
    internal static bool Allows(uint action) => Actions.Contains(action);
    internal static uint Parse(string name) => uint.Parse(name, System.Globalization.CultureInfo.InvariantCulture);
}
