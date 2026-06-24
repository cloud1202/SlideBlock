
using UnityEngine;

public static class Colors
{
    public const string Default = "";
    public const string Green = "#57C457";
    public const string Cyan = "#4FC3F7";
    public const string Yellow = "#FFD54F";
    public const string Orange = "#FF8A65";
    public const string Red = "#EF5350";
    public const string Gray = "#9E9E9E";
    public const string Purple = "#CE93D8";

    public static readonly Color[][] Sets = new Color[][]
    {
        new Color[]
        {
            new Color(1.00f, 0.00f, 0.00f), // FF0000FF
            new Color(1.00f, 0.39f, 0.00f), // FF6400FF
            new Color(1.00f, 1.00f, 0.00f), // FFFF00FF
            new Color(0.00f, 0.71f, 0.00f), // 00B400FF
            new Color(0.40f, 0.00f, 1.00f), // 6600FFFF
            new Color(0.29f, 0.00f, 0.51f), // 4B0082FF
            new Color(0.50f, 0.00f, 0.46f), // 800076FF
        },
    };
}
