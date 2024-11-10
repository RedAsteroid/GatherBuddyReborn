using ECommons.Automation.LegacyTaskManager;
using GatherBuddy.Plugin;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using GatherBuddy.AutoGather.Movement;
using GatherBuddy.Classes;
using GatherBuddy.CustomInfo;
using GatherBuddy.Enums;
using ECommons.Throttlers;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text;
using Dalamud.Utility;
using ECommons.ExcelServices;
using ECommons.Automation;
using GatherBuddy.Data;
using NodeType = GatherBuddy.Enums.NodeType;
using ECommons.UIHelpers.AddonMasterImplementations;

namespace GatherBuddy.AutoGather
{
    public partial class AutoGather : IDisposable
    {
        public AutoGather(GatherBuddy plugin)
        {
            // Initialize the task manager
            TaskManager                            =  new();
            TaskManager.ShowDebug                  =  false;
            _plugin                                =  plugin;
            _soundHelper                           =  new SoundHelper();
            _advancedUnstuck                       =  new();
        }

        private readonly GatherBuddy _plugin;
        private readonly SoundHelper _soundHelper;
        private readonly AdvancedUnstuck _advancedUnstuck;
        
        public           TaskManager TaskManager { get; }

        private bool _enabled { get; set; } = false;
        internal readonly GatheringTracker NodeTarcker = new();

        public unsafe bool Enabled
        {
            get => _enabled;
            set
            {
                if (!value)
                {
                    //Do Reset Tasks
                    var gatheringMasterpiece = (AddonGatheringMasterpiece*)Dalamud.GameGui.GetAddonByName("GatheringMasterpiece", 1);
                    if (gatheringMasterpiece != null && !gatheringMasterpiece->AtkUnitBase.IsVisible)
                    {
                        gatheringMasterpiece->AtkUnitBase.IsVisible = true;
                    }

                    TaskManager.Abort();
                    targetInfo                          = null;
                    StopNavigation();
                    AutoStatus = "空闲中...";
                }
                else
                {
                    RefreshNextTresureMapAllowance();                    
                    WentHome = true; //Prevents going home right after enabling auto-gather
                }

                _enabled = value;
            }
        }

        public void GoHome()
        {
            StopNavigation();

            if (WentHome) return;
            WentHome = true;

            if (Dalamud.Conditions[ConditionFlag.BoundByDuty])
                return;

            if (Lifestream_IPCSubscriber.IsEnabled && !Lifestream_IPCSubscriber.IsBusy())
                Lifestream_IPCSubscriber.ExecuteCommand("auto");
            else 
                GatherBuddy.Log.Warning("未安装或启用 Lifestream");
        }

