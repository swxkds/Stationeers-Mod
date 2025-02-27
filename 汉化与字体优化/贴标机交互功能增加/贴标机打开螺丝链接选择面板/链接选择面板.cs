using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts;
using Assets.Scripts.Inventory;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using HarmonyLib;
using Reagents;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace meanran_xuexi_mods_xiaoyouhua
{
    public class ILogicableReference : UserInterfaceAnimated, IScreenSpaceTooltip
    {
        public ILogicable 若是链接控件返回的链接物;
        public Image 物体缩略图;
        public TextMeshProUGUI 物体描述;
        public LogicType 若是逻辑类型控件返回的逻辑类型;
        public string 显示名称 => 若是链接控件返回的链接物.DisplayName;
        public bool TooltipIsVisible => IsVisible;
        public void 回调_按钮点击()
        {
            链接选择面板.当前焦点 = this;
            链接选择面板.Submit();
        }
        public void 设置(ILogicable 链接物, LogicType 参数类型, bool 链接物螺丝么)
        {
            if (链接物 != null)
            {
                若是链接控件返回的链接物 = 链接物;
                物体缩略图.sprite = ((Thing)链接物).Thumbnail;
                this.若是逻辑类型控件返回的逻辑类型 = 参数类型;
                if (链接物螺丝么)
                { 回调_RefreshString(); }
                else { 物体描述.text = 参数类型.GetName() + "\n" + Localization.GetSlotTooltip(Slot.Class.None); ; }
            }
        }

        public void 回调_RefreshString()
        {
            if (若是链接控件返回的链接物 != null)
            {
                物体描述.text = 若是链接控件返回的链接物.ToTooltip() + "\n" + Localization.GetSlotTooltip(Slot.Class.None);
            }
        }

        private string 工具提示详情()
        {
            return Localization.GetSlotTooltip(Slot.Class.None) + "\n模组制作真好玩";
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            // 此函数由Unity引擎自动调用,当光标进入感应区域时调用
            base.OnPointerEnter(eventData);
            PanelToolTip.Instance.SetUpToolTip(若是链接控件返回的链接物.ToTooltip(), 工具提示详情(), this);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            // 此函数由Unity引擎自动调用,当光标离开感应区域时调用
            base.OnPointerExit(eventData);
            PanelToolTip.Instance.ClearToolTip();
        }

        public void DoUpdate()
        {
            // 此函数由PanelToolTip.Instance在内部调用
            PanelToolTip.Instance.SetInfoText(工具提示详情());
        }
    }

    public struct 链接选择面板消息结构
    {
        public static 链接选择面板消息结构 选择链接物 =
        new 链接选择面板消息结构
        { 控件类型 = 链接选择面板.面板类型.链接物控件, 逻辑类型控件所需的已链接物体 = null, 此物体是读取器还是写入器 = IOCheck.ReadOrWritable };
        public 链接选择面板.面板类型 控件类型;
        public ILogicable 逻辑类型控件所需的已链接物体;
        public IOCheck 此物体是读取器还是写入器;
    }

    public class 链接选择面板 : InputWindowBase
    {
        // Interactable: 控件,注意是控件,而不是物体
        // Interaction: 交互双方. 玩家/玩家活动手插槽/视线交互焦点所在的控件或物体或另一个玩家
        // doAction: 玩家当前是在执行动作还是单纯的就看看(拿着工具不操作也算无动作)

        // AttackWith是操作事件,是对整个物体的相关状态变量进行修改
        // InteractWith是交互事件,是对物体内部的某个控件相关状态变量进行修改
        // 管道没有控件(例开关/按钮/刻度盘),因此是AttackWith,管道也没有
        // AttackWith和InteractWith这两个函数由游戏的事件总线每帧调用,会传递交互双方信息,我们只需要根据这些信息的组合,返回对应消息即可
        public static Thing.DelayedActionInstance 唤醒选择面板(LogicUnitBase 光线命中的设备, Interactable 光线命中的控件, Interaction 交互双方, Thing.DelayedActionInstance 默认动作状态消息, Labeller 贴标机, bool 玩家有动作么 = true)
        {
            // 重要!重要!玩家有动作么只有在协程内部才会写入true,在正常时永远是false
            // 当调用默认动作状态消息.Succeed()给IsDisabled写入false时,表示允许开启协程,当单击左键时,创建动作协程
            // 当调用默认动作状态消息.Fail()给IsDisabled写入true,表示禁止开启协程,当单击左键时,直接忽略
            // 在协程函数内部,判断若鼠标左键弹起或者视线乱唤导致光线命中的焦点物体改变了,直接结束协程,不产生任何AttackWith消息
            // 在协程函数内部,若长按时长>=默认动作状态消息.Duration,产生一个玩家有动作么=true的AttackWith消息

            Thing.DelayedActionInstance 动作状态消息 = null;
            // 仅用于正常函数的视线焦点物体工具提示,左上角
            默认动作状态消息.ActionMessage = ActionStrings.Set;

            // 贴标机电源不是开启时,仅显示工具提示面板:电源未打开,无法交互
            if (!贴标机.OnOff)
            {
                动作状态消息 = 默认动作状态消息.Fail(GameStrings.DeviceNotOn);
            }
            // 贴标机电池未安装或者没有电量,仅显示工具提示面板:未通电,无法交互
            else if (!贴标机.IsOperable)
            {
                动作状态消息 = 默认动作状态消息.Fail(GameStrings.DeviceNoPower);
            }

            // 贴标机电源打开且有电量
            if (动作状态消息 == null)
            {
                if (光线命中的设备.InputNetwork1DevicesSorted != null && 光线命中的设备.InputNetwork1DevicesSorted.Count > 1)
                {
                    默认动作状态消息.AppendStateMessage("单击打开链接节点选择面板");
                    // ActionMessage:启用时设置是绿色字,允许创建动作协程
                    动作状态消息 = 默认动作状态消息.Succeed();
                }
                else
                {
                    默认动作状态消息.AppendStateMessage("线路不通,未找到链接节点");
                    // ActionMessage:禁用时设置是红色字,不允许创建动作协程
                    动作状态消息 = 默认动作状态消息.Fail();
                    return 动作状态消息;
                }

                // 光线命中的设备.GetType() 和 typeof(Interactable) 的区别是一个是读取预设构造指令写入实例内存的类型对象指针,一个是编译完成后加载内存时将IL代码替换成类型对象指针
                // C#为每个类生成类型对象.   nameof()的作用是编译完成后将最终生成的方法名称替换此处
                const BindingFlags 标志 = BindingFlags.Public | BindingFlags.Static;
                var 类型对象指针 = 光线命中的设备.GetType();

                var 获取交互消息 = typeof(扩展方法).GetMethod(nameof(扩展方法.获取链接选择面板所需的交互消息), 标志, null, new[] { 类型对象指针, typeof(Interactable) }, null);
                var 消息 = (链接选择面板消息结构)获取交互消息.Invoke(null, new object[] { 光线命中的设备, 光线命中的控件 });

                if (消息.控件类型 == 面板类型.逻辑类型控件)
                {
                    if (消息.逻辑类型控件所需的已链接物体 == null)
                    {
                        // ActionMessage:禁用时设置是红色字,不允许创建动作协程
                        动作状态消息 = 默认动作状态消息.Fail("请先设置螺丝链接物体");
                        return 动作状态消息;
                    }
                }

                // 协程函数完美执行结束后,发送一条玩家有动作么=true的AttackWith消息,播放角磨机音效,并调用管道高压炸开协程函数
                if (玩家有动作么)
                {
                    // 播放音效
                    光线命中的设备.PlayPooledAudioSound(Defines.Sounds.ScrewdriverSound, Vector3.zero);

                    var _ = (ILogicable)光线命中的设备;
                    // InputNetwork1DevicesSorted保存着数据网节点物体的所有引用,包括自己
                    var 内部物品表 = 光线命中的设备.InputNetwork1DevicesSorted.Where(d => d != _);

                    if (交互双方.SourceThing is Human 玩家 && 玩家.State == EntityState.Alive && 玩家.OrganBrain != null && 玩家.OrganBrain.LocalControl)
                    {
                        if (绘制链接选择面板("链接选择面板", 内部物品表, (消息.逻辑类型控件所需的已链接物体, 消息.此物体是读取器还是写入器), 消息.控件类型))
                        {
                            var 设置螺丝链接 = typeof(扩展方法).GetMethod(nameof(扩展方法.设置螺丝链接), 标志, null, new[] { 类型对象指针, typeof(Interactable), typeof(ILogicable), typeof(LogicType) }, null);

                            // 当选择某个物品后,执行什么事件
                            按钮点击事件 += 按钮点击消息 =>
                            {
                                // 当选择某个物品后,执行什么事件
                                设置螺丝链接.Invoke(null, new object[] { 光线命中的设备, 光线命中的控件, 按钮点击消息.若是链接控件返回的链接物, 按钮点击消息.若是逻辑类型控件返回的逻辑类型 });
                            };
                        }
                    }
                }
            }

            return 动作状态消息;
        }
        public static GameObject 选项按钮预制体;    // 按钮的层级和控制结构,增加显示物体时就构造此结构
        public static GameObject 滚动区域预制体;    // 原始面板的垂直布局组,用于对所有显示的按钮进行排版,有一个区域适配组件(设置了高度适配) 有一个垂直布局组
        public static GameObject 父级画布;
        public delegate void 链接选择面板事件(ILogicableReference 按钮点击返回_当前焦点);
        public static event 链接选择面板事件 按钮点击事件;
        public static 链接选择面板 单例;
        public static InputPanelState 链接选择面板状态 = InputPanelState.None;
        public static ILogicableReference 当前焦点; // 每个按钮保存了一个可链接物体的引用,点击时触发时间将引用赋给当前焦点
        public static 面板类型 当前面板类型;         // 用于区分链接物螺丝和逻辑类型螺丝,这两个的绘制操作不一样
        public TextMeshProUGUI 面板标题;
        public RectTransform 可链接物滚动区域;      // 滚动区域预制体的实例,显示所有链接物按钮,激活此时,请失活逻辑类型滚动区域
        public RectTransform 逻辑类型滚动区域;      // 滚动区域预制体的实例,显示所有逻辑类似按钮,激活此时,请失活可链接物滚动区域
        public Transform 滚动区域父级;
        public TMP_InputField 面板搜索栏;
        public Button 面板取消按钮;
        public ScrollRect 滚动组件;         // 可链接物滚动区域和逻辑类型滚动区域切换时,需要同步切换滚动组件的滚动区

        // 以下是可链接物滚动区域刷新可链接物按钮所使用的数据
        private Dictionary<long, ILogicable> 已发现节点表 = new Dictionary<long, ILogicable>(64);
        private Dictionary<long, ILogicable> 已显示节点表 = new Dictionary<long, ILogicable>(64);
        private Dictionary<long, ILogicable> 已失效节点表 = new Dictionary<long, ILogicable>(64);
        private Dictionary<long, ILogicableReference> 活跃面板节点表 = new Dictionary<long, ILogicableReference>(64);
        private Queue<ILogicableReference> 休眠面板节点表 = new Queue<ILogicableReference>();

        // 以下是可链接物滚动区域刷新可链接物按钮所使用的数据,逻辑类型对于每个链接物来说是固定的,且不像链接物每个物体都是独立的,因此不需要考虑复用
        private List<ILogicableReference> 活跃逻辑类型面板节点表 = new List<ILogicableReference>(64);

        // 为所有已发现链接物构造逻辑类型按钮,若是逻辑读取器,则IOCheck=可读逻辑类型;若是逻辑写入器,则IOCheck=可写逻辑类型
        // 参数滚动区域:该链接物所有的可读逻辑类型按钮或者可写逻辑类型按钮已排版完成的结构引用
        // 已构造参数节点表: 该链接物所有的可读逻辑类型按钮或者可写逻辑类型按钮的引用,搜索栏需要判断每个按钮的显示文本然后激活或者失活单个按钮
        private Dictionary<(int PrefabHash, IOCheck 读或写), (RectTransform 参数滚动区域, List<ILogicableReference> 面板节点表)> 已构造分支表 = new Dictionary<(int, IOCheck), (RectTransform, List<ILogicableReference>)>(64);
        
        // public void Update()
        // {
        //     UITooltipCanvas.Instance?.DoUpdate();
        // }
        public override void Initialize()
        {
            base.Initialize();
            SetVisible(isVisble: false);        // 初始失活面板

            Localization.OnLanguageChanged += 回调_语言设置变更;  // 当显示逻辑类型滚动区域时,请确保不要刷新文本

            // 面板标题.font = AssetsLoad.单例.内置TMP字体;

            面板取消按钮 = this.transform.GetChild(0).GetChild(4).GetComponent<Button>();
            面板取消按钮.onClick.AddListener(CancelInput);

            面板搜索栏.onSubmit.AddListener(回调_搜索栏查询操作);       // 搜索栏输入时按回车键触发此事件,请根据当前面板类型,作出不同的搜索条件
            面板搜索栏.onValueChanged.AddListener(回调_搜索栏查询操作); // 搜索栏文本变化时触发此事件,请根据当前面板类型,作出不同的搜索条件

            滚动区域预制体 = UnityEngine.Object.Instantiate(可链接物滚动区域.gameObject, null);
            滚动区域预制体.gameObject.SetActive(false);      // 预制体保持失活,不要让Unity引擎运行它

            滚动组件 = transform.GetChild(0).GetChild(7).GetComponent<ScrollRectNoDrag>();

            滚动区域父级 = 可链接物滚动区域.transform.parent;
        }
        private static void 回调_语言设置变更()
        {
            if (当前面板类型 == 面板类型.链接物控件)
            {
                foreach (var value in 单例.活跃面板节点表.Values)
                {
                    value.回调_RefreshString();
                }
            }
        }
        public enum 面板类型
        {
            链接物控件, 逻辑类型控件
        }
        public static bool 绘制链接选择面板(string 面板标题, IEnumerable<ILogicable> 已发现表, (ILogicable 已链接物, IOCheck 读或写) __, 面板类型 面板类型)
        {
            // 此单例当前正在显示中,不可以修改内容
            if (链接选择面板状态 != 0)
            {
                return false;
            }

            当前面板类型 = 面板类型;
            CursorManager.SetCursor(isLocked: false);       // 通知光标管理器不要锁定光标位移
            链接选择面板状态 = InputPanelState.Waiting;      // 请在人物镜头管理器相关函数中插入判断此状态,当状态不为0时,锁定镜头移动

            // 单例.面板标题.text = 面板标题;

            EventSystem.current.SetSelectedGameObject(单例.面板搜索栏.gameObject);  // 通知Unity引擎监控搜索栏操作

            if (当前面板类型 == 面板类型.链接物控件)
            {
                if (单例.逻辑类型滚动区域?.gameObject.activeSelf == true)
                { 单例.逻辑类型滚动区域.gameObject.SetActive(false); }

                单例.滚动组件.content = 单例.可链接物滚动区域;

                if (单例.可链接物滚动区域.gameObject.activeSelf != true)
                { 单例.可链接物滚动区域.gameObject.SetActive(true); }

                单例.更新物体滚动区域(已发现表);
                回调_语言设置变更();                // 立刻刷新一次显示文本
            }
            else
            {
                if (单例.可链接物滚动区域.gameObject.activeSelf == true)
                { 单例.可链接物滚动区域.gameObject.SetActive(false); }

                单例.更新参数滚动区域(__);
            }

            单例.回调_搜索栏查询操作(单例.面板搜索栏.text); 
            单例.SetVisible(isVisble: true);                    // 激活(显示)面板

            //LayoutRebuilder.ForceRebuildLayoutImmediate(单例.RectTransform);
            return true;
        }
        public void 更新参数滚动区域((ILogicable 已链接物, IOCheck 读或写) ___)
        {
            if (已构造分支表.TryGetValue((((Thing)(___.已链接物)).PrefabHash, ___.读或写), out (RectTransform 参数滚动区域, List<ILogicableReference> 面板节点表) __))
            {
                活跃逻辑类型面板节点表 = __.面板节点表;

                if (单例.逻辑类型滚动区域?.gameObject.activeSelf == true)
                { 单例.逻辑类型滚动区域.gameObject.SetActive(false); }

                单例.逻辑类型滚动区域 = __.参数滚动区域;
                单例.逻辑类型滚动区域.SetParent(滚动区域父级, false);
                单例.滚动组件.content = 单例.逻辑类型滚动区域;

                if (单例.逻辑类型滚动区域.gameObject.activeSelf != true)
                { 单例.逻辑类型滚动区域.gameObject.SetActive(true); }

                LayoutRebuilder.ForceRebuildLayoutImmediate(单例.逻辑类型滚动区域);
            }
            else
            {
                // 入口类.Log.LogMessage(((Thing)___.参数物体).DisplayName + "\t" + ___.读或写);

                var 分支父级 = UnityEngine.Object.Instantiate(滚动区域预制体);

                var 面板节点表 = new List<ILogicableReference>();
                foreach (var 支持么 in Logicable.LogicTypes)
                {
                    // 当前是逻辑读取器或其他逻辑读取组件,因此获取所有可读逻辑类型,并为所有类型创建按钮,ILogicableReference持有相关引用
                    if (___.读或写 == IOCheck.Readable && ___.已链接物.CanLogicRead(支持么))
                    {
                        ILogicableReference 面板节点;
                        面板节点 = UnityEngine.Object.Instantiate(选项按钮预制体, 分支父级.transform).GetComponent<ILogicableReference>();
                        面板节点.GetComponent<Button>().onClick.AddListener(面板节点.回调_按钮点击);
                        面板节点.设置(___.已链接物, 支持么, 链接物螺丝么: false);
                        面板节点表.Add(面板节点);
                    }
                    // 当前是逻辑写入器或其他逻辑写入组件,因此获取所有写入逻辑类型,并为所有类型创建按钮,ILogicableReference持有相关引用
                    else if (___.读或写 == IOCheck.Writable && ___.已链接物.CanLogicWrite(支持么))
                    {
                        ILogicableReference 面板节点;
                        面板节点 = UnityEngine.Object.Instantiate(选项按钮预制体, 分支父级.transform).GetComponent<ILogicableReference>();
                        面板节点.GetComponent<Button>().onClick.AddListener(面板节点.回调_按钮点击);
                        面板节点.设置(___.已链接物, 支持么, 链接物螺丝么: false);
                        面板节点表.Add(面板节点);
                    }
                }

                已构造分支表.Add((((Thing)(___.已链接物)).PrefabHash, ___.读或写), (分支父级.GetComponent<RectTransform>(), 面板节点表));
                更新参数滚动区域((___.已链接物, ___.读或写));
            }
        }
        public void 回调_搜索栏查询操作(string str)
        {
            // 统一转换为小写并去除前后空格
            string 条件 = str?.Trim().ToLower() ?? "";

            if (当前面板类型 == 面板类型.链接物控件)
            {
                foreach (ILogicableReference 节点 in 活跃面板节点表.Values)
                {
                    string 节点名称 = 节点.显示名称?.Trim().ToLower() ?? "";
                    bool 是否显示 = string.IsNullOrEmpty(条件) || 节点名称.Contains(条件);
                    节点.SetVisible(isVisble: 是否显示);
                }
            }
            else
            {
                foreach (ILogicableReference 节点 in 活跃逻辑类型面板节点表)
                {
                    string 节点名称 = 节点.物体描述.text.Trim().ToLower() ?? "";
                    bool 是否显示 = string.IsNullOrEmpty(条件) || 节点名称.Contains(条件);
                    节点.SetVisible(isVisble: 是否显示);
                }
            }

            SetInputKeyState(!string.IsNullOrWhiteSpace(面板搜索栏.text));
        }
        public void 更新物体滚动区域(IEnumerable<ILogicable> 已发现表)
        {
            // 根据数据网节点物体数量实时更新面板内容
            // 注: 字典特别适合比较操作

            已发现节点表.Clear();
            foreach (var d in 已发现表)
            {
                已发现节点表.Add(((Thing)d).ReferenceId, d);
            }

            foreach (var 节点 in 已发现节点表)
            {
                if (!已显示节点表.ContainsKey(节点.Key))
                {
                    已显示节点表.Add(节点.Key, 节点.Value);
                    构造或唤醒面板节点(节点.Key, 节点.Value);
                }
            }

            已失效节点表.Clear();

            // 哈希表算法会创建很多占位用的数组格子,不可以用 已显示节点表[下标++] 来访问
            foreach (var 节点 in 已显示节点表)
            {
                if (!已发现节点表.ContainsKey(节点.Key))
                { 已失效节点表[节点.Key] = 节点.Value; }
            }

            foreach (var 节点 in 已失效节点表)
            {
                已显示节点表.Remove(节点.Key);
                休眠面板节点(节点.Key);
            }
        }
        private void 构造或唤醒面板节点(long id, ILogicable device)
        {
            // 不同的物体也就是缩略图引用,物体引用和物体描述不同,改的东西不多,可以复用
            // 优先从隐藏池中获取物体信息按钮

            ILogicableReference 面板节点;

            if (休眠面板节点表.Count > 0)
            {
                面板节点 = 休眠面板节点表.Dequeue();
            }
            else
            {
                面板节点 = UnityEngine.Object.Instantiate(选项按钮预制体, 可链接物滚动区域).GetComponent<ILogicableReference>();
                面板节点.GetComponent<Button>().onClick.AddListener(面板节点.回调_按钮点击);
            }

            面板节点.设置(device, LogicType.None, 链接物螺丝么: true);
            活跃面板节点表.Add(id, 面板节点);
            面板节点.gameObject.SetActive(true);
        }
        private void 休眠面板节点(long id)
        {
            // 将不使用的物体信息按钮存放到隐藏池中
            if (!活跃面板节点表.TryGetValue(id, out var 面板节点)) { return; }
            面板节点.SetActive(false);
            活跃面板节点表.Remove(id);
            // 面板节点.设置目标(null, Recipe.INVALID);
            休眠面板节点表.Enqueue(面板节点);
        }
        public static void CancelInput()
        {
            // 回调函数,当点击了关闭面板按钮时调用,然后重置面板为初始状态,并关闭面板
            // 开启或者关闭鼠标控制屏幕光标移动
            CursorManager.SetCursor(isLocked: true);
            // 请在人物移动组件代码中加入此面板状态判断,在面板工作时屏蔽掉鼠标移动导致的人物视线乱晃
            链接选择面板状态 = InputPanelState.Cancelled;

            单例.SetVisible(isVisble: false);
            单例.面板搜索栏.text = string.Empty;

            当前焦点 = null;
            按钮点击事件 = null;

            链接选择面板状态 = InputPanelState.None;
        }

        public static void Submit()
        {
            // 回调函数,当点击了物体信息按钮时调用,将该物体引用赋给螺丝
            // 然后重置面板为初始状态,并关闭面板
            if (按钮点击事件 != null)
            {
                // 请在调用 绘制链接选择面板 返回true时,对此事件写入回调
                按钮点击事件(当前焦点);
            }

            CursorManager.SetCursor(isLocked: true);
            链接选择面板状态 = InputPanelState.Submitted;

            单例.SetVisible(isVisble: false);
            单例.面板搜索栏.text = string.Empty;

            当前焦点 = null;
            按钮点击事件 = null;

            链接选择面板状态 = InputPanelState.None;
        }
    }

    [HarmonyPatch(typeof(InputWindowBase), nameof(InputWindowBase.IsInputWindow), MethodType.Getter)]
    public class InputWindowBase_IsInputWindow_PrefixPatch
    {
        [HarmonyPrefix]
        public static bool IsInputWindow(ref bool __result)
        {
            // 增加链接选择面板的状态判断,当返回true时,鼠标位移不会导致人物视线位移
            __result = 链接选择面板.链接选择面板状态 == InputPanelState.Waiting || InputSourceCode.InputState == InputPanelState.Waiting || InputWindow.InputState == InputPanelState.Waiting || InputPrefabs.InputState == InputPanelState.Waiting;
            return false;
        }
    }

    [HarmonyPatch(typeof(InputWindowBase), nameof(InputWindowBase.Cancel))]
    public class InputWindowBase_Cancel_PrefixPatch
    {
        [HarmonyPrefix]
        public static bool Cancel()
        {
            // 当游戏处于暂停状态时,不要显示链接选择面板
            if (KeyManager.InputState == KeyInputState.Paused && InputSourceCode.InputState == InputPanelState.Waiting)
            {
                return false;
            }

            InputWindow.CancelInput();
            InputSourceCode.CancelInput();
            InputPrefabs.CancelInput();
            链接选择面板.CancelInput();

            return false;
        }
    }
}