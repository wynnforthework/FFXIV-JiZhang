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

渲染脚本依赖 Pillow 与 numpy。介绍截图保存在 `images/screenshots/`；插件清单的 `ImageUrls` 指向同一组图片。替换时需检查标题、按钮、中文字体及滚动裁切。

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
