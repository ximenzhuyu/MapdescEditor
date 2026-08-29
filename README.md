# MapdescEditor

传奇客户端 `MapDesc1.dat` 地图标注预览与编辑工具。作者联系方式：QQ8957277。

## 功能

- 只读取 `.map` 索引，不依赖 WIL、WIX、PAK 等素材库。
- 支持每格 12、14、36 字节地图格式，以及带尾随数据的旧12字节地图。
- 黑色显示禁止移动区域，绿色显示人物可移动区域。
- 按 `48/32`、`32/32` 比例生成小地图坐标投影。
- 读取服务端 `MapInfo.txt`，支持 `别名|地图编号` 语法和同名物理地图。
- 读取、编辑并以 GBK 编码保存客户端 `data/MapDesc1.dat`。
- 支持地图名和说明搜索、颜色选择、大小地图双模式、键盘微调坐标。
- 地图区域右键可直接添加描述点。
- 保存到客户端前可自动生成时间戳备份；备份失败时中止覆盖。
- 自动记忆上一次客户端目录和服务端 MapInfo 路径。

## 基本操作

1. 选择包含 `Map` 和 `data` 文件夹的客户端目录。
2. 选择服务端 `Mir200/Envir/MapInfo.txt`。
3. 从左侧选择逻辑地图与对应物理 `.map`。
4. 在地图上左键选点，或右键选择“在此添加描述点”。
5. 在右侧管理说明、颜色、坐标和模式。
6. 点击“保存到客户端”；启用自动备份时会先备份原文件。

地图画布支持滚轮缩放、右键或中键拖动。选中备注后可用方向键移动；`Alt + 方向键` 可同步移动同位置的模式0和模式1记录。

## 构建环境

- Windows
- .NET 10 SDK
- Windows Forms

```powershell
dotnet build -c Release
```

生成独立的 Windows x64 单文件版本：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```
