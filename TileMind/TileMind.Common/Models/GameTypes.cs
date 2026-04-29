namespace TileMind.Common.Models;

/// <summary>
/// 玩家座位位置（相对于屏幕/摄像头视角）。
/// </summary>
public enum SeatPosition
{
    Self = 0,
    Right = 1,
    Opposite = 2,
    Left = 3
}

/// <summary>
/// 游戏动作类型。
/// </summary>
public enum ActionType
{
    Draw,
    Discard,
    Chi,
    Pon,
    Kan,
    Kakan,
    Ankan,
    Riichi,
    Tsumo,
    Ron
}

/// <summary>
/// 副露类型。
/// </summary>
public enum MeldType
{
    Chi,
    Pon,
    Kan,
    Kakan,
    Ankan
}
