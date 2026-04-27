# 电脑 DIY 硬件压力测试工具

一款绿色免安装的 Windows 硬件压力测试工具，专为电脑 DIY 售后服务设计。

## 特点

- ✅ **绿色免安装**：单个 EXE 文件，双击即可运行
- ✅ **多功能测试**：支持 CPU、内存、GPU 压力测试
- ✅ **快速检测**：可自定义测试时长，快速发现硬件问题
- ✅ **轻量级**：单文件发布，体积小，启动快
- ✅ **开源免费**：基于 MIT 协议开源

## 下载

从 [Releases](https://github.com/XYXS-ZHXD/hardware-stress-test/releases) 页面下载最新版本的 `HardwareStressTest.exe`

## 使用方法

### 命令行参数

```
HardwareStressTest.exe <测试类型> [持续时间秒] [线程数]
```

### 测试类型

| 参数 | 说明 |
|------|------|
| `cpu` | CPU 压力测试（多线程计算压力） |
| `memory` 或 `mem` | 内存压力测试（大内存分配与读写） |
| `gpu` | GPU 压力测试（矩阵运算压力） |
| `all` | 全部测试（依次执行 CPU+ 内存+GPU） |

### 参数说明

- **持续时间秒**：测试持续时间，默认 60 秒
- **线程数**：CPU 测试线程数，默认为 CPU 核心数

### 使用示例

```bash
# CPU 压力测试 2 分钟
HardwareStressTest.exe cpu 120

# 内存压力测试 5 分钟
HardwareStressTest.exe memory 300

# GPU 压力测试 1 分钟
HardwareStressTest.exe gpu 60

# 全部测试 5 分钟
HardwareStressTest.exe all 300

# CPU 测试 3 分钟，使用 8 线程
HardwareStressTest.exe cpu 180 8
```

## 编译说明

### 环境要求

- .NET 8.0 SDK
- Windows 10/11 x64

### 本地编译

```bash
# 恢复依赖
dotnet restore

# 发布单文件版本
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ./publish
```

编译后的文件位于 `./publish/HardwareStressTest.exe`

### GitHub Actions 自动编译

每次推送到 `main` 分支或创建标签时，会自动编译并上传构建产物。

创建标签发布新版本：

```bash
git tag v1.0.0
git push origin v1.0.0
```

## 测试说明

### CPU 压力测试

- 使用多线程执行复杂数学计算（三角函数、平方根等）
- 持续占用 CPU 资源，检测 CPU 稳定性和散热能力
- 建议测试时间：3-5 分钟

### 内存压力测试

- 分配约 75% 的系统可用内存
- 执行内存写入、读取、验证循环
- 检测内存稳定性和数据完整性
- 建议测试时间：5-10 分钟

### GPU 压力测试

- 执行大规模矩阵乘法运算
- 模拟 GPU 计算负载
- 检测 GPU 稳定性和计算能力
- 建议测试时间：3-5 分钟

## 注意事项

⚠️ **重要提示**：

1. 压力测试会导致硬件高负载运行，请确保散热良好
2. 测试期间电脑可能会出现卡顿，属正常现象
3. 如出现蓝屏、死机、重启等情况，说明硬件存在稳定性问题
4. 建议在关闭其他程序的情况下运行测试
5. 笔记本电脑请连接电源适配器后使用

## 技术栈

- **语言**：C#
- **框架**：.NET 8.0
- **发布方式**：单文件自包含发布（Self-contained Single File）
- **兼容性**：Windows 10/11 x64

## 更新日志

### v1.1.0 (2026-04-27)

- 🐛 修复内存测试超时后不停止的问题
- 🔧 增强 GPU 测试负载（矩阵尺寸从 512 增加到 1024）
- 📝 添加 GPU 测试说明（核显用户建议配合 FurMark 等工具）
- ⚡ 优化内存测试轮次显示

### v1.0.0 (2026-04-27)

- 🎉 首次发布
- ✅ 支持 CPU 压力测试
- ✅ 支持内存压力测试
- ✅ 支持 GPU 压力测试
- ✅ 支持综合测试模式
- ✅ 绿色免安装单文件发布

## 作者

- **相由心生** (XYXS-ZHXD)
- 电脑 DIY 售后服务工具

## 许可证

MIT License

## 反馈与建议

如有问题或建议，请在 [Issues](https://github.com/XYXS-ZHXD/hardware-stress-test/issues) 中反馈。
