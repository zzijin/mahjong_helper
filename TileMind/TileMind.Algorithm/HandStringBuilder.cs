using System.Text;
using RiichiSharp.Tiles;
using TileMind.Common.Models;

namespace TileMind.Algorithm;

/// <summary>
/// 将 TileMind 的 AnalyzedFrame 数据转换为 RiichiSharp 牌谱字符串格式。
/// 格式："123m456p789s112z"，副露用括号 "(123m)" 或 "[1111m]"。
/// </summary>
public static class HandStringBuilder
{
    /// <summary>从玩家手牌构建牌谱字符串（13 或 14 张）。</summary>
    public static string BuildHandString(List<DetectionResult> handTiles)
    {
        var tiles = handTiles.Select(TileTypeMapper.FromDetection).ToList();
        return TilesToString(tiles);
    }

    /// <summary>构建含副露记号的完整手牌字符串（用于 Score 计算）。
    /// 例："123m(456p)789s11z" 表示手牌 123m789s11z + 明顺 456p。</summary>
    public static string BuildScoringHandString(PlayerFrameAnalysis player)
    {
        var sb = new StringBuilder();

        // 手牌部分
        var handTiles = player.HandTiles.Select(TileTypeMapper.FromDetection).ToList();
        sb.Append(TilesToString(handTiles));

        // 副露部分（明顺/明刻/明杠加圆括号，暗杠加方括号）
        foreach (var meld in player.Melds)
        {
            var meldTiles = meld.Tiles.Select(TileTypeMapper.FromDetection).OrderBy(t => t.Id).ToList();
            string meldStr = TilesToString(meldTiles);
            char bracket = meld.MeldType switch
            {
                MeldType.Ankan => '[',
                _ => '('
            };
            char close = meld.MeldType switch
            {
                MeldType.Ankan => ']',
                _ => ')'
            };
            sb.Append(bracket).Append(meldStr).Append(close);
        }

        return sb.ToString();
    }

    /// <summary>统计已鸣牌组数（用于向听数计算，暗杠不计数）。</summary>
    public static int CountCalledMelds(List<MeldAnalysis> melds)
    {
        return melds.Count(m => m.MeldType != MeldType.Ankan);
    }

    /// <summary>构建所有可见牌的字符串（弃牌+副露+宝牌指示牌）。
    /// 用于 ukeire 计算排除已不可用的牌。</summary>
    public static string BuildVisibleString(AnalyzedFrame analysis)
    {
        var allVisible = new List<Tile>();

        // 所有玩家的弃牌
        foreach (var (_, pondDets) in analysis.DiscardPondDetections)
            allVisible.AddRange(pondDets.Select(TileTypeMapper.FromDetection));

        // 所有玩家的副露（暗杠除外，不可见）
        foreach (var (_, player) in analysis.Players)
        {
            foreach (var meld in player.Melds)
            {
                if (meld.MeldType != MeldType.Ankan)
                    allVisible.AddRange(meld.Tiles.Select(TileTypeMapper.FromDetection));
            }
        }

        // 宝牌指示牌
        allVisible.AddRange(analysis.DoraIndicatorDetections.Select(TileTypeMapper.FromDetection));

        return TilesToString(allVisible);
    }

    /// <summary>统计手牌中赤牌数量。</summary>
    public static int CountAkaDora(List<DetectionResult> handTiles)
    {
        return handTiles.Count(d => TileTypeMapper.IsAkaDora(d.TileType));
    }

    /// <summary>将 Tile 列表按花色分组排序后拼接为牌谱字符串。</summary>
    private static string TilesToString(List<Tile> tiles)
    {
        if (tiles.Count == 0) return string.Empty;

        var manzu = tiles.Where(t => t.Suit == Suit.Manzu).OrderBy(t => t.Number).ToList();
        var pinzu = tiles.Where(t => t.Suit == Suit.Pinzu).OrderBy(t => t.Number).ToList();
        var souzu = tiles.Where(t => t.Suit == Suit.Souzu).OrderBy(t => t.Number).ToList();
        var honors = tiles.Where(t => t.Suit == Suit.Honor).OrderBy(t => t.Number).ToList();

        var sb = new StringBuilder();
        AppendSuit(sb, manzu, 'm');
        AppendSuit(sb, pinzu, 'p');
        AppendSuit(sb, souzu, 's');
        AppendSuit(sb, honors, 'z');
        return sb.ToString();
    }

    private static void AppendSuit(StringBuilder sb, List<Tile> tiles, char suitChar)
    {
        foreach (var tile in tiles)
            sb.Append(tile.Number);
        if (tiles.Count > 0)
            sb.Append(suitChar);
    }
}