        private class NoGatherableItemsInNodeExceptions : Exception { }
        private class NoColectableActionsExceptions : Exception { }
        public void DoAutoGather()
        {
            if (!IsGathering)
                LuckUsed = new(0); //Reset the flag even if auto-gather was disabled mid-gathering

            if (!Enabled)
            {
                return;
            }

            try
            {
                if (!NavReady)
                {
                    AutoStatus = "等待导航中...";
                    return;
                }
            }
            catch (Exception e)
            {
                //GatherBuddy.Log.Error(e.Message);
                AutoStatus = "未安装或启用 vnavmesh";
                return;
            }

            if (TaskManager.IsBusy)
            {
                //GatherBuddy.Log.Verbose("TaskManager has tasks, skipping DoAutoGather");
                return;
            }

            if (!CanAct)
            {
                AutoStatus = Dalamud.Conditions[ConditionFlag.Gathering] ? AutoStatus = "采集中..." : "当前无法行动...";
                return;
            }

            if (FreeInventorySlots == 0)
            {
                AbortAutoGather("背包已满");
                return;
            }

            if (IsGathering)
            {
                if (targetInfo != null)
                {
                    if (targetInfo.Location != null && targetInfo.Item.NodeType is NodeType.未知 or NodeType.传说)
                        VisitedTimedLocations[targetInfo.Location] = targetInfo.Time;

                    var target = Svc.Targets.Target;
                    if (target != null
                        && target.ObjectKind is ObjectKind.GatheringPoint
                        && targetInfo.Item.NodeType is NodeType.Regular or NodeType.Ephemeral
                        && VisitedNodes.Last?.Value != target.DataId
                        && targetInfo.Location?.WorldPositions.ContainsKey(target.DataId) == true)
                    {
                        FarNodesSeenSoFar.Clear();
                        VisitedNodes.AddLast(target.DataId);
                        while (VisitedNodes.Count > (targetInfo.Location.WorldPositions.Count <= 4 ? 2 : 4))
                            VisitedNodes.RemoveFirst();
                    }
                }

                if (GatherBuddy.Config.AutoGatherConfig.DoGathering)
                {
                    AutoStatus = "采集中...";
                    StopNavigation();
                    try
                    {
                        DoActionTasks(targetInfo?.Item);
                    }
                    catch (NoGatherableItemsInNodeExceptions)
                    {
                        CloseGatheringAddons();
                    }
                    catch (NoColectableActionsExceptions)
                    {
                        Communicator.PrintError("Unable to pick a collectability increasing action to use. Make sure that at least one of the collectable actions is enabled.");
                        AbortAutoGather();
                    }
                    return;
                }

                return;
            }

            //Cache IPC call results
            var isPathGenerating = IsPathGenerating;
            var isPathing = IsPathing;

            if (_advancedUnstuck.IsRunning || CurrentDestination != default && CurrentDestination.DistanceToPlayer() > 3 && _advancedUnstuck.Check(isPathGenerating, isPathing))
            {
                StopNavigation();
                AutoStatus = $"Advanced unstuck in progress!";
                return;
            }

            if (isPathGenerating)
            {
                AutoStatus = "正在生成路径...";
                lastMovementTime = DateTime.Now;
                return;
            }

            if (isPathing)
            {
                StuckCheck();
            }

            if (GatherBuddy.Config.AutoGatherConfig.DoMaterialize 
                && Player.Job is Job.BTN or Job.MIN
                && !isPathing 
                && !Svc.Condition[ConditionFlag.Mounted] 
                && SpiritBondMax > 0)
            {
                DoMateriaExtraction();
                return;
            }

            {//Block to limit the scope of the variable "next"
                UpdateItemsToGather();
                var next = ItemsToGather.FirstOrDefault();

                if (next == null)
                {
                    if (!_plugin.GatherWindowManager.ActiveItems.OfType<Gatherable>().Any(i => InventoryCount(i) < QuantityTotal(i) && !(i.IsTreasureMap && InventoryCount(i) != 0)))
                    {
                        AbortAutoGather();
                        return;
                    }

                    if (GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle)
                        GoHome();

                    AutoStatus = "无待采集物品";
                    return;
                }

                foreach (var (loc, time) in VisitedTimedLocations)
                    if (time.End < AdjuctedServerTime)
                        VisitedTimedLocations.Remove(loc);

                if (targetInfo == null
                    || targetInfo.Location == null
                    || targetInfo.Time.End < AdjuctedServerTime
                    || targetInfo.Item != next.Item
                    || VisitedTimedLocations.ContainsKey(targetInfo.Location))
                {
                    //Find a new location only if the target item changes or the node expires to prevent switching to another node when a new one goes up
                    targetInfo = next;
                    FarNodesSeenSoFar.Clear();
                    VisitedNodes.Clear();
                }
            }

            if (targetInfo.Location == null)
            {
                //Should not happen because UpdateItemsToGather filters out all unaviable items
                GatherBuddy.Log.Debug("未能找到任一导向该物品的目标地点");
                return;
            }

            if (targetInfo.Item.IsTreasureMap && NextTresureMapAllowance == DateTime.MinValue)
            {
                //Wait for timer refresh
                RefreshNextTresureMapAllowance();
                return;
            }

            if (!LocationMatchesJob(targetInfo.Location))
            {
                if (!ChangeGearSet(targetInfo.Location.GatheringType.ToGroup()))
                    AbortAutoGather();
                return;
            }

            //This check must be done after changing jobs.
            if (targetInfo.Item.ItemData.IsCollectable && !CheckCollectablesUnlocked())
            {
                AbortAutoGather();
                return;
            }

            var territoryId = Svc.ClientState.TerritoryType;
            //Idyllshire to The Dravanian Hinterlands
            if (territoryId == 478 && targetInfo.Location.Territory.Id == 399 && Lifestream_IPCSubscriber.IsEnabled)
            {
                var aetheryte = Svc.Objects.Where(x => x.ObjectKind == ObjectKind.Aetheryte && x.IsTargetable).OrderBy(x => x.Position.DistanceToPlayer()).FirstOrDefault();
                if (aetheryte != null)
                {
                    if (aetheryte.Position.DistanceToPlayer() > 10)
                    {
                        AutoStatus = "向以太之光移动中...";
                        if (!isPathing && !isPathGenerating) Navigate(aetheryte.Position, false);
                    }
                    else if (!Lifestream_IPCSubscriber.IsBusy())
                    {
                        AutoStatus = "传送中...";
                        StopNavigation();
                        var exit = targetInfo.Location.DefaultXCoord < 2000 ? 91u : 92u;
                        var name = Dalamud.GameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Aetheryte>()!.GetRow(exit)!.AethernetName.Value!.Name;
                        Lifestream_IPCSubscriber.AethernetTeleport(name);
                    }
                    return;
                }
            }

            var forcedAetheryte = ForcedAetherytes.ZonesWithoutAetherytes.Where(z => z.ZoneId == targetInfo.Location.Territory.Id).FirstOrDefault();
            if (forcedAetheryte.ZoneId != 0 
                && (GatherBuddy.GameData.Aetherytes[forcedAetheryte.AetheryteId].Territory.Id == territoryId
                    || forcedAetheryte.AetheryteId == 70 && territoryId == 886)) //The Firmament
                {
                if (territoryId == 478 && !Lifestream_IPCSubscriber.IsEnabled)
                    AutoStatus = $"请安装 Lifestraem 或手动传送至 {targetInfo.Location.Territory.Name}";
                else
                    AutoStatus = "需要手动传送";
                return;
            }

            //At this point, we are definitely going to gather something, so we may go home after that.
            if (Lifestream_IPCSubscriber.IsEnabled) Lifestream_IPCSubscriber.Abort();
            WentHome = false;

            if (targetInfo.Location.Territory.Id != territoryId)
            {
                if (Dalamud.Conditions[ConditionFlag.BoundByDuty])
                {
                    AutoStatus = "无法在副本任务中传送";
                    return;
                }
                AutoStatus = "传送中...";
                StopNavigation();

                if (!MoveToTerritory(targetInfo.Location))
                    AbortAutoGather();

                return;
            }

            if (ActivateGatheringBuffs(targetInfo.Item.NodeType is NodeType.Unspoiled or NodeType.Legendary))
                return;

            if (DoUseConsumablesWithoutCastTime())
                return;

            var allPositions = targetInfo.Location.WorldPositions
                .ExceptBy(VisitedNodes, n => n.Key)
                .SelectMany(w => w.Value)
                .Where(v => !IsBlacklisted(v))
                .ToHashSet();

            var visibleNodes = Svc.Objects
                .Where(o => allPositions.Contains(o.Position))
                .ToList();

            var closestTargetableNode = visibleNodes
                .Where(o => o.IsTargetable)
                .MinBy(o => Vector3.Distance(Player.Position, o.Position));

            if (closestTargetableNode != null)
            {
                AutoStatus = "正在移动至较近节点...";
                MoveToCloseNode(closestTargetableNode, targetInfo.Item);
                return;
            }

            AutoStatus = "正在移动至较远节点...";

            if (CurrentDestination != default && CurrentFarNodeLocation != targetInfo.Location)
            {
                GatherBuddy.Log.Debug("当前目的地与待采集物品地点不符, 已重置导航");
                StopNavigation();
                FarNodesSeenSoFar.Clear();
                VisitedNodes.Clear();
            }

            CurrentFarNodeLocation = targetInfo.Location;

            if (CurrentDestination != default)
            {
                var currentNode = visibleNodes.FirstOrDefault(o => o.Position == CurrentDestination);
                if (currentNode != null && !currentNode.IsTargetable)
                    GatherBuddy.Log.Verbose($"下一节点距离较远, 当前尚不可选中, 距离: {currentNode.Position.DistanceToPlayer()}.");

                //It takes some time (roundtrip to the server) before a node becomes targetable after it becomes visible,
                //so we need to delay excluding it. But instead of measuring time, we use distance, since character is traveling at a constant speed.
                //Value 80 was determined empirically.
                foreach (var node in visibleNodes.Where(o => o.Position.DistanceToPlayer() < 80))
                    FarNodesSeenSoFar.Add(node.Position);

                if (FarNodesSeenSoFar.Contains(CurrentDestination))
                {
                    GatherBuddy.Log.Verbose("下一节点距离较远, 当前尚不可选中, 已切换至另一节点");
                }
                else
                {
                    return;
                }
            }

            Vector3 selectedFarNode;
            
            // only Legendary and Unspoiled show marker
            if (ShouldUseFlag && targetInfo.Item.NodeType is NodeType.传说 or NodeType.未知)
            {
                var pos = TimedNodePosition;
                // marker not yet loaded on game
                if (pos == null)
                {
                    AutoStatus = "等待标点出现中";
                    return;
                }

                selectedFarNode = allPositions
                    .Where(o => Vector2.Distance(pos.Value, new Vector2(o.X, o.Z)) < 10)
                    .OrderBy(o => Vector2.Distance(pos.Value, new Vector2(o.X, o.Z)))
                    .FirstOrDefault();
                if (selectedFarNode == default)
                    selectedFarNode = VNavmesh_IPCSubscriber.Query_Mesh_NearestPoint(new Vector3(pos.Value.X, 0, pos.Value.Y), 10, 10000);
            }
            else
            {
                //Select the closest node
                selectedFarNode = allPositions
                    .OrderBy(v => Vector3.Distance(Player.Position, v))
                    .FirstOrDefault(n => !FarNodesSeenSoFar.Contains(n));

                if (selectedFarNode == default)
                {
                    FarNodesSeenSoFar.Clear();
                    GatherBuddy.Log.Verbose($"当前选择节点为空, 较远节点筛选器已被清空");
                    return;
                }

            }
             
            MoveToFarNode(selectedFarNode);
        }

