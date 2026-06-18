namespace TileMind.Common.Models;

/// <summary>本家手牌分析结果。</summary>
public class TileAnalysisResult
{
    /// <summary>向听数：-1=和了，0=听牌，1+=距听牌差几手。</summary>
    public int Shanten { get; set; }

    /// <summary>是否已听牌（向听数 = 0）。</summary>
    public bool IsTenpai => Shanten == 0;

    /// <summary>每种牌在牌山中的剩余张数（0~4）。</summary>
    public Dictionary<TileType, int> RemainingCounts { get; set; } = new();

    /// <summary>未听牌时的打牌选项：打哪张 → 结果向听数/受入。</summary>
    public List<DiscardOption> DiscardOptions { get; set; } = new();

    /// <summary>已听牌时的胡牌选项：每一张等牌 → 翻数/符数/点数/剩余张数。</summary>
    public List<WinOption> WinOptions { get; set; } = new();
}

/// <summary>打牌选项（未听牌时）。</summary>
public class DiscardOption
{
    public TileType DiscardTile { get; set; }
    public int ShantenAfter { get; set; }
    public int UniqueUkeire { get; set; }
    public int TotalUkeire { get; set; }
    public List<UkeireTile> UkeireTiles { get; set; } = new();
}

/// <summary>受入牌信息。</summary>
public class UkeireTile
{
    public TileType Tile { get; set; }
    public int Available { get; set; }
}

/// <summary>胡牌选项（已听牌时）。</summary>
public class WinOption
{
    public TileType WinTile { get; set; }
    public int Remaining { get; set; }
    public int Han { get; set; }
    public int Fu { get; set; }
    public int Points { get; set; }
    public List<string> YakuNames { get; set; } = new();
}
