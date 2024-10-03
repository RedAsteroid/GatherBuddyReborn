using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GatherBuddy.Alarms;
using GatherBuddy.AutoGather;
using GatherBuddy.Config;
using GatherBuddy.Enums;
using GatherBuddy.FishTimer;
using GatherBuddy.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using OtterGui;
using OtterGui.Table;
using OtterGui.Widgets;
using Action = System.Action;
using FishRecord = GatherBuddy.FishTimer.FishRecord;
using GatheringType = GatherBuddy.Enums.GatheringType;
using ImRaii = OtterGui.Raii.ImRaii;

namespace GatherBuddy.Gui;

public partial class Interface
{
    private static class ConfigFunctions
    {
        public static Interface _base = null!;

        public static void DrawSetInput(string jobName, string oldName, Action<string> setName)
        {
            var tmp = oldName;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (ImGui.InputText($"{jobName} 套装", ref tmp, 15) && tmp != oldName)
            {
                setName(tmp);
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip($"设置你的 {jobName.ToLowerInvariant()} 套装的名称或编号。");
        }

        private static void DrawCheckbox(string label, string description, bool oldValue, Action<bool> setter)
        {
            if (ImGuiUtil.Checkbox(label, description, oldValue, setter))
                GatherBuddy.Config.Save();
        }

        private static void DrawChatTypeSelector(string label, string description, XivChatType currentValue, Action<XivChatType> setter)
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.DrawChatTypeSelector(label, description, currentValue, setter))
                GatherBuddy.Config.Save();
        }

        // Auto-Gather Config
        public static void DrawAutoGatherBox()
            => DrawCheckbox("启用聚集窗口互动（不推荐禁用此功能）",
                "是否启用自动采集物品。（“仅寻路模式”中建议禁用)",
                GatherBuddy.Config.AutoGatherConfig.DoGathering, b => GatherBuddy.Config.AutoGatherConfig.DoGathering = b);

        public static void DrawGoHomeBox()
            => DrawCheckbox("空闲时回家", "当采集完成或等待限时点时使用 '/li auto' 命令带你回家",
                GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle, b => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle = b);

        public static void DrawAdvancedUnstuckBox()
            => DrawCheckbox("启用避免卡死测试功能",
                "当角色卡住时，使用非寻路的特殊移动技巧",
                GatherBuddy.Config.AutoGatherConfig.UseExperimentalUnstuck,
                b => GatherBuddy.Config.AutoGatherConfig.UseExperimentalUnstuck = b);

        public static void DrawHonkModeBox()
            => DrawCheckbox("采集完成时播放音效", "完成列表采集后结束自动采集并播放音效",
                GatherBuddy.Config.AutoGatherConfig.HonkMode,   b => GatherBuddy.Config.AutoGatherConfig.HonkMode = b);

        public static void DrawMaterialExtraction()
            => DrawCheckbox("启用精炼",
                "你需要安装 YesAlready 并开启: Bothers -> MaterializeDialog",
                GatherBuddy.Config.AutoGatherConfig.DoMaterialize,
                b => GatherBuddy.Config.AutoGatherConfig.DoMaterialize = b);