        private void AbortAutoGather(string? status = null)
        {
            Enabled = false;
            if (!string.IsNullOrEmpty(status))
                AutoStatus = status;
            if (GatherBuddy.Config.AutoGatherConfig.HonkMode)
                _soundHelper.PlayHonkSound(3);
            CloseGatheringAddons();
            if (GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone)
                EnqueueActionWithDelay(GoHome);
        }

        private unsafe void CloseGatheringAddons()
        {
            var masterpieceOpen = MasterpieceAddon != null;
            var gatheringOpen = GatheringAddon != null;
            if (masterpieceOpen)
            {
                EnqueueActionWithDelay(() => { if (MasterpieceAddon is var addon and not null) { Callback.Fire(&addon->AtkUnitBase, true, -1); } });
                TaskManager.Enqueue(() => MasterpieceAddon == null);
                TaskManager.Enqueue(() => GatheringAddon != null);
            }
            if (gatheringOpen || masterpieceOpen)
            {
                EnqueueActionWithDelay(() => { if (GatheringAddon is var addon and not null) { Callback.Fire(&addon->AtkUnitBase, true, -1); } });
                TaskManager.Enqueue(() => {
                    var addon = (AddonSelectYesno*)Dalamud.GameGui.GetAddonByName("SelectYesno");
                    if (addon != null)
                    {
                        EnqueueActionWithDelay(() =>
                        {
                            if ((AddonSelectYesno*)Dalamud.GameGui.GetAddonByName("SelectYesno") is var addon and not null)
                            {
                                var master = new AddonMaster.SelectYesno(addon);
                                master.Yes();
                            }
                        });
                        TaskManager.Enqueue(() => !IsGathering);
                        return true;
                    }
                    else
                    {
                        return IsGathering;
                    }    
                });
            }
        }

