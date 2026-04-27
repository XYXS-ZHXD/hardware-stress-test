using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace HardwareStressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     电脑 DIY 硬件压力测试工具 v1.3 - 绿色版           ║");
            Console.WriteLine("║     引擎：DirectX 11 Compute Shader                   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            if (args.Length == 0) { ShowHelp(); return; }

            string type = args[0].ToLower();
            int dur = args.Length > 1 ? int.Parse(args[1]) : 60;
            int threads = args.Length > 2 ? int.Parse(args[2]) : Environment.ProcessorCount;

            try
            {
                switch (type)
                {
                    case "cpu": await RunCpu(dur, threads); break;
                    case "mem": case "memory": await RunMem(dur); break;
                    case "gpu": await RunGpu(dur); break;
                    case "all": await RunAll(dur, threads); break;
                    default: ShowHelp(); break;
                }
            }
            catch (Exception ex) { Console.WriteLine($"\n❌ 错误：{ex.Message}"); Environment.Exit(1); }
        }

        static void ShowHelp()
        {
            Console.WriteLine("用法: HardwareStressTest.exe <类型> [秒数] [线程数]");
            Console.WriteLine("  cpu     - CPU 压力测试");
            Console.WriteLine("  memory  - 内存压力测试");
            Console.WriteLine("  gpu     - GPU 压力测试 (DirectX 11)");
            Console.WriteLine("  all     - 全部测试");
            Console.WriteLine("示例:  HardwareStressTest.exe gpu 120");
        }

        // ===== CPU =====
        static async Task RunCpu(int sec, int thr)
        {
            Console.WriteLine($"\n🔥 CPU 压力测试 {sec}秒 {thr}线程\n");
            var cts = new CancellationTokenSource();
            var tasks = new Task[thr];
            for (int i = 0; i < thr; i++)
                tasks[i] = Task.Run(() => { double r = 0; var rnd = new Random();
                    while (!cts.Token.IsCancellationRequested)
                        for (int j = 0; j < 100000; j++)
                            r += Math.Sin(rnd.NextDouble()) * Math.Cos(rnd.NextDouble());
                        if (r < 0) Console.Write("."); });
            await Task.Delay(sec * 1000); cts.Cancel();
            await Task.WhenAll(tasks);
            Console.WriteLine("\n✅ CPU 测试完成");
        }

        // ===== 内存 =====
        static async Task RunMem(int sec)
        {
            Console.WriteLine($"\n🔥 内存压力测试 {sec}秒\n");
            long mem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            int n = Math.Max(1, Math.Min((int)(mem * 0.75 / (100L << 20)), 50));
            Console.WriteLine($"   总内存 {mem >> 30} GB, 分配 {n}×100MB\n");
            var cts = new CancellationTokenSource();
            int pass = 0;
            var task = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    pass++; Console.Write($"   第{pass}轮...");
                    var list = new List<byte[]>();
                    for (int i = 0; i < n && !cts.Token.IsCancellationRequested; i++)
                    { var b = new byte[100L << 20]; for (int j = 0; j < b.Length; j += 4096) b[j] = (byte)j; list.Add(b); }
                    foreach (var b in list) for (int j = 0; j < b.Length; j += 4096) _ = b[j];
                    list.Clear(); GC.Collect(); Console.Write("释放\n"); }
            });
            await Task.Delay(sec * 1000); cts.Cancel();
            try { await task; } catch { }
            Console.WriteLine($"\n✅ 内存测试完成！{pass} 轮");
        }

        // ===== GPU (DirectX 11 Compute Shader) =====
        static async Task RunGpu(int sec)
        {
            Console.WriteLine($"\n🔥 GPU 压力测试 {sec}秒\n");

            // 尝试 DirectX 11
            if (RunD3D11Compute(sec))
                return;

            Console.WriteLine("❌ DirectX 11 不可用，回退到 CPU 模拟\n");
            await CpuSim(sec);
        }

        static bool RunD3D11Compute(int sec)
        {
            try
            {
                // 1. 创建 D3D11 设备
                Console.WriteLine("   📋 正在初始化 DirectX 11...");
                
                var (device, context) = CreateD3D11Device();
                if (device == null)
                {
                    Console.WriteLine("   ❌ 无法创建 D3D11 设备，可能缺少显卡驱动");
                    return false;
                }

                string gpuName = GetAdapterName(device);
                Console.WriteLine($"   ✅ D3D11 设备已创建");
                Console.WriteLine($"   🖥️  GPU: {gpuName}");
                Console.WriteLine();

                // 2. HLSL 计算着色器源码
                string hlsl = @"
RWStructuredBuffer<float> A : register(u0);
RWStructuredBuffer<float> B : register(u1);
RWStructuredBuffer<float> C : register(u2);
RWStructuredBuffer<float> D : register(u3);

[numthreads(128, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    uint i = id.x;
    float sum = 0;
    // 256 次浮点运算
    [unroll]
    for (int k = 0; k < 256; k++) {
        float a = A[i * 256 + k];
        float b = B[k * 256 + (i & 255)];
        sum += sin(a) * cos(b) + sqrt(abs(a * b + 0.001f));
        sum += tan(a * 0.5f + b * 0.3f) * sin(b * 0.7f + a * 0.5f);
    }
    C[i] = sum;
    D[i] = frac(sum * 3.14159f);
}";
                // 3. 编译着色器
                Console.WriteLine("   🔧 正在编译计算着色器...");
                CompilerResult result = Compiler.Compile(hlsl, "CSMain", "cs_5_0");
                if (result.HasErrors)
                {
                    Console.WriteLine($"   ❌ 着色器编译失败: {result.Message}");
                    device.Dispose(); context.Dispose();
                    return false;
                }
                Console.WriteLine("   ✅ 计算着色器编译成功！\n");

                // 4. 创建计算着色器
                ID3D11ComputeShader shader = device.CreateComputeShader(result.Buffer);
                result.Dispose();

                // 5. 创建 GPU 缓冲区 (256K 个 float)
                int bufSize = 256 * 1024;
                int stride = sizeof(float);
                int byteSize = bufSize * stride;

                float[] dataA = new float[bufSize];
                float[] dataB = new float[bufSize];
                var rnd = new Random(42);
                for (int i = 0; i < bufSize; i++) { dataA[i] = (float)rnd.NextDouble(); dataB[i] = (float)rnd.NextDouble(); }

                ID3D11Buffer bufferA = CreateBuffer(device, dataA, byteSize);
                ID3D11Buffer bufferB = CreateBuffer(device, dataB, byteSize);
                ID3D11Buffer bufferC = CreateBuffer(device, new float[bufSize], byteSize);
                ID3D11Buffer bufferD = CreateBuffer(device, new float[bufSize], byteSize);

                // 设置 UAV
                ID3D11UnorderedAccessView uavA = device.CreateUnorderedAccessView(bufferA);
                ID3D11UnorderedAccessView uavB = device.CreateUnorderedAccessView(bufferB);
                ID3D11UnorderedAccessView uavC = device.CreateUnorderedAccessView(bufferC);
                ID3D11UnorderedAccessView uavD = device.CreateUnorderedAccessView(bufferD);

                context.CSSetShader(shader);
                context.CSSetUnorderedAccessViews(0, uavA, uavB, uavC, uavD);

                Console.WriteLine("   🚀 开始 GPU 计算压力测试...");
                Console.WriteLine("   (任务管理器 → 性能 → GPU 应显示使用率上升)\n");

                // 6. 计算循环
                var cts = new CancellationTokenSource();
                long dispatchCount = 0;

                var gpuTask = Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        dispatchCount++;

                        // 每次 Dispatch 启动 2048 个线程组 × 128 线程 = 262,144 线程
                        // 每个线程执行 ~512 次浮点运算
                        context.Dispatch(2048, 1, 1);

                        // 等待 GPU 完成
                        context.Flush();

                        if (dispatchCount % 10 == 0)
                            Console.Write($"D[{dispatchCount}] ");
                        else
                            Console.Write(".");
                    }
                    sw.Stop();
                });

                // 等待指定时间
                Task.Delay(sec * 1000).Wait();
                cts.Cancel();
                try { gpuTask.Wait(); } catch { }

                // 7. 统计并清理
                long totalOps = dispatchCount * 2048 * 128 * 512;
                shader.Dispose();
                uavA.Dispose(); uavB.Dispose(); uavC.Dispose(); uavD.Dispose();
                bufferA.Dispose(); bufferB.Dispose(); bufferC.Dispose(); bufferD.Dispose();
                context.Dispose(); device.Dispose();

                Console.WriteLine($"\n\n✅ GPU 压力测试 (DirectX 11) 完成！");
                Console.WriteLine($"   持续时间：{sec}秒");
                Console.WriteLine($"   GPU: {gpuName}");
                Console.WriteLine($"   Dispatch 次数：{dispatchCount}");
                Console.WriteLine($"   总 GPU 浮点运算：约 {totalOps:N0} FLOPs");
                Console.WriteLine($"   💡 请打开任务管理器查看 GPU 使用率");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ GPU 测试失败：{ex.Message}");
                return false;
            }
        }

        // 创建 D3D11 设备
        static (ID3D11Device?, ID3D11DeviceContext?) CreateD3D11Device()
        {
            try
            {
                var result = D3D11.D3D11CreateDevice(
                    null, DriverType.Hardware, DeviceCreationFlags.None,
                    new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 },
                    out ID3D11Device? device);
                
                if (result.Failure || device == null)
                    return (null, null);

                var context = device.ImmediateContext;
                return (device, context);
            }
            catch
            {
                return (null, null);
            }
        }

        // 获取 GPU 名称
        static string GetAdapterName(ID3D11Device device)
        {
            try
            {
                using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
                using var adapter = dxgiDevice.GetAdapter();
                return adapter.Description.Description;
            }
            catch { return "未知"; }
        }

        // 创建 GPU StructuredBuffer
        static ID3D11Buffer CreateBuffer(ID3D11Device device, float[] data, int byteSize)
        {
            return device.CreateBuffer(new BufferDescription(byteSize, BindFlags.UnorderedAccess,
                ResourceUsage.Default, CpuAccessFlags.None, ResourceOptionFlags.BufferStructured,
                sizeof(float)), new SubresourceData(data));
        }

        // CPU 模拟回退
        static async Task CpuSim(int sec)
        {
            const int s = 2048;
            var cts = new CancellationTokenSource();
            long it = 0;
            var t = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested)
                {
                    var m1 = new float[s, s]; var m2 = new float[s, s]; var r = new float[s, s];
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { m1[i, j] = (float)(i * j % 1000) / 1000f; m2[i, j] = (float)((i + j) % 1000) / 1000f; }
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { float sum = 0; for (int k = 0; k < s; k++) sum += m1[i, k] * m2[k, j]; r[i, j] = sum; }
                    it++; if (it % 3 == 0) Console.Write($"S[{s}] "); else Console.Write(".");
                }
            });
            await Task.Delay(sec * 1000); cts.Cancel();
            try { await t; } catch { }
            Console.WriteLine($"\n✅ CPU 模拟完成！{it} 轮 {s}x{s}");
        }

        // ===== 综合 =====
        static async Task RunAll(int sec, int thr)
        {
            int t = sec / Math.Max(sec / 3, 1);
            Console.WriteLine($"\n🔥 综合测试，每项 {t} 秒\n");
            await RunCpu(t, thr); Console.WriteLine(); await Task.Delay(2000);
            await RunMem(t); Console.WriteLine(); await Task.Delay(2000);
            await RunGpu(t); Console.WriteLine("\n✅ 全部完成！");
        }
    }
}
