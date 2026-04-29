using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TileMind.Common.Config
{
    public class ScreenCaptureOptions
    {
        public const string SettingFilePath = @".\settings\screencapturesettings.json";

        //DXGI 适配器索引，通常0表示主显卡
        public int AdapterIndex { get; set; } = 0;
        //显示器索引，通常0表示主显示器
        public int OutputIndex { get; set; } = 0;

        //宝牌指示区
        public Point[] DoraIndicatorArea { get; set; } = new Point[4];

        //牌桌区域，包含所有玩家的手牌、副露区、弃牌区等
        public Point[] TableArea { get; set; } = new Point[4];

        //弃牌区域，包含所有玩家的弃牌区
        public Point[] DiscardPondArea { get; set; } = new Point[4];

        //牌桌信息区域，包含局风、剩牌数等信息显示区域
        public Point[] InfoArea { get; set; } = new Point[4];

        //以下区域通过计算获取
        //本家手牌+副露区
        [JsonIgnore]
        public Point[] SelfHandAndMeldArea { get; set; } = new Point[4];
        //本家弃牌区 
        [JsonIgnore]
        public Point[] SelfDiscardPondArea { get; set; } = new Point[4];

        //下家手牌+副露区
        [JsonIgnore]
        public Point[] RightHandAndMeldArea { get; set; } = new Point[4];
        //下家弃牌区
        [JsonIgnore]
        public Point[] RightDiscardPondArea { get; set; } = new Point[4];

        //对家手牌+副露区
        [JsonIgnore]
        public Point[] OppositeHandAndMeldArea { get; set; } = new Point[4];
        //对家弃牌区
        [JsonIgnore]
        public Point[] OppositeDiscardPondArea { get; set; } = new Point[4];

        //上家手牌+副露区
        [JsonIgnore]
        public Point[] LeftHandAndMeldArea { get; set; } = new Point[4];
        //上家弃牌区
        [JsonIgnore]
        public Point[] LeftDiscardPondArea { get; set; } = new Point[4];
    }
}
