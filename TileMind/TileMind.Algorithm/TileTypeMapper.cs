using RiichiSharp.Tiles;
using TileType = TileMind.Common.Models.TileType;

namespace TileMind.Algorithm;

/// <summary>
/// 牌类型双向映射：TileMind.TileType ↔ RiichiSharp.Tile。
/// TileMind 使用 suit*10+num (M1=0..Z7=36)。RiichiSharp 使用 suit*9+(num-1) (m1=0..red=33)。
/// 赤牌 M0/P0/S0 映射为普通牌 M5/P5/S5，aka-dora 通过 GameContext.AkaCount 处理。
/// </summary>
public static class TileTypeMapper
{
    /// <summary>TileMind TileType → RiichiSharp Tile。</summary>
    public static Tile ToRiichiSharp(TileType tileType)
    {
        int val = (int)tileType;
        int suit = val / 10;
        int num = val % 10;

        // 赤牌 0 → 数字 5
        if (num == 0 && suit < 3)
            num = 5;

        int id = suit * 9 + (num - 1);
        return new Tile((byte)id);
    }

    /// <summary>RiichiSharp Tile → TileMind TileType。</summary>
    public static TileType ToTileMind(Tile tile)
    {
        int suit = tile.Id / 9;
        int num = (tile.Id % 9) + 1;
        return (TileType)(suit * 10 + num);
    }

    /// <summary>判断 TileType 是否为赤牌（M0/P0/S0）。</summary>
    public static bool IsAkaDora(TileType tileType)
    {
        int val = (int)tileType;
        return val % 10 == 0 && val / 10 < 3;
    }

    /// <summary>直接从 DetectionResult 提取 RiichiSharp Tile。</summary>
    public static Tile FromDetection(Common.Models.DetectionResult detection)
        => ToRiichiSharp(detection.TileType);
}