        private static unsafe void RefreshNextTresureMapAllowance()
        {
            if (EzThrottler.Throttle("RequestResetTimestamps", 1000))
            {
                FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance()->RequestResetTimestamps();
            }
        }

        private bool CheckCollectablesUnlocked()
        {
            if (Player.Level < Actions.Collect.MinLevel)
            {
                Communicator.PrintError("列表内存在当前无法采集的收藏品, 原因: 等级不足");
                return false;
            }
            if (Actions.Collect.QuestID != 0 && !QuestManager.IsQuestComplete(Actions.Collect.QuestID))
            {
                Communicator.PrintError("列表内存在当前无法采集的收藏品, 原因: 尚未解锁");
                var sheet = Dalamud.GameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Quest>()!;
                var row = sheet.GetRow(Actions.Collect.QuestID)!;
                var loc = row.IssuerLocation.Value!;
                var map = loc.Map.Value!;
                var pos = MapUtil.WorldToMap(new Vector2(loc.X, loc.Z), map);
                var mapPayload = new MapLinkPayload(loc.Territory.Row, loc.Map.Row, pos.X, pos.Y);
                var text = new SeStringBuilder();
                text.AddText("收藏品可以由 ")
                    .AddUiForeground(0x0225)
                    .AddUiGlow(0x0226)
                    .AddQuestLink(Actions.Collect.QuestID)
                    .AddUiForeground(500)
                    .AddUiGlow(501)
                    .AddText($"{(char)SeIconChar.LinkMarker}")
                    .AddUiGlowOff()
                    .AddUiForegroundOff()
                    .AddText(row.Name)
                    .Add(RawPayload.LinkTerminator)
                    .AddUiGlowOff()
                    .AddUiForegroundOff()
                    .AddText(" 任务解锁, 开始地点: ")
                    .AddUiForeground(0x0225)
                    .AddUiGlow(0x0226)
                    .Add(mapPayload)
                    .AddUiForeground(500)
                    .AddUiGlow(501)
                    .AddText($"{(char)SeIconChar.LinkMarker}")
                    .AddUiGlowOff()
                    .AddUiForegroundOff()
                    .AddText($"{mapPayload.PlaceName} {mapPayload.CoordinateString}")
                    .Add(RawPayload.LinkTerminator)
                    .AddUiGlowOff()
                    .AddUiForegroundOff()
                    .AddText(".");
                Communicator.Print(text.BuiltString);
                return false;
            }
            return true;
        }

        private bool ChangeGearSet(GatheringType job)
        {
            var set = job switch
            {
                GatheringType.Miner => GatherBuddy.Config.MinerSetName,
                GatheringType.Botanist => GatherBuddy.Config.BotanistSetName,
                _ => null,
            };
            if (set == null || set.Length == 0)
            {
                Communicator.PrintError($"未对 {job} 配置任何职业套装");
                return false;
            }

            Chat.Instance.ExecuteCommand($"/gearset change \"{set}\"");
            TaskManager.DelayNext(Random.Shared.Next(500, 1500));  //Add a random delay to be less suspicious 
            return true;
        }

        public void Dispose()
        {
            _advancedUnstuck.Dispose();
            NodeTarcker.Dispose();
        }
    }
}
