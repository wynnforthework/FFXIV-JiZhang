# AutoFATEGrind 源码 UI 体系审计

分析日期 2026-10-31；源目录 `D:/UGit/FFXIV-AutoFATEGrind/AutoFateGrind/Windows`。本文根据实际源码调用链分析，未运行参考插件；不以截图证明动画或交互。下列“参考已有”与“账本缺口”分别描述，不能混称已实现。

## 1. 架构与职责

`Styling / Layout` 定义语义 token 和逻辑尺寸；`Fonts / TextDraw` 提供字体、测量、截断；`Paint` 提供绘制原语；`Motion` 保存跨帧状态；`Components` 把 Hit、Motion、Paint、TextDraw 组合成控件；`Sections` 编排业务区域；`Pages` 编排页面；`Shell/AppWindow` 管理窗口、轨道、滚动区、动作底座与生命周期。

这条调用链是视觉一致性的根源。只读 Styling.cs 并改颜色，绕过 Components 与 Motion，会得到外形相近但行为不同的界面。账本目前 LedgerView 仍较集中；应按职责拆出 Shell、Paint、Motion、控件，而不照搬 FATE 业务逻辑。

## 2. 视觉语言与尺寸

静态规范保存在本机 skill 的 `references/autofategrind.md`；本报告以下聚焦调用链、行为与差距。深蓝灰的四级表面负责空间，紫色负责品牌与交互，薄荷／玫瑰／琥珀负责语义。强、次、弱、静默文字四级，边线远弱于正文。Styling.ForegroundOn 根据相对亮度阈值 .65 选择前景；不能所有彩色底都用同一种字色。

Paint.Glass 顺序是可选 Shadow → 顶点渐变 → TopLight → Stroke；上层比下层亮，表面随 accent tint 和 hover 改变。它没有实时背景采样，所谓玻璃是渐变、透明叠加、微边线和发光，不是真模糊。普通 Surface 不必承受每张卡 5 层阴影；Hero 才明显升起与发光。

逻辑尺寸乘 ImGuiHelpers.GlobalScale，正文、图标有各自托管字体。外壳 52 顶栏、64 轨、26 内容留白、6 右侧 inset；底座 Grind 为 78，其他页运行中 62，不运行时无底座。主要按钮 54 高、统计格 66、设置行 40、开关 40×22。账本的底部状态栏属于适配，不是参考插件原有形态。

Ambient 的三组 5 层圆以紫／蓝／粉构成环境光。中心按 14–23 秒的不同正弦相位缓慢移动，半径为窗口宽 .40–.45，峰值 alpha .035–.055。不是随机闪烁或整窗渐变。

## 3. 动效内核：连续值与离散事件分开

Motion.cs 的连续值使用 `current += (target-current)*(1-exp(-speed*dt))`。dt 截断至 .05 秒，稳定 ID 保存状态，接近 .0005 时吸附。首次出现直接初始化到目标，避免控件首次渲染全部飞入。speed 是趋近速率，不是毫秒；18 对应约 167ms 达到95%，16 约187ms，30约100ms。

离散进入由 TickCount64 时间戳计算，使用 EaseOutCubic；状态切换由 Transition 记住上次离散值。首次不强行播放，真实变化才重置起点。PushReveal 同时处理 Alpha 与小幅 Y 位移，作用域退出恢复样式。EaseInOutCubic 用于窗口折叠；EaseOutBack、Smoothstep 在内核可用，不能据此声称所有组件都用了弹簧。

| 对象／源码 | 触发与参数 | 实际视觉行为 |
|---|---|---|
| AppWindow 页面 | OnOpen／Show，260ms，12px | 淡入、向上就位，切页重置滚动 |
| AppWindow 折叠 | 280ms × 剩余路程比例，EaseInOutCubic | 高度变到52，内容 Alpha 随 collapse 下降，过渡中锁定 resize |
| Motion 通用切换 | 默认240ms／10px | 新状态淡入位移 |
| GrindPage | running 切换320ms | 运行态与待命态内容重入 |
| NavRail | selectedIndex，speed16 | 同一个选中底板连续滑动到目标，不是每项各自闪开 |
| Hover | speed18 | hover 亮度、底色、描边逐步出现及消退 |
| Segmented | indicator X、width，speed18 | 底板位置与宽度一同移动，支持不等宽选项 |
| ToggleSwitch | on16，hover18 | 滑块位置、轨道色、描边、外光一起插值 |
| HeroButton | hover18，press30 | hover升2px，press回压2px；命中区保持原位 |
| Dropdown | reveal200ms，逐行14ms，最多前10行 | 面板从42%高度展开，箭头翻转，选项9px进入 |
| SearchField | focus16 | 输入焦点边线及搜索图标变亮；焦点是上一帧缓存，不是 hover |
| RunningPanel | progress10、goal6 | 进度与目标环平滑追随实际状态 |
| 周期状态 | Pulse600／800／2600／1900，Orbit3400ms | 运行徽标、呼吸光、旋转扫环分别传达状态 |

