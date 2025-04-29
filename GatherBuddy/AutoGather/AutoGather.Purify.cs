using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Plugin;
using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace GatherBuddy.AutoGather
{


    public partial class AutoGather
    {
        private bool HasReducibleItems()
        {
            if (!GatherBuddy.Config.AutoGatherConfig.DoReduce)
                return false;

            if (!QuestManager.IsQuestComplete(67633)) // 完成"生命、精选，另一个答案"任务
            {
                GatherBuddy.Config.AutoGatherConfig.DoReduce = false;
                Communicator.PrintError(
                    "[GatherBuddyReborn] 自动精选已启用，但角色未解锁精选技能。功能已禁用。");
                return false;
            }

            unsafe
            {
                var manager = InventoryManager.Instance();
                if (manager == null)
                    return false;

                foreach (var invType in InventoryTypes)
                {
                    var container = manager->GetInventoryContainer(invType);
                    if (container == null || !container->IsLoaded)
                        continue;

                    for (int i = 0; i < container->Size; i++)
                    {
                        var slot = container->GetInventorySlot(i);
                        if (slot != null
                         && slot->ItemId != 0
                         && GatherBuddy.GameData.Gatherables.TryGetValue(slot->ItemId, out var gatherable)
                         && gatherable.ItemData.AetherialReduce != 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }


        public unsafe bool DoAetherialReduction()
        {
            if (DailyRoutines_IPCSubscriber.IsEnabled && DailyRoutines_IPCSubscriber.IsAutoReductionBusy())
            {
                AutoStatus = "正在精选...";
                return true;
            }

            if (Svc.Condition[ConditionFlag.Mounted])
            {
                TaskManager.Enqueue(StopNavigation);
                EnqueueDismount();
                TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Mounted]);
                return false;
            }

            if (!DailyRoutines_IPCSubscriber.IsEnabled)
            {
                Communicator.PrintError("自动精选需要DailyRoutines插件，请确保其已安装并启用。");
                return false;
            }

            var delay = (int)GatherBuddy.Config.AutoGatherConfig.ExecutionDelay;

            try
            {
                TaskManager.Enqueue(StopNavigation);
                EnqueueActionWithDelay(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 21));
                TaskManager.DelayNext(500);
                TaskManager.Enqueue(() => !DailyRoutines_IPCSubscriber.StartAutoReduction(), 3000, true, "启动自动精选");
                TaskManager.Enqueue(() => !DailyRoutines_IPCSubscriber.IsAutoReductionBusy(), 180000, true, "等待精选完成");
                TaskManager.DelayNext(delay);
                TaskManager.DelayNext(1000);

                return true;
            }
            catch (Exception ex)
            {
                GatherBuddy.Log.Error($"调用DailyRoutines时出错: {ex.Message}");
                Communicator.PrintError($"自动精选失败: {ex.Message}");
                return false;
            }
        }
    }
}
