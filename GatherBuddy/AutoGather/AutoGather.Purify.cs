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
        private unsafe bool HasReducibleItems()
        {
            if (!QuestManager.IsQuestComplete(67633)) // 完成"不再是收藏品"任务
            {
                Communicator.PrintError("[GatherBuddy Reborn] 已启用自动精选但相关任务尚未完成。功能已禁用。");
                return false;
            }

            var agent = AgentPurify.Instance();
            return agent != null && agent->ReducibleItems.Count > 0;
        }


        public bool DoAetherialReduction()
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
                    GatherBuddy.Log.Warning("DR正在忙碌中。");
                    return true;
                }

                return DailyRoutines_IPCSubscriber.StartAutoReduction();
            }
            catch (Exception ex)
            {
                GatherBuddy.Log.Error($"调用DailyRoutines的StartAutoReduction方法时出错: {ex.Message}");
                Communicator.PrintError($"自动精选失败: {ex.Message}");
                return false;
            }
        }
    }
}
