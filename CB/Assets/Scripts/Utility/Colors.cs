
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
            new Color(1.00f, 0.84f, 0.00f), // FFD600FF
            new Color(0.32f, 1.00f, 0.00f), // 52FF00FF
            new Color(0.00f, 1.00f, 0.52f), // 00FF85FF
            new Color(0.00f, 0.64f, 1.00f), // 00A3FFFF
            new Color(0.20f, 0.00f, 1.00f), // 3300FFFF
            new Color(1.00f, 0.00f, 0.96f), // FF00F5FF
        },
    };
}
