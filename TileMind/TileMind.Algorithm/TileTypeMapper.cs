using RiichiSharp.Tiles;
using TileType = TileMind.Common.Models.TileType;

namespace TileMind.Algorithm;

/// <summary>
/// 牌类型双向映射：TileMind.TileType ↔ RiichiSharp.Tile。
/// TileMind: suit*10+offset (M1=0..M9=8, M0=9, P1=10..Z7=36)，offset 0-8=牌面1-9，offset 9=赤牌。
/// RiichiSharp: suit*9+offset (m1=0..m9=8, p1=9.., s1=18.., honors 27..33)。
/// </summary>
public static class TileTypeMapper
{
    /// <summary>TileMind TileType → RiichiSharp Tile。</summary>
    public static Tile ToRiichiSharp(TileType tileType)
    {
        int val = (int)tileType;
        int suit = val / 10;
        int offset = val % 10; // 花色内位置：0-8=牌面1-9，9=赤牌

        // 赤牌（offset=9, 花色0-2）→ 普通牌 5（offset=4）
        int id;
        if (offset == 9 && suit < 3)
            id = suit * 9 + 4; // m5=4, p5=13, s5=22
        else
            id = suit * 9 + offset;

        return new Tile((byte)id);
    }

    /// <summary>RiichiSharp Tile → TileMind TileType。</summary>
    public static TileType ToTileMind(Tile tile)
    {
        int suit = tile.Id / 9;
        int offset = tile.Id % 9;
        return (TileType)(suit * 10 + offset);
    }

    /// <summary>判断 TileType 是否为赤牌（M0/P0/S0）：val%10==9 且花色<3。</summary>
    public static bool IsAkaDora(TileType tileType)
    {
        int val = (int)tileType;
        return val % 10 == 9 && val / 10 < 3;
    }

    /// <summary>直接从 DetectionResult 提取 RiichiSharp Tile。</summary>
    public static Tile FromDetection(Common.Models.DetectionResult detection)
        => ToRiichiSharp(detection.TileType);
}
