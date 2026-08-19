# WPF 原型代码分布规划

对照 `task.md` 全阶段。原则：**壳薄、页独立、可复用抽控件、演示数据单独放**。  
现阶段不做完整 MVVM / DI；以「能分清职责、后期好搬」为准。

## 结论

| 问题 | 建议 |
|------|------|
| 所有功能写在一个文件？ | **否**。`MainWindow` 只保留壳与编排 |
| 现在就要大拆？ | **不必一次拆完**；按阶段落地时再拆，避免空文件夹 |
| 最重的一块？ | **阶段 4 试验项目页**必须独立成多文件，不能塞进 MainWindow |

---

## 目标目录

```text
Cmie.MotorTest.Wpf/
├── App.xaml / App.xaml.cs          # 应用入口；全局资源入口
├── MainWindow.xaml(.cs)            # 仅：壳布局 + 导航编排 + 打开全局弹层
│
├── Themes/
│   ├── Brushes.xaml                # 浅/深色画刷（可从 App.xaml 拆出）
│   ├── Controls.xaml               # TopButton / SideButton / Dropdown 等
│   └── ThemeService.cs             # ApplyTheme / 记忆主题（从 MainWindow 迁出）
│
├── Shell/                          # 顶栏、侧栏、底栏相关逻辑（可选逐步迁）
│   ├── SidebarController.cs        # 收起动画、宽度记忆
│   ├── TopMenuController.cs        # 试验操作 / 帮助 Popup
│   └── StatusBarController.cs      # 阶段 9 动态文案（可后建）
│
├── Views/                          # 每个业务页一对 XAML + cs
│   ├── HomePage.xaml(.cs)          # 阶段 2
│   ├── NewTestPage.xaml(.cs)       # 阶段 3
│   ├── ProjectPage.xaml(.cs)       # 阶段 4（页本身要再拆，见下）
│   ├── RealtimePage.xaml(.cs)      # 阶段 5
│   ├── SettingsPage.xaml(.cs)      # 阶段 6
│   ├── ReportPage.xaml(.cs)        # 阶段 7
│   └── UsersPage.xaml(.cs)         # 阶段 8
│
├── Views/Project/                  # 阶段 4 子块（最重，必须拆）
│   ├── TestTreePanel.xaml(.cs)     # 4.2 试验树
│   ├── StageHost.xaml(.cs)         # 4.1 中间舞台
│   ├── CompactMetricsPanel.xaml(.cs)# 4.6 紧凑实时
│   ├── PinnedMetricsBar.xaml(.cs)  # 4.7 重点指标（5.3 复用）
│   ├── DockChipBar.xaml(.cs)       # 4.4 Dock 芯片
│   └── TestFloatingWindow.xaml(.cs)# 4.5 试验浮窗
│
├── Views/Settings/                 # 阶段 6 子页（Tab/子导航）
│   ├── RatioSettingsView.xaml(.cs)
│   ├── ModelParamsView.xaml(.cs)
│   ├── CommsSettingsView.xaml(.cs)
│   └── TempSettingsView.xaml(.cs)
│
├── Controls/                       # 跨页可复用小控件
│   ├── MetricCard.xaml(.cs)        # 首页/实时共用指标卡
│   ├── OverlayHost.xaml(.cs)       # 遮罩弹层壳（登录、用户编辑）
│   └── ...
│
├── Dialogs/                        # 全局或半全局弹层
│   ├── LoginOverlay.xaml(.cs)      # 从 MainWindow 迁出（阶段 1 已有）
│   └── UserEditDialog.xaml(.cs)    # 阶段 8
│
├── Services/
│   ├── AppToast.cs                 # ✅ 已有
│   ├── NavigationService.cs        # Navigate(key)；页注册（可选）
│   ├── UserSession.cs              # 当前用户、登录态
│   └── AppSettingsStore.cs         # 侧栏收起、主题等本地记忆
│
├── Models/                         # 演示用数据结构（无业务后端）
│   ├── TestItem.cs
│   ├── MetricReading.cs
│   ├── ReportItem.cs
│   └── UserAccount.cs
│
└── Demo/                           # 假数据工厂（与 UI 分离）
    ├── DemoMetrics.cs
    ├── DemoTestTree.cs
    └── DemoUsers.cs
```

---

