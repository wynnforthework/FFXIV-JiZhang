# 本地开发

需要 Windows x64、.NET 10 SDK、Python 3.12+ 和国服 Dalamud API15。先构建并验证，再按 [验收清单](ACCEPTANCE.md) 完成游戏内测试。

```powershell
./scripts/Build.ps1 -DalamudHome "$env:APPDATA/XIVLauncherCN/addon/Hooks/dev"
```

也可通过 `./scripts/Get-Dalamud.ps1` 获取仓库锁定并校验 SHA256 的参考程序集，再运行 `./scripts/Build.ps1`。

在 `/xlsettings` 的开发插件设置中添加 `src/JiZhang/bin/Release/JiZhang.dll` 的绝对路径，重新扫描并启用开发插件。构建产物 `artifacts/JiZhang.zip` 包含 SQLite Windows x64 原生库及私有依赖，不含 Dalamud 或游戏程序集。

## 截图

预览宿主与插件共享 LedgerView；使用示例数据和微软雅黑，实际游戏采用 Dalamud 默认托管字体。图表及设置截图属于离线原生渲染，不作为国服交易验收证据。

```powershell
$env:DALAMUD_HOME = "$env:APPDATA/XIVLauncherCN/addon/Hooks/dev"
dotnet run --project tools/JiZhang.Preview -- artifacts/ui-preview
python scripts/render-preview.py artifacts/ui-preview marketing-overview transactions settings
```

渲染脚本依赖 Pillow 与 numpy。README 高清截图保存在 `images/screenshots/`。插件管理器用等比缩小到730×380以内的 `images/image1.png`、`image2.png`、`image3.png`；开发插件直接读取 DLL 旁的这些文件，第三方插件清单的 `ImageUrls` 也指向对应小图。构建和 ZIP 必须包含这些文件，打包时校验尺寸。卫月会缓存已加载或加载失败的图片；本机日志已确认旧大图被拒绝后留下失败缓存。源码的 OnOpen 只刷新仓库和扫描开发插件，不调用 ClearIconCache，因此关闭再打开安装器、禁用再启用插件都不保证刷新图片；重启游戏后缓存才会重新建立。替换时需检查标题、按钮、中文字体及滚动裁切。

动效输入验证：

```powershell
dotnet run --project tools/JiZhang.Preview -- artifacts/ui-motion --motion
```

输出213帧状态与选定帧绘制数据，验证导航、下拉、开关、周期选择、折叠反向及减少动态效果。

## 发布

提交与 PR 自动构建和测试。正式发布前更新版本号，完成国服验收并填写 `release-validation.json`，再推送与 AssemblyVersion 匹配的版本标签。发布工作流生成 ZIP、SHA256 和订阅清单；验收未通过时不发布。

## 模块

- `JiZhang.Core`：余额账本、证据关联、分类、SQLite 与 CSV。
- `JiZhang`：Dalamud 服务适配、命令和 ImGui 界面。
- `tests`：不依赖游戏的核心测试。
- `scripts`：引用程序集下载、构建、截图渲染及打包。


### AutoDuty 对照与本机排查

AutoDuty 的 `AutoDuty/AutoDuty.json` 使用标准 `ImageUrls` 数组指向图片服务，并未通过插件代码向安装器注入截图。小肥记账采用同一清单机制，另为开发插件提供 `images/image1.png` 至 `image3.png`。

本机当前启用的开发插件路径为 `artifacts/JiZhang/JiZhang.dll`；排查时必须检查该路径旁的清单和图片，不能只检查 `bin/Release`。本轮已核实当前目录中的三张图片符合尺寸限制、与清单远程 URL 的内容一致且远程可访问。日志中08:18的旧图错误分别为1040×1000、1040×780、1040×960；之后重载插件没有触发图片重新加载。故不能把文件修复后直接视作安装器画面已验证，需重启后在安装器展开小肥记账条目检查。
