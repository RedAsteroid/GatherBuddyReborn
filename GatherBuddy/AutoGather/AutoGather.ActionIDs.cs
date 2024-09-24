using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;

namespace GatherBuddy.AutoGather;

public partial class AutoGather
{
    public static class Actions
    {
        public class BaseAction(uint btnId, uint minId, string name, int minLevel, int gpCost)
        {
            private uint MinerID  { get; } = minId;
            private uint BotanyID { get; } = btnId;

            public uint ActionID
            {
                get
                {
                    switch (Player.Job)
                    {
                        case Job.BTN: return BotanyID;
                        case Job.MIN: return MinerID;
                    }

                    return 0;
                }
            }

            public string Name     { get; } = name;
            public int    MinLevel { get; } = minLevel;
            public int    GpCost   { get; } = gpCost;
        }

        public static BaseAction Sneak         = new BaseAction(304,   303,   "隐行",          8,  0);
        public static BaseAction TwelvesBounty = new BaseAction(282,   280,   "十二神加护",       20, 150);
        public static BaseAction Bountiful     = new BaseAction(273,   4073,  "丰收/高产",       24, 100);
        public static BaseAction SolidAge      = new BaseAction(215,   232,   "农夫之智/石工之理",   25, 300);
        public static BaseAction Yield1        = new BaseAction(222,   239,   "天赐收成/莫非王土",   30, 400);
        public static BaseAction Yield2        = new BaseAction(224,   241,   "天赐收成/莫非王土II", 40, 500);
        public static BaseAction Collect       = new BaseAction(815,   240,   "收藏品采集",       50, 0);
        public static BaseAction Scour         = new BaseAction(22186, 22182, "提纯",          50, 0);
        public static BaseAction Brazen        = new BaseAction(22187, 22183, "大胆提纯",        50, 0);
        public static BaseAction Meticulous    = new BaseAction(22188, 22184, "慎重提纯",        50, 0);
        public static BaseAction Scrutiny      = new BaseAction(22189, 22185, "集中检查",        50, 200);
        public static BaseAction Luck          = new BaseAction(4095,  4081,  "开拓者/登山者的眼力",  55, 200);
        public static BaseAction Wise          = new BaseAction(26522, 26521, "理智同兴",        90, 0);
        public static BaseAction GivingLand    = new BaseAction(4590,  4589,  "大地的恩惠",       74, 200);
    }
}
