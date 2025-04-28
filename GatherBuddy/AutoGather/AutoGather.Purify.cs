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
            if (!GatherBuddy.Config.AutoGatherConfig.DoReduce || Svc.Condition[ConditionFlag.Mounted])
                return false;

            if (!QuestManager.IsQuestComplete(67633)) // 完成“生命、精选，另一个答案”任务
            {
                GatherBuddy.Config.AutoGatherConfig.DoReduce = false;
                Communicator.PrintError(
                    "[GatherBuddyReborn] Aetherial reduction is enabled, but the relevant quest has not been completed yet. The feature has been disabled.");
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

            AutoStatus = "正在精选...";

            try
            {
                if (DailyRoutines_IPCSubscriber.IsAutoReductionBusy())
                {
                    return true;
                }
                EnqueueActionWithDelay(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 21));
                TaskManager.Enqueue(() => !DailyRoutines_IPCSubscriber.StartAutoReduction());

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
