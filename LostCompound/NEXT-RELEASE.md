# 下次发布：v0.0.4-alpha（计划 2026-11 第 1 周，在 GitHub 发）

> 这是发布清单，不是设计文档。发完就把这一节删掉 / 整份文件删掉。

## 为什么是 0.0.4 而不是重发 0.0.3

`v0.0.3-alpha` 的 tag 与 release **已经推出去**了（tag 指向 `d3edf21`）。
移动已发布的 tag 会让「同一个版本号对应两种内容」，所以新内容发新号。

## v0.0.4 相对 v0.0.3 的内容

自 `d3edf21` 以来：

| 提交 | 内容 |
|---|---|
| `d6685c3` | **超雄模式（调试 127）修好**：原先时长传 `-12`，被 `Math.Max(turns,0)` 夹成 0，挂上就判定到期被清掉。改用 `Buff.Permanent`，并改正提示语（它走 StrBonus，不改 BaseStr） |
| `8abf7c6` | 文档：镜子轮换「吞掉落差」是刻意设计（用户裁定），并记录「镜子 + 存读档刷物品」的验证结论（刷不出来，只会变少） |
| `b887010` | **六件道具战斗外可用**：药 / 水瓶 / 碘片 / 金属 / 镐子 / 硫酸（热硫 / 热硫罐 / 毒气弹 / 红磷 / 白磷 / 压缩沙瓶仍限战斗内） |

## 发布步骤（照抄即可）

```powershell
cd C:\Users\dayli\RiderProjects\LostCompound

# 1. 版本号改成 0.0.4-alpha（csproj 里四处）
#    Version / AssemblyVersion(=0.0.4.0) / FileVersion(=0.0.4.0) / InformationalVersion

# 2. 打包（必须用这个 dotnet，裸 dotnet 没有 SDK）
C:\Users\dayli\.dotnet\dotnet.exe publish LostCompound\LostCompound.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=embedded `
  -p:EnableCompressionInSingleFile=false -o publish

# 3. 确认 exe 里带了版本与提交号
(Get-Item publish\LostCompound.exe).VersionInfo | Select FileVersion,ProductVersion

# 4. 更新说明
#    - RELEASE-v0.0.4-alpha.md      （极简版，贴 GitHub Release 正文）
#    - CHANGELOG-v0.0.4-alpha.txt   （完整版，随包发）
#    - 复制一份到 publish\

# 5. 提交 + 打 tag + 推
git add -A; git commit -m "release: v0.0.4-alpha"
git tag -a v0.0.4-alpha -m "LostCompound v0.0.4-alpha"
git push github master; git push github v0.0.4-alpha
```

## 发布前必须人工确认（我做不了）

- [ ] 双击 `publish\LostCompound.exe`，确认**没有 ANSI 乱码**（窗口右上角的 `[41;1H` 之类）
- [ ] 点窗口 **×**，再重新打开，确认「继续游戏」能读回进度
- [ ] 战斗中点 × ，确认**存的是最近的安全点**（重开后回到战斗前的那一回合）
- [ ] 调试：战斗中输 `255` 开 debug，再输 `127`，确认超雄 buff **留在 Buff 框里**
- [ ] 地图上输 `5`，确认「药 / 水瓶 / 碘片 / 金属 / 镐子 / 硫酸 / 镜子」标 `[可用]`，
      热硫 / 毒气弹 等标 `[仅战斗中]`

## 已知遗留（不阻塞发布）

- 30 行高的终端上，8×8 / 9×9 地图会被压矮（状态框 6 行 + Buff 框 4 行占掉了高度）。
  窗口拉到 35 行以上即可。
- 换层时在「自选环境」菜单按 ×，存的是**进 Boss 房之前**的安全点（Boss 白打）。
  要修得加「楼梯」状态，未做。
- `Player.Name` 是死字段（显示走 `Profile.DisplayName`），删它要动 public API，未做。

## 更远的待办（不在 0.0.4 范围）

- 难度系统（困难 / fatal）—— 卡着配方 2 的 33/34、所有「困难」数值
- 辐射系统（秒级计时器 + Boss 死亡回调）—— 卡着放射性区域、辐射剑、盖革计数器、电子云
- 矿洞 / 墓地 / 雾都城市
- 配方 2 / 3 的全部道具