## 各层职责（一句话）

| 位置 | 放什么 | 不放什么 |
|------|--------|----------|
| **MainWindow** | 壳 XAML；注册页；`Navigate`；打开登录/退出确认；把事件转给 Service/Page | 试验树、浮窗、设置表单、报表勾选逻辑 |
| **Views/\*Page** | 该页自己的布局与交互 | 改主题画刷、侧栏宽度 |
| **Views/Project/\*** | 试验项目页的子面板 | 其它页面的导航 |
| **Controls** | 2 个以上页面会用的 UI 块 | 只在一页出现的大布局 |
| **Dialogs** | 遮罩弹层 | 页面内嵌小面板 |
| **Services** | 无视觉状态：导航、Toast、会话、本地设置 | 直接操作大量控件树（可持有弱引用/回调） |
| **Models / Demo** | 数据形状与假数据 | UI 事件 |

---

## 与 task.md 阶段对应

| 阶段 | 主要落点 | MainWindow 是否加码 |
|------|----------|---------------------|
| 0～1 壳/主题/菜单/登录/侧栏 | 已在 MainWindow + App；**建议下一刀**：Login→`Dialogs`，主题→`ThemeService`，侧栏→`SidebarController` | 只减不增 |
| 2 首页交互 | `Views/HomePage` + 回调 `Navigate` | 几乎不改 |
| 3 新建试验 | `Views/NewTestPage` | 注册路由即可 |
| 4 试验项目 | `Views/ProjectPage` + `Views/Project/*` | **禁止**堆进 MainWindow |
| 5 实时数据 | `Views/RealtimePage`；重点指标复用 `PinnedMetricsBar` | 否 |
| 6 设置 | `Views/SettingsPage` + `Views/Settings/*` | 否 |
| 7 报表 | `Views/ReportPage` | 否 |
| 8 用户 | `Views/UsersPage` + `Dialogs/UserEditDialog`；会话用 `UserSession` | 仅联动头像文案 |
| 9 体验 | `AppSettingsStore` / `StatusBarController` | 薄封装 |

---

## MainWindow 应保留的「薄」API

页面与弹层通过这些与壳通信即可（后续可换成接口）：

```csharp
// 已有 / 建议保持公开
void Navigate(string pageKey);
void ShowToast(string text);

// 建议逐步补齐，避免页面直接摸壳控件
UserSession Session { get; }
void OpenLogin();
```

首页「继续试验 / 新建试验」→ 调 `Navigate("project")` / `Navigate("new-test")`，**不要**在 HomePage 里操作侧栏按钮。

---

## 拆分节奏（避免空架子）

1. **做阶段 2 前（推荐先做一小步）**  
   - `LoginOverlay` → `Dialogs/`  
   - `ApplyTheme` → `Themes/ThemeService.cs`  
   - 侧栏动画 → `Shell/SidebarController.cs`  

2. **阶段 3**  
   - 新建 `NewTestPage`，删掉对应 Placeholder  

3. **阶段 4 一开始就按子面板建文件**  
   - 先 `ProjectPage` 三栏空壳，再逐个填 `TestTreePanel` / 浮窗等  

4. **阶段 5～8**  
   - 一页一对文件；设置/用户按上表拆子视图  

5. **Models/Demo**  
   - 第一次需要列表数据时再建立，不要提前空转  

---

## 文件体量经验阈值（原型）

| 类型 | 建议上限 | 超了就拆 |
|------|----------|----------|
| `*.xaml.cs` | ~300～400 行 | 按区域拆 UserControl / Controller |
| `MainWindow.xaml.cs` | ~250 行（编排后） | 壳逻辑外迁 |
| 单个 Page XAML | 一屏能读完结构 | 子 UserControl |

---

## 当前债务（已知应外迁）

已在 `MainWindow.xaml.cs` 中、建议优先搬走：

- 主题切换 `ApplyTheme` / `SetBrush`
- 侧栏收起动画与本地文件读写
- 登录 Overlay 整段 UI + 校验
- 顶栏三个 Popup 的开关逻辑（可留 XAML 在壳，逻辑进 Controller）

已做得对的部分：

- `Views/HomePage`、`Views/PlaceholderPage`
- `Services/AppToast.cs`
- 样式集中在 `App.xaml`（后续可再拆 ResourceDictionary）