`Styling.Pulse` 输出0..1，`Motion.Wave` 是正弦。不要让所有组件共用一套机械同步的相位，也不要给货币金额添加会误导当前余额的慢速数字插值。

## 4. 控件交互与几何契约

Hit.Area 先分配真实 InvisibleButton，返回 Clicked／Hovered／Held；禁用时仅 Dummy，不能点击，并在 hover 时使用手形鼠标。绘制偏移和尺寸变化不应让点击区跳动。ID 由 ImGui ID stack 与 salt 区分；不能用会随显示金额变化的字符串当稳定动画键。

NavRail 是滑动选中底板＋轨边短标，非选中 hover 另画底，图标颜色插值；运行、缺失依赖、未读日志徽标各有语义。SidebarTab 则是设置二级菜单，不是主导航实现。

Segmented 先测量每个选项图标与文字；能等宽则等宽，否则依据内容分配，空间不够时压缩并截断。绘制当前选中底板后创建各个选项 hit，结束用 Dummy 精确恢复游标。只有按颜色改 Button 不等价。

Dropdown 不只是给 Combo 换皮：关闭 field、popup、选项三层都自绘；状态包含打开时间、搜索、键盘高亮和滚动目标。面板最大列表300，依据视口上下空间放置，宽面板右对齐触发器并 clamp。外侧点击沿 ImGui popup 生命周期关闭，需处理同一帧 open 状态。详细文本会改变行高，面板 shadow 有显式扩展 clip。选项可搜索并有无匹配状态。

SettingsGroup 用 ChannelsSplit(2) 先画内容、后按测得高度在背景通道画卡片，再合并。SettingsRow 标签左对齐、控件右对齐，40高，组内细线分隔；help 只在 row hover 时出现，说明按卡片内右边界换行。布局靠内容测量，不是固定高度卡片塞入所有设置。

Tooltip 特别重要：外壳零padding会传给提示窗，因此专用作用域恢复14×11padding、320换行宽、5行间距、字体与背景；空字符串直接返回。不能依赖外壳默认状态。账本目前直接 BeginTooltip，仍缺统一的padding作用域。

## 5. 字体体系

Fonts.cs 使用 Caption1.0／Body1.125／Headline1.25／Title1.625，相对 Dalamud 默认字号；图标分 Body／Large1.625／Display2.25。Latin 主字体是 NotoSans-Medium-Latin.ttf，中文 Noto CJK补缺，中文TTC face2，日文face0。动态游戏字形、语言和默认字体变化会触发重建；Dispose解绑事件并释放句柄。TextDraw 测量、截断和绘制在同一字体scope内，最大16项截断缓存。

可明确缩小已覆盖目标大小的图标字体；数字正文则应按目标大小烘焙。不要把“禁止放大小字位图”误写成禁止所有 drawlist 显式字号。

账本目前用 Dalamud 默认字体＋三档字号，预览用微软雅黑：这只能验证布局与烘焙策略，不能声称已经与参考字体一致。要复现字形，需取得字体及许可证、采用相同Latin/CJK合并和范围策略并验证真实运行。

## 6. 可访问性、生命周期与源码局限

Motion.Approach、Reveal 和 AppWindow 折叠会检查 UiBuilder.ShouldUseReducedMotion 并立即到终态。但是 Ambient.Wave、Styling.Pulse、ProgressRing.Sweep 等直读周期函数没有统一检查。复用时应给所有持续动画明确的静止态，避免把局部支持写成全部支持。

