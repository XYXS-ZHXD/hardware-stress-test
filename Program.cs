using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareStressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     电脑 DIY 硬件压力测试工具 v1.1 - 绿色版           ║");
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
            Console.WriteLine("  gpu     - GPU 压力测试（矩阵运算压力）");
            Console.WriteLine("  all     - 全部测试（依次执行）");
            Console.WriteLine();
            Console.WriteLine("参数说明:");
            Console.WriteLine("  持续时间秒：测试持续时间，默认 60 秒");
            Console.WriteLine("  线程数：CPU 测试线程数，默认为 CPU 核心数");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  HardwareStressTest.exe cpu 120        # CPU 测试 2 分钟");
            Console.WriteLine("  HardwareStressTest.exe memory 300     # 内存测试 5 分钟");
            Console.WriteLine("  HardwareStressTest.exe gpu 60 8       # GPU 测试 1 分钟");
            Console.WriteLine("  HardwareStressTest.exe all 300        # 全部测试 5 分钟");
        }

        static async Task RunCpuTest(int durationSeconds, int threadCount)
        {
            Console.WriteLine($"\n🔥 开始 CPU 压力测试");
            Console.WriteLine($"   持续时间：{durationSeconds}秒");
            Console.WriteLine($"   线程数：{threadCount}");
            Console.WriteLine();

            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(() => CpuStressWorker(cts.Token)));
            }

            Console.WriteLine("✅ 所有线程已启动，开始压力测试...");
            Console.WriteLine();

            await Task.Delay(durationSeconds * 1000, cts.Token);
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
                // 执行复杂数学计算（浮点运算 + 三角函数）
                for (int i = 0; i < 10000; i++)
                {
                    result += Math.Sin(random.NextDouble()) * Math.Cos(random.NextDouble());
                    result += Math.Sqrt(Math.Abs(result) + 1);
                }
                iterations += 10000;

                // 每 10 万次迭代报告一次
                if (iterations % 100000 == 0)
                {
                    Console.Write(".");
                }
            }

            Console.WriteLine($"\n   线程完成，执行计算次数：{iterations}");
        }

        static async Task RunMemoryTest(int durationSeconds)
        {
            Console.WriteLine($"\n🔥 开始内存压力测试");
            Console.WriteLine($"   持续时间：{durationSeconds}秒");
            Console.WriteLine();

            // 分配约 75% 的可用内存
            long totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            long allocateSize = (long)(totalMemory * 0.75);
            int chunkSize = 1024 * 1024 * 100; // 100MB 每块
            int chunkCount = (int)(allocateSize / chunkSize);

            // 限制最少 1 块，最多 100 块
            chunkCount = Math.Max(1, Math.Min(chunkCount, 100));

            Console.WriteLine($"   系统总内存：{totalMemory / 1024 / 1024 / 1024} GB");
            Console.WriteLine($"   计划分配：{chunkCount} 块 × 100MB = {chunkCount * 100} MB");
            Console.WriteLine();

            var chunks = new List<byte[]>();
            var cts = new CancellationTokenSource();
            int pass = 0;
            bool testCompleted = false;

            try
            {
                var testTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested && !testCompleted)
                    {
                        pass++;
                        Console.WriteLine($"\n   === 第 {pass} 轮内存分配与读写测试 ===");

                        // 分配内存
                        for (int i = 0; i < chunkCount && !cts.Token.IsCancellationRequested; i++)
                        {
                            var chunk = new byte[chunkSize];
                            
                            // 写入数据
                            for (int j = 0; j < chunkSize; j += 4096)
                            {
                                chunk[j] = (byte)(j % 256);
                            }
                            
                            chunks.Add(chunk);
                            Console.Write("A");
                        }

                        if (cts.Token.IsCancellationRequested) break;

                        Console.WriteLine($"\n   已分配 {chunks.Count * 100} MB 内存");

                        // 读取验证
                        Console.WriteLine("   正在读取验证...");
                        long readCount = 0;
                        foreach (var chunk in chunks)
                        {
                            for (int j = 0; j < chunkSize; j += 4096)
                            {
                                byte expected = (byte)(j % 256);
                                if (chunk[j] != expected)
                                {
                                    Console.WriteLine($"\n❌ 内存错误！地址偏移：{j}");
                                }
                                readCount++;
                            }
                            Console.Write("R");
                        }
                        Console.WriteLine($"\n   读取验证 {readCount} 个内存块");

                        // 释放内存
                        chunks.Clear();
                        GC.Collect();
                        Console.WriteLine("   内存已释放，准备下一轮...");
                    }
                    
                    testCompleted = true;
                });

                // 等待指定时间后取消
                await Task.Delay(durationSeconds * 1000);
                cts.Cancel();
                
                // 等待任务完成
                await testTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 内存测试错误：{ex.Message}");
                throw;
            }
            finally
            {
                // 确保释放内存
                if (chunks != null)
                {
                    chunks.Clear();
                }
                GC.Collect();
            }

            Console.WriteLine($"\n✅ 内存压力测试完成！持续时间：{durationSeconds}秒，共 {pass} 轮");
        }

        static async Task RunGpuTest(int durationSeconds)
        {
            Console.WriteLine($"\n🔥 开始 GPU 压力测试");
            Console.WriteLine($"   持续时间：{durationSeconds}秒");
            Console.WriteLine();
            Console.WriteLine("⚠️  说明：本工具使用 CPU 模拟 GPU 矩阵运算负载");
            Console.WriteLine("   对于核显（如 HD630），建议同时运行 GPU 测试 + 实际 3D 应用");
            Console.WriteLine("   如：FurMark、Unigine Heaven 等专用 GPU 压力工具");
            Console.WriteLine();

            var cts = new CancellationTokenSource();
            long iterations = 0;
            bool testCompleted = false;

            var gpuTask = Task.Run(() =>
            {
                var random = new Random();
                const int matrixSize = 1024; // 增大矩阵尺寸到 1024x1024
                
                while (!cts.Token.IsCancellationRequested && !testCompleted)
                {
                    // 大规模矩阵运算（模拟 GPU 计算负载）
                    float[,] matrix1 = new float[matrixSize, matrixSize];
                    float[,] matrix2 = new float[matrixSize, matrixSize];
                    float[,] result = new float[matrixSize, matrixSize];

                    // 初始化矩阵（使用固定值，减少 Random 开销）
                    for (int i = 0; i < matrixSize; i++)
                    {
                        for (int j = 0; j < matrixSize; j++)
                        {
                            matrix1[i, j] = (float)(i * j % 100) / 100.0f;
                            matrix2[i, j] = (float)((i + j) % 100) / 100.0f;
                        }
                    }

                    // 矩阵乘法（密集计算，模拟 GPU 负载）
                    for (int i = 0; i < matrixSize; i++)
                    {
                        for (int j = 0; j < matrixSize; j++)
                        {
                            float sum = 0;
                            for (int k = 0; k < matrixSize; k++)
                            {
                                sum += matrix1[i, k] * matrix2[k, j];
                            }
                            result[i, j] = sum;
                        }
                    }

                    iterations++;
                    Console.Write($"G");
                    
                    // 每 10 次迭代报告一次
                    if (iterations % 10 == 0)
                    {
                        Console.Write($"[{iterations}] ");
                    }
                }
                
                testCompleted = true;
            });

            // 等待指定时间后取消
            await Task.Delay(durationSeconds * 1000);
            cts.Cancel();

            try
            {
                await gpuTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ GPU 测试错误：{ex.Message}");
                throw;
            }

            Console.WriteLine($"\n✅ GPU 压力测试完成！持续时间：{durationSeconds}秒，执行 {iterations} 轮矩阵运算 ({matrixSize}x{matrixSize})");
        }

        static async Task RunAllTests(int durationSeconds, int threadCount)
        {
            int singleTestDuration = durationSeconds / 3;

            Console.WriteLine("\n🔥 开始综合压力测试（CPU + 内存 + GPU）");
            Console.WriteLine($"   总持续时间：{durationSeconds}秒");
            Console.WriteLine($"   每项测试：{singleTestDuration}秒");
            Console.WriteLine();

            await RunCpuTest(singleTestDuration, threadCount);
            await Task.Delay(2000); // 间隔 2 秒

            await RunMemoryTest(singleTestDuration);
            await Task.Delay(2000); // 间隔 2 秒

            await RunGpuTest(singleTestDuration);

            Console.WriteLine("\n✅ 全部压力测试完成！");
        }
    }
}