        public static void DrawMinimumGPGathering()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MinimumGPForGathering;
            if (ImGui.DragInt("采集时的最低 GP", ref tmp, 1, 0, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.MinimumGPForGathering = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawMinimumGPCollectibleRotation()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MinimumGPForCollectableRotation;
            if (ImGui.DragInt("采集时使用技能的最低 GP", ref tmp, 1, 0, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.MinimumGPForCollectableRotation = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawAlwaysUseSolidAgeCollectables()
            => DrawCheckbox("使用 石工之理/农夫之智 时忽略上述选项", "如果满足采集分，无视开始时 GP ，使用 石工之理/农夫之智",
                GatherBuddy.Config.AutoGatherConfig.AlwaysUseSolidAgeCollectables, b => GatherBuddy.Config.AutoGatherConfig.AlwaysUseSolidAgeCollectables = b);

        public static void DrawMinimumGPCollectable()
        {
            ImGui.PushItemWidth(300);
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MinimumGPForCollectable;
            if (ImGui.DragInt("采集收藏品的最小 GP", ref tmp, 1, 0, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.MinimumGPForCollectable = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawMinimumCollectibilityScore()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MinimumCollectibilityScore;
            if (ImGui.DragInt("采集前需达到的收藏价值", ref tmp, 1, 1, 1000))
            {
                GatherBuddy.Config.AutoGatherConfig.MinimumCollectibilityScore = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawGatherIfLastIntegrity()
            => DrawCheckbox(
                "以采集替代耐久耗尽点位丢失",
                "如果收藏价值不能达到要求而耐久即将耗尽，将会在最后以收藏品采集结尾",
                GatherBuddy.Config.AutoGatherConfig.GatherIfLastIntegrity,
                b => GatherBuddy.Config.AutoGatherConfig.GatherIfLastIntegrity = b);

        public static void DrawGatherIfLastIntegrityMinimumCollectibility()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.GatherIfLastIntegrityMinimumCollectibility;
            if (ImGui.DragInt("以采集替代耐久耗尽点位丢失时所需的最低收藏价值", ref tmp, 1, 1000))
            {
                GatherBuddy.Config.AutoGatherConfig.GatherIfLastIntegrityMinimumCollectibility = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawUseFlagBox()
            => DrawCheckbox("禁用地图标记导航",            "是否使用地图标记进行导航（仅限时采集点）",
                GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing, b => GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing = b);

        public static void DrawFarNodeFilterDistance()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance;
            if (ImGui.DragFloat("过远采集点距离过滤", ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(
                "在寻找非空采集点时，GBR将过滤掉任何比这更靠近你的采集点。防止检查已经看到为空的采集点。");
        }

        public static void DrawTimedNodePrecog()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog;
            if (ImGui.DragInt("限时采集点预知（秒）", ref tmp, 1, 0, 600))
            {
                GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("在限时采集点出现前，GBR应该提前多久认为限时采集点出现");
        }

        public static void DrawBYIIBox()
            => DrawCheckbox("使用 高产/丰收 II", "切换是否在采集中使用 高产/丰收 II", GatherBuddy.Config.AutoGatherConfig.BYIIConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.BYIIConfig.UseAction = b);

        public static void DrawBYIIMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.BYIIConfig.MinimumGP;
            if (ImGui.DragInt("高产/丰收 II 最小使用 GP", ref tmp, 1, 100, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.BYIIConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawBYIIMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.BYIIConfig.MaximumGP;
            if (ImGui.DragInt("高产/丰收 II 最大使用 GP", ref tmp, 1, 100, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.BYIIConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawBYIIMinimumIncrease()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.BYIIConfig.GetOptionalProperty<int>("MinimumIncrease");
            if (ImGui.DragInt("Minimum yield increase", ref tmp, 0.1f, 1, 3))
            {
                GatherBuddy.Config.AutoGatherConfig.BYIIConfig.SetOptionalProperty("MinimumIncrease", tmp);
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawBYIIUseWithCrystals()
            => DrawCheckbox("采集水晶时使用", "", GatherBuddy.Config.AutoGatherConfig.BYIIConfig.GetOptionalProperty<bool>("UseWithCystals"),
                b => GatherBuddy.Config.AutoGatherConfig.BYIIConfig.SetOptionalProperty("UseWithCystals", b));

        public static void DrawLuckBox()
            => DrawCheckbox("使用 登山者/开拓者的眼力", "切换是否在采集中使用 登山者/开拓者的眼力。", GatherBuddy.Config.AutoGatherConfig.LuckConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.LuckConfig.UseAction = b);

        public static void DrawLuckMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.LuckConfig.MinimumGP;
            if (ImGui.DragInt("登山者/开拓者的眼力 最小 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.LuckConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawLuckMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.LuckConfig.MaximumGP;
            if (ImGui.DragInt("登山者/开拓者的眼力 最大 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.LuckConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawGivingLandBox()
            => DrawCheckbox("使用 大地的恩惠", "切换是否在采集中使用 大地的恩惠 。", GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.UseAction = b);

        public static void DrawGivingLandMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.MinimumGP;
            if (ImGui.DragInt("大地的恩惠 最小 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawGivingLandMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.MaximumGP;
            if (ImGui.DragInt("大地的恩惠 最大 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.GivingLandConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawUseGivingLandOnCooldown()
            => DrawCheckbox("当 大地的恩惠 冷却完成后，采集任意水晶", "当大地的恩惠可用时，在任意普通采集点上随机采集水晶，不管当前的目标物品是什么。", GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown,
                b => GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown = b);
        public static void DrawTwelvesBountyBox()
            => DrawCheckbox("使用 十二神加护", "切换是否使用 十二神加护 采集水晶。", GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.UseAction = b);

        public static void DrawTwelvesBountyMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.MinimumGP;
            if (ImGui.DragInt("十二神加护 最小 GP", ref tmp, 1, 150, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawTwelvesBountyMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.MaximumGP;
            if (ImGui.DragInt("十二神加护 最大 GP", ref tmp, 1, 150, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawYieldIIMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.MaximumGP;
            if (ImGui.DragInt("莫非王土/天赐收成 II 最大 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawYieldIIMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.MinimumGP;
            if (ImGui.DragInt("莫非王土/天赐收成 II 最小 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawYieldIIUseWithCrystals()
            => DrawCheckbox("在采集水晶时使用", "", GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.GetOptionalProperty<bool>("UseWithCystals"),
                b => GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.SetOptionalProperty("UseWithCystals", b));

        public static void DrawYieldIMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.YieldIConfig.MaximumGP;
            if (ImGui.DragInt("莫非王土/天赐收成 I 最大 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.YieldIConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawYieldIMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.YieldIConfig.MinimumGP;
            if (ImGui.DragInt("莫非王土/天赐收成 I 最小 GP", ref tmp, 1, 200, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.YieldIConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawYieldIUseWithCrystals()
            => DrawCheckbox("在采集水晶时使用", "", GatherBuddy.Config.AutoGatherConfig.YieldIConfig.GetOptionalProperty<bool>("UseWithCystals"),
                b => GatherBuddy.Config.AutoGatherConfig.YieldIConfig.SetOptionalProperty("UseWithCystals", b));

        public static void DrawConditions(AutoGatherConfig.ActionConfig config)
        {
            DrawCheckbox("使用条件", "对技能应用特定的条件",
                config.Conditions.UseConditions,
                b => config.Conditions.UseConditions = b);

            if (config.Conditions.UseConditions)
            {
                if (ImGui.TreeNodeEx("技能条件"))
                {
                    DrawCheckbox("仅在第一步使用", "仅在当前为采集的第一下时使用",
                        config.Conditions.UseOnlyOnFirstStep,
                        b => config.Conditions.UseOnlyOnFirstStep = b);

                    int tmp = (int)config.Conditions.RequiredIntegrity;
                    if (ImGui.DragInt("需要采集点满耐久使用", ref tmp, 0.1f, 1, 10))
                    {
                        config.Conditions.RequiredIntegrity = (uint)tmp;
                        GatherBuddy.Config.Save();
                    }

                    DrawCheckbox("使用采集点类型过滤器", "仅在特定类型的采集点使用",
                        config.Conditions.FilterNodeTypes,
                        b => config.Conditions.FilterNodeTypes = b);

                    if (config.Conditions.FilterNodeTypes)
                    {
                        if (ImGui.TreeNodeEx("采集点过滤器"))
                        {
                            var node = config.Conditions.NodeFilter;
                            DrawNodeFilter("普通",   node.RegularNode);
                            DrawNodeFilter("未知", node.UnspoiledNode);
                            DrawNodeFilter("限时", node.EphemeralNode);
                            DrawNodeFilter("传说", node.LegendaryNode);
                        }
                    }
                }
            }
        }

        private static void DrawNodeFilter(string name, AutoGatherConfig.ActionConditions.NodeFilters.NodeConfig node)
        {
            using var id = ImRaii.PushId(name);
            if (ImGuiUtil.Checkbox($"在 {name} 采集点使用", $"在 {name} 采集点使用该技能", node.Use, b => node.Use = b))
                GatherBuddy.Config.Save();

            if (!node.Use)
                return;

            ImGui.Indent();
            var tmp = node.NodeLevel;

            ImGui.AlignTextToFramePadding();
            ImGuiUtil.TextWrapped("最低采集点等级：");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt($"", ref tmp, 1, 1))
            {
                // make sure the level is within bounds, max 100
                node.NodeLevel = Math.Clamp(tmp, 1, 100);
                GatherBuddy.Config.Save();
            }

            if (ImGuiUtil.Checkbox($"如果 GP 已满，允许更低等级的采集点", "在某些情况下避免 GP 溢出", node.AvoidCap,
                    b => node.AvoidCap = b))
                GatherBuddy.Config.Save();
            ImGui.Unindent();
        }


        public static void DrawYieldIICheckbox()
            => DrawCheckbox("使用 莫非王土/天赐收成 II", "在可用时使用这些技能",
                GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.YieldIIConfig.UseAction = b);

        public static void DrawYieldICheckbox()
            => DrawCheckbox("使用莫非王土/天赐收成 I", "在可用时使用这些技能",
                GatherBuddy.Config.AutoGatherConfig.YieldIConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.YieldIConfig.UseAction = b);

        public static void DrawScrutinyCheckbox()
            => DrawCheckbox("使用 集中检查", "使用 集中检查 采集收藏品", GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.UseAction = b);

        public static void DrawMeticulousCheckbox()
            => DrawCheckbox("使用 慎重提纯", "使用 慎重提纯 采集收藏品",
                GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.UseAction = b);

        public static void DrawScourCheckbox()
            => DrawCheckbox("使用 提纯", "在合适时使用 提纯 采集收藏品",
                GatherBuddy.Config.AutoGatherConfig.ScourConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.ScourConfig.UseAction = b);

        public static void DrawBrazenCheckbox()
            => DrawCheckbox("使用 大胆提纯", "在合适时使用 大胆提纯 采集收藏品",
                GatherBuddy.Config.AutoGatherConfig.BrazenConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.BrazenConfig.UseAction = b);

        public static void DrawSolidAgeCollectablesCheckbox()
            => DrawCheckbox("使用 石工之理/农夫之智 （收藏品）", "使用 石工之理/农夫之智 采集收藏品",
                GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.UseAction = b);

        public static void DrawSolidAgeGatherablesCheckbox()
            => DrawCheckbox("使用 石工之理/农夫之智 （可采集物）", "使用 石工之理/农夫之智 采集收藏品",
                GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.UseAction,
                b => GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.UseAction = b);

        public static void DrawMountUpDistance()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.MountUpDistance;
            if (ImGui.DragFloat("需要上坐骑的距离", ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.MountUpDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("你将会上坐骑来移动到下一个采集点的距离。");
        }

        public static void DrawScrutinyMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.MaximumGP;
            if (ImGui.DragInt("集中检查 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Scrutiny.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawMeticulousMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.MaximumGP;
            if (ImGui.DragInt("慎重提纯 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Meticulous.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawScourMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.ScourConfig.MaximumGP;
            if (ImGui.DragInt("提纯 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Scour.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.ScourConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawBrazenMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.BrazenConfig.MaximumGP;
            if (ImGui.DragInt("大胆提纯 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Brazen.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.BrazenConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawSolidAgeCollectablesMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.MaximumGP;
            if (ImGui.DragInt("石工之理/农夫之智 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.SolidAge.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawSolidAgeGatherablesMaxGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.MaximumGP;
            if (ImGui.DragInt("石工之理/农夫之智 最大 GP", ref tmp, 1, AutoGather.AutoGather.Actions.SolidAge.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }
        public static void DrawSolidAgeGatherablesUseWithCrystals()
            => DrawCheckbox("当采集水晶时使用", "", GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.GetOptionalProperty<bool>("UseWithCystals"),
                b => GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.SetOptionalProperty("UseWithCystals", b));

        public static void DrawSolidAgeGatherablesMinYield()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.GetOptionalProperty<int>("MinimumYield");
            if (ImGui.DragInt("最小 莫非王土/天赐收成", ref tmp, 0.1f, 1, 20))
            {
                GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.SetOptionalProperty("MinimumYield", tmp);
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawScrutinyMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.MinimumGP;
            if (ImGui.DragInt("集中检查 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Scrutiny.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.ScrutinyConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawMeticulousMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.MinimumGP;
            if (ImGui.DragInt("慎重提纯 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Meticulous.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.MeticulousConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawScourMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.ScourConfig.MinimumGP;
            if (ImGui.DragInt("提纯 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Scour.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.ScourConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawBrazenMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.BrazenConfig.MinimumGP;
            if (ImGui.DragInt("大胆提纯 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.Brazen.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.BrazenConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawSolidAgeCollectablesMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.MinimumGP;
            if (ImGui.DragInt("石工之理/农夫之智 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.SolidAge.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.SolidAgeCollectablesConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawSolidAgeGatherablesMinGp()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.MinimumGP;
            if (ImGui.DragInt("石工之理/农夫之智 最小 GP", ref tmp, 1, AutoGather.AutoGather.Actions.SolidAge.GpCost, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawCordialCheckbox()
            => DrawCheckbox(
                "使用 强心剂",
                "当可用时使用 强心剂，如果 强心剂 不在冷却中且 GP 在特定范围内。",
                GatherBuddy.Config.AutoGatherConfig.CordialConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.CordialConfig.UseConsumable = b);

        public static void DrawCordialMinGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.CordialConfig.MinimumGP;
            if (ImGui.DragInt("强心剂 最小 GP", ref tmp, 1, 0, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.CordialConfig.MinimumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawCordialMaxGP()
        {
            int tmp = (int)GatherBuddy.Config.AutoGatherConfig.CordialConfig.MaximumGP;
            if (ImGui.DragInt("强心剂 最大 GP", ref tmp, 1, 1000, 30000))
            {
                GatherBuddy.Config.AutoGatherConfig.CordialConfig.MaximumGP = (uint)tmp;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawFoodCheckbox()
            => DrawCheckbox(
                "使用 食物",
                "使用 食物 如果物品可用且角色无该状态。",
                GatherBuddy.Config.AutoGatherConfig.FoodConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.FoodConfig.UseConsumable = b);

        public static void DrawPotionCheckbox()
            => DrawCheckbox(
                "使用 药剂",
                "使用 药剂 如果物品可用且角色无该状态。",
                GatherBuddy.Config.AutoGatherConfig.PotionConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.PotionConfig.UseConsumable = b);

        public static void DrawManualCheckbox()
            => DrawCheckbox(
                "使用 指南",
                "使用 指南 如果物品可用且角色无该状态。",
                GatherBuddy.Config.AutoGatherConfig.ManualConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.ManualConfig.UseConsumable = b);

        public static void DrawSquadronManualCheckbox()
            => DrawCheckbox(
                "使用 军用指南",
                "使用 军用指南 如果物品可用且角色无该状态。",
                GatherBuddy.Config.AutoGatherConfig.SquadronManualConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.SquadronManualConfig.UseConsumable = b);

        public static void DrawSquadronPassCheckbox()
            => DrawCheckbox(
                "使用 传送网使用优惠券",
                "使用 传送网使用优惠券 如果物品可用且角色无该状态。",
                GatherBuddy.Config.AutoGatherConfig.SquadronPassConfig.UseConsumable,
                b => GatherBuddy.Config.AutoGatherConfig.SquadronPassConfig.UseConsumable = b);

        public static void DrawAntiStuckCooldown()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetCooldown;
            if (ImGui.DragFloat("防卡冷却", ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetCooldown = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("你卡死之后多少秒导航系统将会重置。");
        }

        public static void DrawForceWalkingBox()
            => DrawCheckbox("强制行走",                      "强制以行走替代骑乘前往采集点。",
                GatherBuddy.Config.AutoGatherConfig.ForceWalking, b => GatherBuddy.Config.AutoGatherConfig.ForceWalking = b);

        public static void DrawStuckThreshold()
        {
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetThreshold;
            if (ImGui.DragFloat("卡死阈值", ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("多少秒之后导航系统将会判定你是否卡死。");
        }

        public static void DrawSortingMethodCombo()
        {
            var v = GatherBuddy.Config.AutoGatherConfig.SortingMethod;
            ImGui.SetNextItemWidth(SetInputWidth);

            using var combo = ImRaii.Combo("物品排序方法", v.ToString());
            ImGuiUtil.HoverTooltip("内部排序物品时所使用的方法");
            if (!combo)
                return;

            if (ImGui.Selectable(AutoGatherConfig.SortingType.Location.ToString(), v == AutoGatherConfig.SortingType.Location))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.Location;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(AutoGatherConfig.SortingType.None.ToString(), v == AutoGatherConfig.SortingType.None))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.None;
                GatherBuddy.Config.Save();
            }
        }

        // General Config
        public static void DrawOpenOnStartBox()
            => DrawCheckbox("启动时打开设置界面",
                "切换在启动游戏后 GatherBuddy 界面是否可见。",
                GatherBuddy.Config.OpenOnStart, b => GatherBuddy.Config.OpenOnStart = b);

        public static void DrawLockPositionBox()
            => DrawCheckbox("锁定设置界面移动",
                "切换 GatherBuddy 界面移动是否被锁定。",
                GatherBuddy.Config.MainWindowLockPosition, b =>
                {
                    GatherBuddy.Config.MainWindowLockPosition = b;
                    _base.UpdateFlags();
                });

        public static void DrawLockResizeBox()
            => DrawCheckbox("锁定设置界面尺寸",
                "切换 GatherBuddy 界面尺寸是否被锁定。",
                GatherBuddy.Config.MainWindowLockResize, b =>
                {
                    GatherBuddy.Config.MainWindowLockResize = b;
                    _base.UpdateFlags();
                });

        public static void DrawRespectEscapeBox()
            => DrawCheckbox("ESC 关闭主界面",
                "切换聚焦主窗口时按 ESC 键是否关闭主窗口。",
                GatherBuddy.Config.CloseOnEscape, b =>
                {
                    GatherBuddy.Config.CloseOnEscape = b;
                    _base.UpdateFlags();
                });

        public static void DrawGearChangeBox()
            => DrawCheckbox("启用套装切换",
                "切换是否根据采集点自动切换到正确的职业套装。\n使用采矿工套装，园艺工套装以及捕鱼人套装。",
                GatherBuddy.Config.UseGearChange, b => GatherBuddy.Config.UseGearChange = b);

        public static void DrawTeleportBox()
            => DrawCheckbox("启用传送",
                "切换是否自动传送到所选的采集点。",
                GatherBuddy.Config.UseTeleport, b => GatherBuddy.Config.UseTeleport = b);

        public static void DrawMapOpenBox()
            => DrawCheckbox("打开带位置的地图",
                "切换是否自动打开所选的采集点所在地区的地图且高亮其采集位置。",
                GatherBuddy.Config.UseCoordinates, b => GatherBuddy.Config.UseCoordinates = b);

        public static void DrawPlaceMarkerBox()
            => DrawCheckbox("在地图上放置旗帜标记",
                "切换是否自动在所选采集点的近似位置放一个红色小旗标记且不打开地图。",
                GatherBuddy.Config.UseFlag, b => GatherBuddy.Config.UseFlag = b);

        public static void DrawMapMarkerPrintBox()
            => DrawCheckbox("打印地图位置",
                "切换是否自动在聊天栏中发送一个所选采集点近似位置的地图链接。",
                GatherBuddy.Config.WriteCoordinates, b => GatherBuddy.Config.WriteCoordinates = b);

        public static void DrawPlaceWaymarkBox()
            => DrawCheckbox("放置自定义场景标记",
                "切换是否放置你为确定位置手动设置的场景标记。",
                GatherBuddy.Config.PlaceCustomWaymarks, b => GatherBuddy.Config.PlaceCustomWaymarks = b);

        public static void DrawPrintUptimesBox()
            => DrawCheckbox("采集时打印采集点出现时间",
                "打印你尝试 /gather 的限时采集点的出现时间到聊天栏。",
                GatherBuddy.Config.PrintUptime, b => GatherBuddy.Config.PrintUptime = b);

        public static void DrawSkipTeleportBox()
            => DrawCheckbox("跳过附近传送",
                "如果你已经在同一地图且比所选的以太之光更靠近目标，跳过传送。",
                GatherBuddy.Config.SkipTeleportIfClose, b => GatherBuddy.Config.SkipTeleportIfClose = b);

        public static void DrawShowStatusLineBox()
            => DrawCheckbox("显示状态线",
                "在可采集物和鱼类表下面显示一条状态线。",
                GatherBuddy.Config.ShowStatusLine, v => GatherBuddy.Config.ShowStatusLine = v);

        public static void DrawHideClippyBox()
            => DrawCheckbox("隐藏采集助手按钮",
                "永远隐藏可采集物和鱼类标签页中的采集助手按钮。",
                GatherBuddy.Config.HideClippy, v => GatherBuddy.Config.HideClippy = v);

        private const string ChatInformationString =
            "注意，无论选择的频道是什么，消息只会被打印到你的聊天日志中"
          + " - 其他人不会看到你“说”的话。";

        public static void DrawPrintTypeSelector()
            => DrawChatTypeSelector("消息的聊天类型",
                "用于打印由 GatherBuddy 发出的常规消息的聊天类型。\n"
              + ChatInformationString,
                GatherBuddy.Config.ChatTypeMessage, t => GatherBuddy.Config.ChatTypeMessage = t);

        public static void DrawErrorTypeSelector()
            => DrawChatTypeSelector("错误的聊天类型",
                "用于打印由 GatherBuddy 发出的错误消息的聊天类型。\n"
              + ChatInformationString,
                GatherBuddy.Config.ChatTypeError, t => GatherBuddy.Config.ChatTypeError = t);

        public static void DrawContextMenuBox()
            => DrawCheckbox("添加游戏内上下文菜单",
                "为可采集物的游戏内右键菜单添加“采集”条目。",
                GatherBuddy.Config.AddIngameContextMenus, b =>
                {
                    GatherBuddy.Config.AddIngameContextMenus = b;
                    if (b)
                        _plugin.ContextMenu.Enable();
                    else
                        _plugin.ContextMenu.Disable();
                });

        public static void DrawPreferredJobSelect()
        {
            var v       = GatherBuddy.Config.PreferredGatheringType;
            var current = v == GatheringType.Multiple ? "无偏好" : v.ToString();
            ImGui.SetNextItemWidth(SetInputWidth);
            using var combo = ImRaii.Combo("偏好职业", current);
            ImGuiUtil.HoverTooltip(
                "在采集采矿工和园艺工都可以采集物品时，选择你的职业偏好。\n"
              + "当两者都可以采集物品时，这实际上将通常的采集命令转换为 /gathermin 或 /gatherbtn，"
              + "即便连续尝试，也忽略其他选项。");
            if (!combo)
                return;

            if (ImGui.Selectable("无偏好", v == GatheringType.Multiple) && v != GatheringType.Multiple)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Multiple;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(GatheringType.Miner.ToString(), v == GatheringType.Miner) && v != GatheringType.Miner)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Miner;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(GatheringType.Botanist.ToString(), v == GatheringType.Botanist) && v != GatheringType.Botanist)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Botanist;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawPrintClipboardBox()
            => DrawCheckbox("打印剪贴板信息",
                "当你保存一个对象到剪贴板时，不论是否可行都尝试打印到聊天栏中。",
                GatherBuddy.Config.PrintClipboardMessages, b => GatherBuddy.Config.PrintClipboardMessages = b);

        // Weather Tab
        public static void DrawWeatherTabNamesBox()
            => DrawCheckbox("在天气标签页显示名称",
                "切换是否在天气标签页的表格中显示名称，或者仅仅显示在鼠标悬浮时会显示名字的图标。",
                GatherBuddy.Config.ShowWeatherNames, b => GatherBuddy.Config.ShowWeatherNames = b);

        // Alarms
        public static void DrawAlarmToggle()
            => DrawCheckbox("启用闹钟", "切换所有闹钟开关", GatherBuddy.Config.AlarmsEnabled,
                b =>
                {
                    if (b)
                        _plugin.AlarmManager.Enable();
                    else
                        _plugin.AlarmManager.Disable();
                });

        private static bool _gatherDebug = false;

        public static void DrawAlarmsInDutyToggle()
            => DrawCheckbox("在副本任务中启用闹钟", "设置当你在副本任务中时闹钟是否应该触发。",
                GatherBuddy.Config.AlarmsInDuty,     b => GatherBuddy.Config.AlarmsInDuty = b);

        public static void DrawAlarmsOnlyWhenLoggedInToggle()
            => DrawCheckbox("仅在游戏内启用闹钟",  "设置当你未登入任何角色时闹钟是否应该触发。",
                GatherBuddy.Config.AlarmsOnlyWhenLoggedIn, b => GatherBuddy.Config.AlarmsOnlyWhenLoggedIn = b);

        private static void DrawAlarmPicker(string label, string description, Sounds current, Action<Sounds> setter)
        {
            var cur = (int)current;
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo(label, ref cur, AlarmCache.SoundIdNames))
                setter((Sounds)cur);
            ImGuiUtil.HoverTooltip(description);
        }

        public static void DrawWeatherAlarmPicker()
            => DrawAlarmPicker("天气改变闹钟提醒", "选择一个在每8个艾欧泽亚时的正常天气改变的时候播放的声音。",
                GatherBuddy.Config.WeatherAlarm,       _plugin.AlarmManager.SetWeatherAlarm);

        public static void DrawHourAlarmPicker()
            => DrawAlarmPicker("艾欧泽亚时改变闹钟提醒", "选择一个在当前艾欧泽亚时改变的时候播放的声音。",
                GatherBuddy.Config.HourAlarm,              _plugin.AlarmManager.SetHourAlarm);

        // Fish Timer
        public static void DrawFishTimerBox()
            => DrawCheckbox("显示捕鱼计时器",
                "切换是否在钓鱼时显示捕鱼计时器。",
                GatherBuddy.Config.ShowFishTimer, b => GatherBuddy.Config.ShowFishTimer = b);

        public static void DrawFishTimerEditBox()
            => DrawCheckbox("编辑捕鱼计时器",
                "启用编辑捕鱼计时器窗口。",
                GatherBuddy.Config.FishTimerEdit, b => GatherBuddy.Config.FishTimerEdit = b);

        public static void DrawFishTimerClickthroughBox()
            => DrawCheckbox("捕鱼计时器鼠标穿透",
                "允许鼠标穿透捕鱼计时器，并禁用其上下文菜单。",
                GatherBuddy.Config.FishTimerClickthrough, b => GatherBuddy.Config.FishTimerClickthrough = b);

        public static void DrawFishTimerHideBox()
            => DrawCheckbox("在捕鱼计时器中隐藏未捕获的鱼",
                "在捕鱼计时器窗口中隐藏所有未被给定的钓组和钓饵的组合记录的鱼。",
                GatherBuddy.Config.HideUncaughtFish, b => GatherBuddy.Config.HideUncaughtFish = b);

        public static void DrawFishTimerHideBox2()
            => DrawCheckbox("在捕鱼计时器中隐藏不可捕获的鱼",
                "在捕鱼计时器窗口中隐藏所有前置条件未满足的鱼，例如 捕鱼人之识 或 钓组。",
                GatherBuddy.Config.HideUnavailableFish, b => GatherBuddy.Config.HideUnavailableFish = b);

        public static void DrawFishTimerUptimesBox()
            => DrawCheckbox("在捕鱼计时器中显示出现时间",
                "在捕鱼计时器窗口中显示限时鱼的出现时间。",
                GatherBuddy.Config.ShowFishTimerUptimes, b => GatherBuddy.Config.ShowFishTimerUptimes = b);

        public static void DrawKeepRecordsBox()
            => DrawCheckbox("保持捕鱼记录",
                "在你的计算机上存储捕鱼记录。这对于捕鱼计时器窗口的咬钩计时是必要的。",
                GatherBuddy.Config.StoreFishRecords, b => GatherBuddy.Config.StoreFishRecords = b);

        public static void DrawFishTimerScale()
        {
            var value = GatherBuddy.Config.FishTimerScale / 1000f;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragFloat("捕鱼计时器咬钩计时尺寸", ref value, 0.1f, FishRecord.MinBiteTime / 500f,
                FishRecord.MaxBiteTime / 1000f,
                "%2.3f 秒");

            ImGuiUtil.HoverTooltip("捕鱼计时器窗口咬钩计时的尺寸取决于这个值。\n"
              + "如果你的咬钩时间超过该值，进度条和咬钩窗口将不显示。\n"
              + "你应该把这个值保持在你的最高咬钩窗口，且尽可能低。通常来说40秒够了。");

            if (!ret)
                return;

            var newValue = (ushort)Math.Clamp((int)(value * 1000f + 0.9), FishRecord.MinBiteTime * 2, FishRecord.MaxBiteTime);
            if (newValue == GatherBuddy.Config.FishTimerScale)
                return;

            GatherBuddy.Config.FishTimerScale = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawFishTimerIntervals()
        {
            int value = GatherBuddy.Config.ShowSecondIntervals;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragInt("捕鱼计时器间隔分隔符", ref value, 0.01f, 0, 16);
            ImGuiUtil.HoverTooltip("捕鱼计时器窗口可以显示若干间隔线和0到16之间的相应的秒数。\n"
              + "设置为0关闭此功能。");
            if (!ret)
                return;

            var newValue = (byte)Math.Clamp(value, 0, 16);
            if (newValue == GatherBuddy.Config.ShowSecondIntervals)
                return;

            GatherBuddy.Config.ShowSecondIntervals = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawHideFishPopupBox()
            => DrawCheckbox("隐藏捕获弹窗",
                "阻止弹出显示你捕获的鱼以及它的大小、数量和质量的弹窗。",
                GatherBuddy.Config.HideFishSizePopup, b => GatherBuddy.Config.HideFishSizePopup = b);


        // Spearfishing Helper
        public static void DrawSpearfishHelperBox()
            => DrawCheckbox("显示刺鱼助手",
                "切换是否在刺鱼时显示刺鱼助手。",
                GatherBuddy.Config.ShowSpearfishHelper, b => GatherBuddy.Config.ShowSpearfishHelper = b);

        public static void DrawSpearfishNamesBox()
            => DrawCheckbox("显示鱼名悬浮窗",
                "切换是否在刺鱼窗口中显示被识别的鱼的名称。",
                GatherBuddy.Config.ShowSpearfishNames, b => GatherBuddy.Config.ShowSpearfishNames = b);

        public static void DrawAvailableSpearfishBox()
            => DrawCheckbox("显示可捕获的鱼的列表",
                "切换是否在刺鱼窗口的一侧显示当前刺鱼点可捕获的鱼的列表。",
                GatherBuddy.Config.ShowAvailableSpearfish, b => GatherBuddy.Config.ShowAvailableSpearfish = b);

        public static void DrawSpearfishSpeedBox()
            => DrawCheckbox("在悬浮窗中显示鱼的速度",
                "切换是否在刺鱼窗口中鱼的名字旁额外显示鱼的速度。",
                GatherBuddy.Config.ShowSpearfishSpeed, b => GatherBuddy.Config.ShowSpearfishSpeed = b);

        public static void DrawSpearfishCenterLineBox()
            => DrawCheckbox("显示中心线",
                "切换是否在刺鱼窗口中显示一条从鱼叉的中心向上的直线。",
                GatherBuddy.Config.ShowSpearfishCenterLine, b => GatherBuddy.Config.ShowSpearfishCenterLine = b);

        public static void DrawSpearfishIconsAsTextBox()
            => DrawCheckbox("以文本方式显示速度和尺寸",
                "切换是否以文本替代图标显示可捕获的鱼的速度和大小。",
                GatherBuddy.Config.ShowSpearfishListIconsAsText, b => GatherBuddy.Config.ShowSpearfishListIconsAsText = b);

        public static void DrawSpearfishFishNameFixed()
            => DrawCheckbox("在固定位置显示鱼的名称",
                "切换是在移动的鱼上显示已识别的鱼的名称还是在一个固定位置上显示。",
                GatherBuddy.Config.FixNamesOnPosition, b => GatherBuddy.Config.FixNamesOnPosition = b);

        public static void DrawSpearfishFishNamePercentage()
        {
            if (!GatherBuddy.Config.FixNamesOnPosition)
                return;

            var tmp = (int)GatherBuddy.Config.FixNamesPercentage;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (!ImGui.DragInt("鱼的名称的位置百分比", ref tmp, 0.1f, 0, 100, "%i%%"))
                return;

            tmp = Math.Clamp(tmp, 0, 100);
            if (tmp == GatherBuddy.Config.FixNamesPercentage)
                return;

            GatherBuddy.Config.FixNamesPercentage = (byte)tmp;
            GatherBuddy.Config.Save();
        }

        // Gather Window
        public static void DrawShowGatherWindowBox()
            => DrawCheckbox("显示采集窗口",
                "显示一个包含选定的可采集物和它们的出现时间的小窗口。",
                GatherBuddy.Config.ShowGatherWindow, b => GatherBuddy.Config.ShowGatherWindow = b);

        public static void DrawGatherWindowAnchorBox()
            => DrawCheckbox("以左下角锚定采集窗口",
                "让采集窗口向顶部增长并从顶部收缩。",
                GatherBuddy.Config.GatherWindowBottomAnchor, b => GatherBuddy.Config.GatherWindowBottomAnchor = b);

        public static void DrawGatherWindowTimersBox()
            => DrawCheckbox("显示采集窗口计时器",
                "在采集窗口中显示可采集物的出现时间。",
                GatherBuddy.Config.ShowGatherWindowTimers, b => GatherBuddy.Config.ShowGatherWindowTimers = b);

        public static void DrawGatherWindowAlarmsBox()
            => DrawCheckbox("在采集窗口中显示激活的闹钟",
                "额外显示激活的闹钟作为采集窗口的最后一个预设，遵守该窗口的常规规则。",
                GatherBuddy.Config.ShowGatherWindowAlarms, b =>
                {
                    GatherBuddy.Config.ShowGatherWindowAlarms = b;
                    _plugin.GatherWindowManager.SetShowGatherWindowAlarms(b);
                });

        public static void DrawSortGatherWindowBox()
            => DrawCheckbox("在采集窗口中以出现时间排序",
                "按出现时间对采集窗口中所选的物品进行排序。",
                GatherBuddy.Config.SortGatherWindowByUptime, b => GatherBuddy.Config.SortGatherWindowByUptime = b);

        public static void DrawGatherWindowShowOnlyAvailableBox()
            => DrawCheckbox("仅显示可采集物品",
                "仅显示采集窗口设置中的当前可采集的物品。",
                GatherBuddy.Config.ShowGatherWindowOnlyAvailable, b => GatherBuddy.Config.ShowGatherWindowOnlyAvailable = b);

        public static void DrawHideGatherWindowInDutyBox()
            => DrawCheckbox("副本任务中隐藏采集窗口",
                "进入任何副本任务后隐藏采集窗口。",
                GatherBuddy.Config.HideGatherWindowInDuty, b => GatherBuddy.Config.HideGatherWindowInDuty = b);

        public static void DrawGatherWindowHoldKey()
        {
            DrawCheckbox("仅在按住快捷键时显示采集窗口",
                "仅在你按住你所选的快捷键时显示采集窗口。",
                GatherBuddy.Config.OnlyShowGatherWindowHoldingKey, b => GatherBuddy.Config.OnlyShowGatherWindowHoldingKey = b);

            if (!GatherBuddy.Config.OnlyShowGatherWindowHoldingKey)
                return;

            ImGui.SetNextItemWidth(SetInputWidth);
            Widget.KeySelector("需要按住的快捷键", "设置一个用来按下保持窗口可见的快捷键。",
                GatherBuddy.Config.GatherWindowHoldKey,
                k => GatherBuddy.Config.GatherWindowHoldKey = k, Configuration.ValidKeys);
        }

        public static void DrawGatherWindowLockBox()
            => DrawCheckbox("锁定采集窗口位置",
                "防止拖动移动采集窗口。",
                GatherBuddy.Config.LockGatherWindow, b => GatherBuddy.Config.LockGatherWindow = b);


        public static void DrawGatherWindowHotkeyInput()
        {
            if (Widget.ModifiableKeySelector("打开采集窗口的快捷键", "设置一个用来打开采集窗口的快捷键。", SetInputWidth,
                    GatherBuddy.Config.GatherWindowHotkey, k => GatherBuddy.Config.GatherWindowHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }

        public static void DrawMainInterfaceHotkeyInput()
        {
            if (Widget.ModifiableKeySelector("打开主界面的快捷键", "设置一个用来打开 GatherBuddy 主界面的快捷键Set a hotkey to open the main GatherBuddy interface.",
                    SetInputWidth,
                    GatherBuddy.Config.MainInterfaceHotkey, k => GatherBuddy.Config.MainInterfaceHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }


        public static void DrawGatherWindowDeleteModifierInput()
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.ModifierSelector("右键删除物品的修饰键",
                    "设置用来在右键单击采集窗口中的物品以删除它们时配合使用的修饰键。",
                    GatherBuddy.Config.GatherWindowDeleteModifier, k => GatherBuddy.Config.GatherWindowDeleteModifier = k))
                GatherBuddy.Config.Save();
        }


        public static void DrawAetherytePreference()
        {
            var tmp     = GatherBuddy.Config.AetherytePreference == AetherytePreference.Cost;
            var oldPref = GatherBuddy.Config.AetherytePreference;
            if (ImGui.RadioButton("偏好更便宜的以太之光", tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Cost;
            var hovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            if (ImGui.RadioButton("偏好更少跑图时长", !tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Distance;
            hovered |= ImGui.IsItemHovered();
            if (hovered)
                ImGui.SetTooltip(
                    "指定你是更喜欢离目标更近的以太之光（更少的跑图时间）"
                  + " 还是在为了指定物品扫描所有可采集点后传送花费更便宜的以太之光。"
                  + " 只有在物品没有时间限制并且有多个可采集点时有效。");

            if (oldPref != GatherBuddy.Config.AetherytePreference)
            {
                GatherBuddy.UptimeManager.ResetLocations();
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawAlarmFormatInput()
            => DrawFormatInput("闹钟的消息格式",
                "保持为空以不输出任何消息。\n"
              + "可替换：\n"
              + "- {Alarm} 带括号的闹钟名称。\n"
              + "- {Item} 物品链接。\n"
              + "- {Offset} 以秒为单位的闹钟偏移量。\n"
              + "- {DurationString} “还需要等待...”或“还能够维持...”。\n"
              + "- {Location} 地图旗帜链接与位置名称。",
                GatherBuddy.Config.AlarmFormat, Configuration.DefaultAlarmFormat, s => GatherBuddy.Config.AlarmFormat = s);

        public static void DrawIdentifiedGatherableFormatInput()
            => DrawFormatInput("被识别的可采集物的消息格式",
                "保持为空以不输出任何消息。\n"
              + "可替换：\n"
              + "- {Input} 输入的搜索文本。\n"
              + "- {Item} 物品链接。",
                GatherBuddy.Config.IdentifiedGatherableFormat, Configuration.DefaultIdentifiedGatherableFormat,
                s => GatherBuddy.Config.IdentifiedGatherableFormat = s);
    }

    private void DrawConfigTab()
    {
        using var id  = ImRaii.PushId("设置");
        using var tab = ImRaii.TabItem("设置");
        ImGuiUtil.HoverTooltip("根据个人需求设置独属于你的 GatherBuddy 。\n"
          + "If you treat him well, he might even become a real boy.");

        if (!tab)
            return;

        using var child = ImRaii.Child("设置标签页");
        if (!child)
            return;

        if (ImGui.CollapsingHeader("自动采集设置"))
        {
            if (ImGui.TreeNodeEx("通用##autoGeneral"))
            {
                ConfigFunctions.DrawHonkModeBox();
                AutoGatherUI.DrawMountSelector();
                ConfigFunctions.DrawMountUpDistance();
                ConfigFunctions.DrawMinimumGPGathering();
                ConfigFunctions.DrawSortingMethodCombo();
                ConfigFunctions.DrawUseGivingLandOnCooldown();
                ConfigFunctions.DrawGoHomeBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("收藏品"))
            {
                ConfigFunctions.DrawMinimumGPCollectable();
                ConfigFunctions.DrawMinimumGPCollectibleRotation();
                ConfigFunctions.DrawAlwaysUseSolidAgeCollectables();
                ConfigFunctions.DrawMinimumCollectibilityScore();
                ConfigFunctions.DrawGatherIfLastIntegrity();

                if (GatherBuddy.Config.AutoGatherConfig.GatherIfLastIntegrity)
                    ConfigFunctions.DrawGatherIfLastIntegrityMinimumCollectibility();

                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("采集技能"))
            {
                if (ImGui.TreeNodeEx("高产/丰收 II"))
                {
                    ConfigFunctions.DrawBYIIBox();
                    ConfigFunctions.DrawBYIIMinGP();
                    ConfigFunctions.DrawBYIIMaxGP();
                    ConfigFunctions.DrawBYIIMinimumIncrease();
                    ConfigFunctions.DrawBYIIUseWithCrystals();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.BYIIConfig);
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("莫非王土/天赐收成 II"))
                {
                    ConfigFunctions.DrawYieldIICheckbox();
                    ConfigFunctions.DrawYieldIIMinGP();
                    ConfigFunctions.DrawYieldIIMaxGP();
                    ConfigFunctions.DrawYieldIIUseWithCrystals();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.YieldIIConfig);
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("莫非王土/天赐收成 I"))
                {
                    ConfigFunctions.DrawYieldICheckbox();
                    ConfigFunctions.DrawYieldIMinGP();
                    ConfigFunctions.DrawYieldIMaxGP();
                    ConfigFunctions.DrawYieldIUseWithCrystals();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.YieldIConfig);
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("石工之理/农夫之智"))
                {
                    ConfigFunctions.DrawSolidAgeGatherablesCheckbox();
                    ConfigFunctions.DrawSolidAgeGatherablesMinGp();
                    ConfigFunctions.DrawSolidAgeGatherablesMaxGp();
                    ConfigFunctions.DrawSolidAgeGatherablesMinYield();
                    ConfigFunctions.DrawSolidAgeGatherablesUseWithCrystals();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.SolidAgeGatherablesConfig);
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("登山者/开拓者的眼力"))
                {
                    ConfigFunctions.DrawLuckBox();
                    ConfigFunctions.DrawLuckMinGP();
                    ConfigFunctions.DrawLuckMaxGP();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.LuckConfig);
                    ImGui.TreePop();
                }
                if (ImGui.TreeNodeEx("大地的恩惠"))
                {
                    ConfigFunctions.DrawGivingLandBox();
                    ConfigFunctions.DrawGivingLandMinGP();
                    ConfigFunctions.DrawGivingLandMaxGP();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.GivingLandConfig);
                    ImGui.TreePop();
                }
                if (ImGui.TreeNodeEx("十二神加护"))
                {
                    ConfigFunctions.DrawTwelvesBountyBox();
                    ConfigFunctions.DrawTwelvesBountyMinGP();
                    ConfigFunctions.DrawTwelvesBountyMaxGP();
                    ConfigFunctions.DrawConditions(GatherBuddy.Config.AutoGatherConfig.TwelvesBountyConfig);
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("收藏品技能"))
            {
                if (ImGui.TreeNodeEx("提纯"))
                {
                    ConfigFunctions.DrawScourCheckbox();
                    ConfigFunctions.DrawScourMinGp();
                    ConfigFunctions.DrawScourMaxGp();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("大胆提纯"))
                {
                    ConfigFunctions.DrawBrazenCheckbox();
                    ConfigFunctions.DrawBrazenMinGp();
                    ConfigFunctions.DrawBrazenMaxGp();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("慎重提纯"))
                {
                    ConfigFunctions.DrawMeticulousCheckbox();
                    ConfigFunctions.DrawMeticulousMinGp();
                    ConfigFunctions.DrawMeticulousMaxGp();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("集中检查"))
                {
                    ConfigFunctions.DrawScrutinyCheckbox();
                    ConfigFunctions.DrawScrutinyMinGp();
                    ConfigFunctions.DrawScrutinyMaxGp();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("石工之理/农夫之智"))
                {
                    ConfigFunctions.DrawSolidAgeCollectablesCheckbox();
                    ConfigFunctions.DrawSolidAgeCollectablesMinGp();
                    ConfigFunctions.DrawSolidAgeCollectablesMaxGp();
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("消耗品"))
            {
                if (ImGui.TreeNodeEx("强心剂"))
                {
                    ConfigFunctions.DrawCordialCheckbox();
                    ConfigFunctions.DrawCordialMinGP();
                    ConfigFunctions.DrawCordialMaxGP();
                    AutoGatherUI.DrawCordialSelector();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("食物"))
                {
                    ConfigFunctions.DrawFoodCheckbox();
                    AutoGatherUI.DrawFoodSelector();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("药剂"))
                {
                    ConfigFunctions.DrawPotionCheckbox();
                    AutoGatherUI.DrawPotionSelector();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("指南"))
                {
                    ConfigFunctions.DrawManualCheckbox();
                    AutoGatherUI.DrawManualSelector();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("军用指南"))
                {
                    ConfigFunctions.DrawSquadronManualCheckbox();
                    AutoGatherUI.DrawSquadronManualSelector();
                    ImGui.TreePop();
                }

                if (ImGui.TreeNodeEx("传送网使用优惠券"))
                {
                    ConfigFunctions.DrawSquadronPassCheckbox();
                    AutoGatherUI.DrawSquadronPassSelector();
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("高级"))
            {
                ConfigFunctions.DrawAutoGatherBox();
                ConfigFunctions.DrawUseFlagBox();
                ConfigFunctions.DrawForceWalkingBox();
                ConfigFunctions.DrawAdvancedUnstuckBox();
                ConfigFunctions.DrawMaterialExtraction();
                ConfigFunctions.DrawAntiStuckCooldown();
                ConfigFunctions.DrawStuckThreshold();
                ConfigFunctions.DrawTimedNodePrecog();
                ImGui.TreePop();
            }
        }

        if (ImGui.CollapsingHeader("通用设置"))
        {
            if (ImGui.TreeNodeEx("采集命令"))
            {
                ConfigFunctions.DrawPreferredJobSelect();
                ConfigFunctions.DrawGearChangeBox();
                ConfigFunctions.DrawTeleportBox();
                ConfigFunctions.DrawMapOpenBox();
                ConfigFunctions.DrawPlaceMarkerBox();
                ConfigFunctions.DrawPlaceWaymarkBox();
                ConfigFunctions.DrawAetherytePreference();
                ConfigFunctions.DrawSkipTeleportBox();
                ConfigFunctions.DrawContextMenuBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("套装名"))
            {
                ConfigFunctions.DrawSetInput("采矿工",    GatherBuddy.Config.MinerSetName,    s => GatherBuddy.Config.MinerSetName    = s);
                ConfigFunctions.DrawSetInput("园艺工", GatherBuddy.Config.BotanistSetName, s => GatherBuddy.Config.BotanistSetName = s);
                ConfigFunctions.DrawSetInput("捕鱼人",   GatherBuddy.Config.FisherSetName,   s => GatherBuddy.Config.FisherSetName   = s);
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("闹钟"))
            {
                ConfigFunctions.DrawAlarmToggle();
                ConfigFunctions.DrawAlarmsInDutyToggle();
                ConfigFunctions.DrawAlarmsOnlyWhenLoggedInToggle();
                ConfigFunctions.DrawWeatherAlarmPicker();
                ConfigFunctions.DrawHourAlarmPicker();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("消息"))
            {
                ConfigFunctions.DrawPrintTypeSelector();
                ConfigFunctions.DrawErrorTypeSelector();
                ConfigFunctions.DrawMapMarkerPrintBox();
                ConfigFunctions.DrawPrintUptimesBox();
                ConfigFunctions.DrawPrintClipboardBox();
                ConfigFunctions.DrawAlarmFormatInput();
                ConfigFunctions.DrawIdentifiedGatherableFormatInput();
                ImGui.TreePop();
            }

            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader("界面设置"))
        {
            if (ImGui.TreeNodeEx("设置窗口"))
            {
                ConfigFunctions._base = this;
                ConfigFunctions.DrawOpenOnStartBox();
                ConfigFunctions.DrawRespectEscapeBox();
                ConfigFunctions.DrawLockPositionBox();
                ConfigFunctions.DrawLockResizeBox();
                ConfigFunctions.DrawWeatherTabNamesBox();
                ConfigFunctions.DrawShowStatusLineBox();
                ConfigFunctions.DrawHideClippyBox();
                ConfigFunctions.DrawMainInterfaceHotkeyInput();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("钓鱼窗口"))
            {
                ConfigFunctions.DrawKeepRecordsBox();
                ConfigFunctions.DrawFishTimerBox();
                ConfigFunctions.DrawFishTimerEditBox();
                ConfigFunctions.DrawFishTimerClickthroughBox();
                ConfigFunctions.DrawFishTimerHideBox();
                ConfigFunctions.DrawFishTimerHideBox2();
                ConfigFunctions.DrawFishTimerUptimesBox();
                ConfigFunctions.DrawFishTimerScale();
                ConfigFunctions.DrawFishTimerIntervals();
                ConfigFunctions.DrawHideFishPopupBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("采集窗口"))
            {
                ConfigFunctions.DrawShowGatherWindowBox();
                ConfigFunctions.DrawGatherWindowAnchorBox();
                ConfigFunctions.DrawGatherWindowTimersBox();
                ConfigFunctions.DrawGatherWindowAlarmsBox();
                ConfigFunctions.DrawSortGatherWindowBox();
                ConfigFunctions.DrawGatherWindowShowOnlyAvailableBox();
                ConfigFunctions.DrawHideGatherWindowInDutyBox();
                ConfigFunctions.DrawGatherWindowHoldKey();
                ConfigFunctions.DrawGatherWindowLockBox();
                ConfigFunctions.DrawGatherWindowHotkeyInput();
                ConfigFunctions.DrawGatherWindowDeleteModifierInput();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("刺鱼助手"))
            {
                ConfigFunctions.DrawSpearfishHelperBox();
                ConfigFunctions.DrawSpearfishNamesBox();
                ConfigFunctions.DrawSpearfishSpeedBox();
                ConfigFunctions.DrawAvailableSpearfishBox();
                ConfigFunctions.DrawSpearfishIconsAsTextBox();
                ConfigFunctions.DrawSpearfishCenterLineBox();
                ConfigFunctions.DrawSpearfishFishNameFixed();
                ConfigFunctions.DrawSpearfishFishNamePercentage();
                ImGui.TreePop();
            }

            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader("颜色"))
        {
            foreach (var color in Enum.GetValues<ColorId>())
            {
                var (defaultColor, name, description) = color.Data();
                var currentColor = GatherBuddy.Config.Colors.TryGetValue(color, out var current) ? current : defaultColor;
                if (Widget.ColorPicker(name, description, currentColor, c => GatherBuddy.Config.Colors[color] = c, defaultColor))
                    GatherBuddy.Config.Save();
            }

            ImGui.NewLine();

            if (Widget.PaletteColorPicker("聊天栏中的名字", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorNames,
                    Configuration.DefaultSeColorNames, Configuration.ForegroundColors, out var idx))
                GatherBuddy.Config.SeColorNames = idx;
            if (Widget.PaletteColorPicker("聊天栏中的命令", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorCommands,
                    Configuration.DefaultSeColorCommands, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorCommands = idx;
            if (Widget.PaletteColorPicker("聊天栏中的参数", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorArguments,
                    Configuration.DefaultSeColorArguments, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorArguments = idx;
            if (Widget.PaletteColorPicker("聊天栏中的闹钟消息", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorAlarm,
                    Configuration.DefaultSeColorAlarm, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorAlarm = idx;

            ImGui.NewLine();
        }
    }
}