动画状态字典是静态且从当前文件看无统一清理；动态ID可能增长，不能把它当无限列表模板。窗口析构、字形重建、皮肤栈与字体栈释放需按实际生命周期管理。Alpha 通过 ImGui 样式控制时，自绘颜色须使用 GetColorU32 或显式乘样式alpha；直接 ColorConvertFloat4ToU32 会绕过淡入，不能以为压 Alpha 就全部生效。

## 7. 当前小肥记账差距与实施顺序

| 能力 | 参考插件 | 账本当前状态 |
|---|---|---|
| Shell／背景 | 顶栏＋窄轨＋动态环境光 | 几何已接近，背景目前静止 |
| 导航与胶囊 | 跨帧滑动与颜色插值 | 点击后直接跳变 |
| 页面／折叠 | 260／280ms及正确中断 | 页面无reveal，折叠瞬间改变高度 |
| 控件系统 | Hit／Paint／Motion独立复用 | 多数仍内联在LedgerView |
| Settings | 自适应分组＋右对齐开关／自绘dropdown | 默认Checkbox／Combo／Slider，尚未统一 |
| 字体 | NotoLatin/CJK、4档文本、多档图标 | 默认字体3档；图标目前手绘 |
| Tooltip | 独立padding与字体scope | 部分直接BeginTooltip |
| 状态动效 | 有语义的呼吸／进度／徽标 | 基本静态 |
| Reduced motion | 过渡支持，持续动画部分遗漏 | 尚未引入完整策略 |

先建立可测试的 Motion（稳定ID、dt、Reduced、生命周期）和统一 Hit／Tooltip，再实现轨道、胶囊、页面进入；随后做设置行、toggle和dropdown；最后做周期环境光与非关键状态提示。保持余额、入账和筛选结果即时准确。每项完成要有独立的动态证据，静态截图不证明任何动画。

## 8. 动态验收

对导航从首项到末项及反向切换录制；检查指示板连续移动、hover可反向衰减。折叠未完成时立即展开，窗口应从当前位置继续，不跳到端点。下拉靠屏幕底部打开、搜索无结果、键盘高亮滚动、外部点击与禁用分别核对。对15／30／60／144fps及单帧停顿检查无爆跳。打开减少动态效果后，过渡直接到终态、装饰持续动画静止，操作仍有效。

离线验证必须采集多个时间点（例如0／80／160／320ms）或短视频，模拟鼠标移入、按下、释放、离开。单张PNG仅用于字体、裁切与布局，不能拿来宣称动效验收。真实字体、系统缩放和游戏窗口行为仍需在Dalamud检查。


## 本轮实现进度（2026-10-31）

已接入每窗口 UiMotion 与可反向 FoldMotion：导航16／胶囊18连续插值，页面260ms／12px，折叠280ms比例时长，背景不同周期缓慢移动；持续背景与过渡均接入 Dalamud Reduced motion。自绘颜色改用 GetColorU32，使样式 Alpha 对自绘也生效。开关、下拉框与动作按钮提取到 LedgerControls；设置按内容分通道绘制背景，不再使用固定高度；Tooltip恢复独立padding。

下拉框本轮有200ms高度展开、箭头翻转、行级错峰淡入、视口上下定位，沿用原生popup与Selectable的关闭／键盘语义；尚无参考插件的搜索模式或详情行，不宣称完整复刻所有组件。字体仍是Dalamud默认字体，尚未复制Noto Latin字形方案。

实际离线原生ImGui输入覆盖：点击导航、下拉选择支出、开关保存、7/14/30日切换、折叠中反向、完整折叠到52px、Reduced下展开与切页、关闭。导出213帧状态和选定帧绘制数据；核心测试另覆盖帧率趋近、停顿、中断反向与Reduced。游戏内字体、ESC和平台窗口行为仍待用户验收。


## 独立视觉修订（2026-10-31）

用户撤回视觉复现方向，要求仅参考规范和动效。小肥记账现改为 Paper & Pine 暖纸白、墨绿、铜金与顶部文字导航，移除紫色光晕、玻璃渐变和图标侧栏。前文标志性尺寸描述用于来源分析，不再作为本项目外观验收标准。可编辑矢量设计板及实际渲染对照见 [design/README.md](design/README.md)。保留托管字体、交互状态、Reduced motion、指数趋近、260ms 切页与280ms 反向折叠。周期选项现为当月/当年/所有，组成图改为矩形树图。
