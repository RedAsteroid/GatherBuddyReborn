using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Classes;
using GatherBuddy.CustomInfo;
using GatherBuddy.Interfaces;
using GatherBuddy.Plugin;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace GatherBuddy.AutoGather
{
    public partial class AutoGather
    {
        private unsafe void EnqueueDismount()
        {
            TaskManager.Enqueue(StopNavigation);

            var am = ActionManager.Instance();
            TaskManager.Enqueue(() => { if (Dalamud.Conditions[ConditionFlag.Mounted]) am->UseAction(ActionType.Mount, 0); }, "下坐骑");

            TaskManager.Enqueue(() => !Dalamud.Conditions[ConditionFlag.InFlight] && CanAct, 1000, "等待飞行状态取消");
            TaskManager.Enqueue(() => { if (Dalamud.Conditions[ConditionFlag.Mounted]) am->UseAction(ActionType.Mount, 0); }, "下坐骑 2");
            TaskManager.Enqueue(() => !Dalamud.Conditions[ConditionFlag.Mounted] && CanAct, 1000, "等待坐骑状态取消");
            TaskManager.Enqueue(() => { if (!Dalamud.Conditions[ConditionFlag.Mounted]) TaskManager.DelayNextImmediate(500); } );//Prevent "Unable to execute command while jumping."
        }

        private unsafe void EnqueueMountUp()
        {
            var am = ActionManager.Instance();
            var mount = GatherBuddy.Config.AutoGatherConfig.AutoGatherMountId;
            Action doMount;

            if (IsMountUnlocked(mount) && am->GetActionStatus(ActionType.Mount, mount) == 0)
            {
                doMount = () => am->UseAction(ActionType.Mount, mount);
            }
            else
            {
                if (am->GetActionStatus(ActionType.GeneralAction, 24) != 0)
                {
                    return;
                }

                doMount = () => am->UseAction(ActionType.GeneralAction, 24);
            }

            TaskManager.Enqueue(StopNavigation);
            TaskManager.Enqueue(doMount);
            TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Mounted], 2000);
        }

        private unsafe bool IsMountUnlocked(uint mount)
        {
            var instance = PlayerState.Instance();
            if (instance == null)
                return false;

            return instance->IsMountUnlocked(mount);
        }

        private void MoveToCloseNode(IGameObject gameObject, Gatherable targetItem)
        {
            var distance = gameObject.Position.DistanceToPlayer();
            if (distance < 3)
            {
                if (Dalamud.Conditions[ConditionFlag.Mounted])
                {
                    //Try to dismount early. It would help with nodes where it is not possible to dismount at vnavmesh's provided floor point
                    EnqueueDismount();
                    TaskManager.Enqueue(() => {
                        //If early dismount failed, navigate to the node
                        if (Dalamud.Conditions[ConditionFlag.Mounted])
                        {
                            Navigate(gameObject.Position, Dalamud.Conditions[ConditionFlag.InFlight]);
                            TaskManager.Enqueue(() => !IsPathGenerating);
                            TaskManager.Enqueue(() => !IsPathing, 1000);
                            EnqueueDismount();
                            //If even that fails, do advanced unstuck
                            TaskManager.Enqueue(() => { if (Dalamud.Conditions[ConditionFlag.Mounted]) AdvancedUnstuckCheck(false, false, true); });
                        }
                    });
                }
                else if (targetItem.ItemData.IsCollectable && Player.Object.CurrentGp < GatherBuddy.Config.AutoGatherConfig.MinimumGPForCollectable
                     || !targetItem.ItemData.IsCollectable && Player.Object.CurrentGp < GatherBuddy.Config.AutoGatherConfig.MinimumGPForGathering)
                {
                    StopNavigation();
                    AutoStatus = "等待采集力恢复中...";
                } 
                else
                {
                    // Use consumables with cast time just before gathering a node when player is surely not mounted
                    if (DoUseConsumablesWithCastTime())
                    {
                        StopNavigation();
                    }
                    else
                    {
                        //Enqueue navigation anyway, since the node may be behind a rock or a tree
                        Navigate(gameObject.Position, false);
                        EnqueueNodeInteraction(gameObject, targetItem);
                    }
                }
            }
            else if (distance < Math.Max(GatherBuddy.Config.AutoGatherConfig.MountUpDistance, 5))
            {
                Navigate(gameObject.Position, false);
            }
            else
            {
                if (!Dalamud.Conditions[ConditionFlag.Mounted])
                {
                    EnqueueMountUp();
                }
                else
                {
                    Navigate(gameObject.Position, ShouldFly(gameObject.Position));
                }
            }
        }

        private Vector3? lastPosition = null;
        private DateTime lastMovementTime;
        private DateTime lastResetTime;


        private void StuckCheck()
        {
            if (GatherBuddy.Config.AutoGatherConfig.UseExperimentalUnstuck)
                return;
            
            if (EzThrottler.Throttle("StuckCheck", 100))
            {
                // Check if character is stuck
                if (lastPosition.HasValue && Vector3.Distance(Player.Object.Position, lastPosition.Value) < 2.0f)
                {
                    // If the character hasn't moved much
                    if ((DateTime.Now - lastMovementTime).TotalSeconds > GatherBuddy.Config.AutoGatherConfig.NavResetThreshold)
                    {
                        // Check if enough time has passed since the last reset
                        if ((DateTime.Now - lastResetTime).TotalSeconds > GatherBuddy.Config.AutoGatherConfig.NavResetCooldown)
                        {
                            GatherBuddy.Log.Warning("角色被卡住, 正在重置导航...");
                            StopNavigation();
                            return;
                        }
                    }
                }
                else
                {
                    // Character has moved, update last known position and time
                    lastPosition     = Player.Object.Position;
                    lastMovementTime = DateTime.Now;
                }
            }
        }

        private void StopNavigation()
        {
            // Reset navigation logic here
            // For example, reinitiate navigation to the destination
            CurrentDestination = default;
            VNavmesh_IPCSubscriber.Nav_PathfindCancelAll();
            VNavmesh_IPCSubscriber.Path_Stop();
            lastResetTime = DateTime.Now;
            advandedLastPosition = null;
        }

        private void Navigate(Vector3 destination, bool shouldFly)
        {
            if (CurrentDestination == destination && (IsPathing || IsPathGenerating))
                return;
            
            StopNavigation();
            CurrentDestination = destination;
            GatherBuddy.Log.Debug($"正在导航至 {CurrentDestination}");
            var loop = 1;
            Vector3 correctedDestination = GetCorrectedDestination(shouldFly);
            while (Vector3.Distance(correctedDestination, CurrentDestination) > 15 && loop < 8)
            {
                GatherBuddy.Log.Information("上一节点与下一采集点间距离过远 : "
                    + Vector3.Distance(correctedDestination, CurrentDestination));
                correctedDestination = shouldFly ? CurrentDestination.CorrectForMesh(loop * 0.5f) : CurrentDestination;
                loop++;
            }

            if (Vector3.Distance(correctedDestination, CurrentDestination) > 10)
            {
                GatherBuddy.Log.Warning($"无效的目的地: {correctedDestination}");
                StopNavigation();
                return;
            }

            if (!correctedDestination.SanityCheck())
            {
                GatherBuddy.Log.Warning($"无效的目的地: {correctedDestination}");
                StopNavigation();
                return;
            }

            LastNavigationResult = VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(correctedDestination, shouldFly);
        }

        private Vector3 GetCorrectedDestination(bool shouldFly)
        {
            var selectedOffset = WorldData.NodeOffsets.FirstOrDefault(o => o.Original == CurrentDestination);
            if (selectedOffset != null)
            {
                return selectedOffset.Offset;
            }
            else
            {
                return shouldFly ? CurrentDestination.CorrectForMesh(0.5f) : CurrentDestination;
            }
        }

        private void MoveToFarNode(Vector3 position)
        {
            var farNode = position;

            if (!Dalamud.Conditions[ConditionFlag.Mounted])
            {
                EnqueueMountUp();
            }
            else
            {
                Navigate(farNode, ShouldFly(farNode));
            }
        }

        private void MoveToTerritory(ILocation location)
        {
            TaskManager.EnqueueImmediate(() => _plugin.Executor.GatherLocation(location));
            if (location.Territory.Id != Svc.ClientState.TerritoryType)
            {
                TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.BetweenAreas]);
                TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.BetweenAreas]);
            }
            TaskManager.DelayNext(1500);
        }

        private Vector3? advandedLastPosition = null;
        private DateTime advancedLastMovementTime;
        private DateTime advancedMovementStart = DateTime.MinValue;

        private bool AdvancedUnstuckCheck(bool isPathGenerating, bool isPathing, bool force = false)
        {
            if (!GatherBuddy.Config.AutoGatherConfig.UseExperimentalUnstuck)
                return false;

            if (!_movementController.Enabled &&
                (  force
                || advandedLastPosition.HasValue && advandedLastPosition.Value.DistanceToPlayer() < 2.0f && isPathing
                || !isPathGenerating && !isPathing && CurrentDestination != default && CurrentDestination.DistanceToPlayer() > 3))
            {
                // If the character hasn't moved much
                if ((DateTime.Now - advancedLastMovementTime).TotalSeconds > GatherBuddy.Config.AutoGatherConfig.NavResetThreshold)
                {
                    GatherBuddy.Log.Warning($"角色被卡住, 尝试使用高级脱离卡死方法");
                    StopNavigation();
                    var rng = new Random();
                    var rnd = () => (rng.Next(2) == 0 ? -1 : 1) * rng.NextSingle();
                    Vector3 newPosition = Player.Position + Vector3.Normalize(new Vector3(rnd(), rnd(), rnd())) * 10f;
                    _movementController.DesiredPosition = newPosition;
                    _movementController.Enabled         = true;
                    advancedMovementStart               = DateTime.Now;
                }
            }
            else if (_movementController.Enabled && (DateTime.Now - advancedMovementStart).TotalSeconds > 1.5)
            {
                _movementController.Enabled         = false;
                _movementController.DesiredPosition = Vector3.Zero;
            }
            else
            {
                // Character has moved, update last known position and time
                advandedLastPosition     = Player.Object.Position;
                advancedLastMovementTime = DateTime.Now;
            }
            return _movementController.Enabled;
        }
    }
}
