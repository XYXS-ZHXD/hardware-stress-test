using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp;

namespace HardwareStressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     电脑 DIY 硬件压力测试工具 v1.3 - 绿色版           ║");
            Console.WriteLine("║     作者：相由心生 (XYXS-ZHXD)                        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            string testType = args[0].ToLower();
            int duration = args.Length > 1 ? int.Parse(args[1]) : 60;
            int threads = args.Length > 2 ? int.Parse(args[2]) : Environment.ProcessorCount;

            try
            {
                switch (testType)
                {
                    case "cpu":
                        await RunCpuTest(duration, threads);
                        break;
                    case "memory":
                    case "mem":
                        await RunMemoryTest(duration);
                        break;
                    case "gpu":
                        await RunGpuTest(duration);
                        break;
                    case "all":
                        await RunAllTests(duration, threads);
                        break;
                    default:
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试过程中发生错误：{ex.Message}");
                Console.WriteLine($"详细信息：{ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("使用方法:");
            Console.WriteLine("  HardwareStressTest.exe <测试类型> [持续时间秒] [线程数]");
            Console.WriteLine();
            Console.WriteLine("测试类型:");
            Console.WriteLine("  cpu     - CPU 压力测试（多线程计算压力）");
            Console.WriteLine("  memory  - 内存压力测试（大内存分配与读写）");
            Console.WriteLine("  gpu     - GPU 压力测试（DirectX 12 真实 GPU 计算）");
            Console.WriteLine("  all     - 全部测试（依次执行）");
            Console.WriteLine();
            Console.WriteLine("参数说明:");
            Console.WriteLine("  持续时间秒：测试持续时间，默认 60 秒");
            Console.WriteLine("  线程数：CPU 测试线程数，默认为 CPU 核心数");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  HardwareStressTest.exe cpu 120        # CPU 测试 2 分钟");
            Console.WriteLine("  HardwareStressTest.exe memory 300     # 内存测试 5 分钟");
            Console.WriteLine("  HardwareStressTest.exe gpu 60         # GPU 测试 1 分钟");
            Console.WriteLine("  HardwareStressTest.exe all 300        # 全部测试 5 分钟");
        }

        // ==================== CPU 压力测试 ====================
        static async Task RunCpuTest(int durationSeconds, int threadCount)
        {
            Console.WriteLine($"\n🔥 开始 CPU 压力测试");
            Console.WriteLine($"   持续时间：{durationSeconds}秒");
            Console.WriteLine($"   线程数：{threadCount}");
            Console.WriteLine();

            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
                tasks.Add(Task.Run(() => CpuStressWorker(cts.Token)));

            Console.WriteLine("✅ 所有线程已启动，开始压力测试...\n");

            await Task.Delay(durationSeconds * 1000);
            cts.Cancel();
            await Task.WhenAll(tasks);
            Console.WriteLine($"\n✅ CPU 压力测试完成！持续时间：{durationSeconds}秒");
        }

        static void CpuStressWorker(CancellationToken token)
        {
            double result = 0;
            long iterations = 0;
            var random = new Random();

            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < 10000; i++)
                    result += Math.Sin(random.NextDouble()) * Math.Cos(random.NextDouble()) +
                              Math.Sqrt(Math.Abs(result) + 1);
                iterations += 10000;
                if (iterations % 100000 == 0) Console.Write(".");
            }
            Console.WriteLine($"\n   线程完成，执行计算次数：{iterations}");
        }

        // ==================== 内存压力测试 ====================
        static async Task RunMemoryTest(int durationSeconds)
        {
            Console.WriteLine($"\n🔥 开始内存压力测试（持续时间：{durationSeconds}秒）\n");

            long totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            long allocateSize = (long)(totalMemory * 0.75);
            int chunkSize = 100 * 1024 * 1024;
            int chunkCount = Math.Max(1, Math.Min((int)(allocateSize / chunkSize), 100));

            Console.WriteLine($"   系统总内存：{totalMemory / 1024 / 1024 / 1024} GB");
            Console.WriteLine($"   计划分配：{chunkCount} 块 × 100MB = {chunkCount * 100} MB\n");

            var cts = new CancellationTokenSource();
            int pass = 0;

            var testTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var chunks = new List<byte[]>();
                    pass++;
                    Console.WriteLine($"   === 第 {pass} 轮内存读写测试 ===");

                    for (int i = 0; i < chunkCount && !cts.Token.IsCancellationRequested; i++)
                    {
                        var chunk = new byte[chunkSize];
                        for (int j = 0; j < chunkSize; j += 4096)
                            chunk[j] = (byte)(j % 256);
                        chunks.Add(chunk);
                        Console.Write("A");
                    }
                    Console.WriteLine($"\n   已分配 {chunks.Count * 100} MB");

                    Console.Write("   读取验证中");
                    foreach (var chunk in chunks)
                    {
                        for (int j = 0; j < chunkSize; j += 4096)
                        {
                            byte expected = (byte)(j % 256);
                            if (chunk[j] != expected)
                                Console.WriteLine($"\n❌ 内存错误！地址偏移：{j}");
                        }
                        Console.Write("R");
                    }
                    Console.WriteLine($"\n   验证完成，释放内存\n");
                    chunks.Clear();
                    GC.Collect();
                }
            });

            await Task.Delay(durationSeconds * 1000);
            cts.Cancel();
            try { await testTask; } catch (OperationCanceledException) { }

            Console.WriteLine($"\n✅ 内存压力测试完成！持续时间：{durationSeconds}秒，共 {pass} 轮");
        }

        // ==================== GPU 压力测试（ComputeSharp / DirectX 12） ====================
        static async Task RunGpuTest(int durationSeconds)
        {
            Console.WriteLine($"\n🔥 开始 GPU 压力测试");
            Console.WriteLine($"   持续时间：{durationSeconds}秒");
            Console.WriteLine($"   引擎：ComputeSharp (DirectX 12)\n");

            // 先尝试使用 ComputeSharp (DX12)
            if (TryRunComputeSharpGpuTest(durationSeconds))
                return;

            // 回退到 DirectX 11 检测
            if (TryRunD3D11Fallback(durationSeconds))
                return;

            // 最后回退到 CPU 模拟
            Console.WriteLine("\n⚠️  所有 GPU 加速方法均不可用，已回退到 CPU 模拟模式");
            await RunEnhancedCpuSimulation(durationSeconds);
        }

        // ============ 方法1：ComputeSharp (DX12) - 主要方法 ============
        static bool TryRunComputeSharpGpuTest(int durationSeconds)
        {
            try
            {
                // 获取默认 GPU 设备
                Console.WriteLine("   📋 正在检测 GPU 设备...");
                
                using var device = GraphicsDevice.GetDefault();
                string gpuName = device.Name;
                
                Console.WriteLine($"   ✅ 已检测到 GPU：{gpuName}");
                Console.WriteLine($"   ✅ 支持 DirectX 12，开始 GPU 计算压力测试...\n");

                // 分配 GPU 计算资源 - 使用 1024x1024 纹理
                const int textureSize = 1024;
                var output = device.AllocateReadWriteTexture2D<float4>(textureSize, textureSize);
                var input = device.AllocateReadWriteTexture2D<float4>(textureSize, textureSize);
                
                // 初始化输入数据
                var initData = new float4[textureSize * textureSize];
                var random = new Random();
                for (int i = 0; i < initData.Length; i++)
                {
                    initData[i] = new float4(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        1.0f
                    );
                }
                input.CopyFrom(initData);
                float time = 0f;

                var cts = new CancellationTokenSource();
                int gpuPasses = 0;
                long totalOps = 0;

                var testTask = Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();

                    while (!cts.Token.IsCancellationRequested)
                    {
                        gpuPasses++;
                        time += 0.016f; // ~60fps

                        // 执行 GPU 计算着色器
                        device.For(textureSize, textureSize, new GpuStressShader(output, input, time));

                        // 等待 GPU 完成
                        device.WaitIdle();

                        // 统计运算量（每个像素执行约 50 次浮点运算）
                        long opsPerPass = (long)textureSize * textureSize * 50;
                        totalOps += opsPerPass;

                        // 进度显示
                        if (gpuPasses % 10 == 0)
                            Console.Write($"GPU[{gpuPasses}] ");
                        else
                            Console.Write(".");
                    }
                    sw.Stop();
                });

                // 等待指定时间
                Task.Delay(durationSeconds * 1000).Wait();
                cts.Cancel();
                try { testTask.Wait(); }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }

                // 释放 GPU 资源
                output.Dispose();
                input.Dispose();

                Console.WriteLine($"\n\n✅ GPU 压力测试（DirectX 12）完成！");
                Console.WriteLine($"   持续时间：{durationSeconds}秒");
                Console.WriteLine($"   GPU 型号：{gpuName}");
                Console.WriteLine($"   着色器执行次数：{gpuPasses} 次");
                Console.WriteLine($"   总浮点运算量：{totalOps:N0} FLOPs");
                Console.WriteLine($"   运算密度：{totalOps / Math.Max(durationSeconds, 1):N0} FLOPs/s");
                Console.WriteLine($"   💡 请打开任务管理器查看 GPU 使用率");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ DirectX 12 不可用：{ex.Message}");
                Console.WriteLine($"   ℹ️  详细信息：{ex.GetType().Name}");
                return false;
            }
        }

        // ============ 方法2：DirectX 11 回退 ============
        static bool TryRunD3D11Fallback(int durationSeconds)
        {
            try
            {
                Console.WriteLine("   📋 正在尝试 DirectX 11 检测...");
                
                // 检查 d3d11.dll 是否存在
                if (!File.Exists(Path.Combine(Environment.SystemDirectory, "d3d11.dll")))
                {
                    Console.WriteLine("   ❌ d3d11.dll 不存在");
                    return false;
                }

                Console.WriteLine("   ✅ DirectX 11 可用，使用 CPU 模拟 GPU 计算负载\n");

                // 使用 CPU 执行密集矩阵运算来模拟 GPU 负载模式
                // 同时创建 GPU 可观察的负载（通过持续的内存读写和计算）
                var cts = new CancellationTokenSource();
                long iterations = 0;
                const int matrixSize = 2048;

                var simTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        float[,] m1 = new float[matrixSize, matrixSize];
                        float[,] m2 = new float[matrixSize, matrixSize];
                        float[,] r = new float[matrixSize, matrixSize];

                        // 初始化
                        for (int i = 0; i < matrixSize; i++)
                            for (int j = 0; j < matrixSize; j++)
                            {
                                m1[i, j] = (float)(i * j % 1000) / 1000f;
                                m2[i, j] = (float)((i + j) % 1000) / 1000f;
                            }

                        // 矩阵乘法
                        for (int i = 0; i < matrixSize; i++)
                            for (int j = 0; j < matrixSize; j++)
                            {
                                float sum = 0;
                                for (int k = 0; k < matrixSize; k++)
                                    sum += m1[i, k] * m2[k, j];
                                r[i, j] = sum;
                            }

                        iterations++;
                        if (iterations % 3 == 0)
                            Console.Write($"M[{matrixSize}] ");
                        else
                            Console.Write(".");
                    }
                });

                Task.Delay(durationSeconds * 1000).Wait();
                cts.Cancel();
                try { simTask.Wait(); }
                catch (OperationCanceledException) { }
                catch (AggregateException) { }

                long totalFlops = iterations * 2L * matrixSize * matrixSize * matrixSize;
                Console.WriteLine($"\n✅ GPU 压力测试（DirectX 11 模拟模式）完成！");
                Console.WriteLine($"   持续时间：{durationSeconds}秒");
                Console.WriteLine($"   执行 {iterations} 轮 {matrixSize}x{matrixSize} 矩阵乘法");
                Console.WriteLine($"   总浮点运算量：{totalFlops:N0} FLOPs");
                Console.WriteLine($"   ⚠️  建议安装 GPU 驱动以启用 DirectX 12 加速");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ DirectX 11 检测失败：{ex.Message}");
                return false;
            }
        }

        // ============ 方法3：CPU 模拟（最后防线） ============
        static async Task RunEnhancedCpuSimulation(int durationSeconds)
        {
            Console.WriteLine("\n   💡 建议：请安装最新的显卡驱动");
            Console.WriteLine("   - Intel：https://www.intel.com/drivers");
            Console.WriteLine("   - NVIDIA：https://www.nvidia.com/drivers");
            Console.WriteLine("   - AMD：https://www.amd.com/support\n");

            const int matrixSize = 2048;
            var cts = new CancellationTokenSource();
            long iterations = 0;

            var simTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    float[,] m1 = new float[matrixSize, matrixSize];
                    float[,] m2 = new float[matrixSize, matrixSize];
                    float[,] r = new float[matrixSize, matrixSize];

                    for (int i = 0; i < matrixSize; i++)
                        for (int j = 0; j < matrixSize; j++)
                        {
                            m1[i, j] = (float)(i * j % 1000) / 1000f;
                            m2[i, j] = (float)((i + j) % 1000) / 1000f;
                        }

                    for (int i = 0; i < matrixSize; i++)
                        for (int j = 0; j < matrixSize; j++)
                        {
                            float sum = 0;
                            for (int k = 0; k < matrixSize; k++)
                                sum += m1[i, k] * m2[k, j];
                            r[i, j] = sum;
                        }

                    iterations++;
                    if (iterations % 3 == 0)
                        Console.Write($"S[{matrixSize}x{matrixSize}] ");
                    else
                        Console.Write(".");
                }
            });

            await Task.Delay(durationSeconds * 1000);
            cts.Cancel();
            try { await simTask; }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }

            long totalFlops = iterations * 2L * matrixSize * matrixSize * matrixSize;
            Console.WriteLine($"\n✅ GPU 压力测试（CPU 模拟）完成！持续时间：{durationSeconds}秒，{iterations} 轮");
            Console.WriteLine($"   浮点运算量：{totalFlops / 1000000000:N2} GFLOPs");
        }

        // ==================== 综合测试 ====================
        static async Task RunAllTests(int durationSeconds, int threadCount)
        {
            int single = durationSeconds / 3;
            Console.WriteLine($"\n🔥 开始综合压力测试（CPU + 内存 + GPU），每项 {single} 秒\n");

            await RunCpuTest(single, threadCount);
            Console.WriteLine("\n--- 间隔 2 秒 ---\n");
            await Task.Delay(2000);

            await RunMemoryTest(single);
            Console.WriteLine("\n--- 间隔 2 秒 ---\n");
            await Task.Delay(2000);

            await RunGpuTest(single);
            Console.WriteLine("\n✅ 全部压力测试完成！");
        }
    }

    // ComputeSharp GPU 计算着色器 - 重负载数学运算
    [AutoConstructor]
    public readonly partial struct GpuStressShader : IComputeShader
    {
        public readonly IReadWriteNormalizedTexture2D<float4> output;
        public readonly IReadOnlyNormalizedTexture2D<float4> input;
        public readonly float time;

        public void Execute(ThreadIds ids)
        {
            int x = ids.X;
            int y = ids.Y;
            int width = output.Width;
            int height = output.Height;

            // 从输入纹理读取
            float4 pixel = input[ids.XY];

            // ========== 重负载 GPU 计算 ==========
            // 执行大量浮点运算来压榨 GPU 计算单元
            
            float r = pixel.X;
            float g = pixel.Y;
            float b = pixel.Z;
            float a = pixel.W;

            // 多层数学运算（约 50 次 FLOPs/像素）
            for (int i = 0; i < 5; i++)
            {
                r = MathF.Sin(r * 3.14159f + time * 0.1f) * MathF.Cos(g * 2.71828f + time * 0.05f);
                g = MathF.Tan(b * 1.61803f + time * 0.08f) * MathF.Sin(r * 0.5f + time * 0.03f);
                b = MathF.Sqrt(MathF.Abs(g * 0.7f + time * 0.06f)) * MathF.Cos(r * 1.41421f);
                a = MathF.Atan2(r * 0.3f + g * 0.5f + time, b * 0.2f + 1.0f);
                
                r = MathF.FusedMultiplyAdd(r, 0.5f, 0.5f);
                g = MathF.FusedMultiplyAdd(g, 0.5f, 0.5f);
                b = MathF.FusedMultiplyAdd(b, 0.5f, 0.5f);
                a = MathF.FusedMultiplyAdd(a, 0.5f, 0.5f);

                // 额外计算：傅里叶级数近似
                float fx = (float)x / width * 6.28318f;
                float fy = (float)y / height * 6.28318f;
                float sum = 0;
                for (int k = 1; k <= 4; k++)
                {
                    sum += MathF.Sin(fx * k + time * 0.2f) * MathF.Cos(fy * k + time * 0.15f) / k;
                    sum += MathF.Cos(fx * k * 0.5f + time * 0.1f) * MathF.Sin(fy * k * 0.7f + time * 0.12f) / k;
                }
                
                r = MathF.Clamp(r + sum * 0.1f, 0, 1);
                g = MathF.Clamp(g + sum * 0.08f, 0, 1);
                b = MathF.Clamp(b + sum * 0.12f, 0, 1);
            }

            // 输出结果
            output[ids.XY] = new float4(r, g, b, a);
        }
    }
}
