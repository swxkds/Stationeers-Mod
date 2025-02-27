using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using static meanran_xuexi_mods_xiaoyouhua.链接选择面板;

namespace meanran_xuexi_mods_xiaoyouhua
{
    public static partial class 扩展方法
    {
        public static void 设置螺丝链接(this LogicReader 逻辑读取器, Interactable 逻辑读取器控件, ILogicable 按钮点击返回的链接物, LogicType 按钮点击返回的逻辑类型)
        {
            switch (逻辑读取器控件.Action)
            {
                case InteractableType.Button1:
                    设置链接物体(逻辑读取器, 按钮点击返回的链接物); break;
                case InteractableType.Button2:
                    设置链接类型(逻辑读取器, 按钮点击返回的逻辑类型); break;
                default: break;
            }
        }
        private static void 设置链接物体(LogicReader 逻辑读取器, ILogicable 选择焦点)
        {
            // TODO:联机游戏请在此处发送数据包,目前不知道应该发送什么消息
            逻辑读取器.CurrentDevice = 选择焦点 as Device;
            逻辑读取器.LogicType = LogicType.None;
            逻辑读取器.Setting = 0;
        }
        private static void 设置链接类型(LogicReader 逻辑读取器, LogicType 参数类型)
        {
            // TODO:联机游戏请在此处发送数据包,目前不知道应该发送什么消息
            if (逻辑读取器.CurrentDevice != null)
            {
                逻辑读取器.LogicType = 参数类型;
                逻辑读取器.Setting = 0;
            }
        }
        public static 链接选择面板消息结构 获取链接选择面板所需的交互消息(this LogicReader 逻辑读取器, Interactable 逻辑读取器控件)
        {
            switch (逻辑读取器控件.Action)
            {
                case InteractableType.Button1:
                    return 链接选择面板消息结构.选择链接物;
                case InteractableType.Button2:
                    return new 链接选择面板消息结构 { 控件类型 = 面板类型.逻辑类型控件, 逻辑类型控件所需的已链接物体 = 逻辑读取器.CurrentDevice, 此物体是读取器还是写入器 = IOCheck.Readable };
                default: return 链接选择面板消息结构.选择链接物;
            }
        }
    }

    [HarmonyPatch(typeof(LogicReader), nameof(LogicReader.InteractWith))]
    public class LogicReader_InteractWith_PrefixPatch
    {
        // 添加了光线命中的事件处理逻辑
        [HarmonyPrefix]
        public static bool 交互事件处理(LogicReader __instance, ref Thing.DelayedActionInstance __result, Interactable interactable, Interaction interaction, bool doAction = true)
        {
            // 游戏交互的设计是光线命中时,生成消息,此消息中 保存着玩家,玩家活动手插槽,目标被命中控件,目标物体首地址的引用
            if (interaction.SourceSlot.Get() is Labeller 贴标机)
            {
                // interactable.ContextualName: 若是控件,显示"开"关";若是物体,显示名称
                // 仅用于协程函数的时长定义,正常不使用Duration
                var 动作状态消息 = new Thing.DelayedActionInstance
                { Duration = 0, ActionMessage = interactable.ContextualName };
                __result = 链接选择面板.唤醒选择面板(__instance, interactable, interaction, 动作状态消息, 贴标机, doAction);
                return false;
            }

            // 其他情况执行游戏自带的交互逻辑
            return true;
        }
    }

}


