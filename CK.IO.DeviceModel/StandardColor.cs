namespace CK.IO.DeviceModel;

public enum StandardColor
{
    Off,
    WhiteBlinking,
    White,
    RedBlinking,
    Red,
    GreenBlinking,
    Green,
    BlueBlinking,
    Blue,
    YellowBlinking,
    Yellow,
    MagentaBlinking,
    Magenta,
    CyanBlinking,
    Cyan,
}

public static class StandardColorExtensions
{
    /// <summary>
    /// Gets whether this color is blinking.
    /// </summary>
    /// <param name="color">This color.</param>
    /// <returns>True if this color is blinking, false otherwise.</returns>
    public static bool IsBlinking( this StandardColor color ) => ((int)color & 1) != 0;
}


