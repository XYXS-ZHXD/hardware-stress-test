using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;

namespace HardwareStressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  硬件压力测试 v1.3 - DirectX 11 Compute Shader       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            if (args.Length == 0) { ShowHelp(); return; }
            string type = args[0].ToLower();
            int dur = args.Length > 1 ? int.Parse(args[1]) : 60;
            int thr = args.Length > 2 ? int.Parse(args[2]) : Environment.ProcessorCount;

            try
            {
                switch (type)
                {
                    case "cpu": await RunCpu(dur, thr); break;
                    case "mem": case "memory": await RunMem(dur); break;
                    case "gpu": await RunGpu(dur); break;
                    case "all": await RunAll(dur, thr); break;
                    default: ShowHelp(); break;
                }
            }
            catch (Exception ex) { Console.WriteLine($"\n❌ 错误：{ex.Message}"); }
        }

        static void ShowHelp()
        {
            Console.WriteLine("用法: HardwareStressTest.exe <类型> [秒数] [线程数]");
            Console.WriteLine("  cpu     - CPU 压力测试");
            Console.WriteLine("  memory  - 内存压力测试");
            Console.WriteLine("  gpu     - GPU 压力测试 (DirectX 11 Compute)");
            Console.WriteLine("  all     - 全部测试");
        }

        static async Task RunCpu(int sec, int thr)
        {
            Console.WriteLine($"\n🔥 CPU 压力测试 {sec}秒 {thr}线程\n");
            var cts = new CancellationTokenSource();
            var tasks = new Task[thr];
            for (int i = 0; i < thr; i++)
                tasks[i] = Task.Run(() => { double r = 0; var rnd = new Random();
                    while (!cts.Token.IsCancellationRequested)
                        for (int j = 0; j < 100000; j++) r += Math.Sin(rnd.NextDouble()) * Math.Cos(rnd.NextDouble()); });
            await Task.Delay(sec * 1000); cts.Cancel();
            await Task.WhenAll(tasks);
            Console.WriteLine($"\n✅ CPU 完成");
        }

        static async Task RunMem(int sec)
        {
            Console.WriteLine($"\n🔥 内存压力测试 {sec}秒\n");
            long mem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            int n = Math.Max(1, Math.Min((int)(mem * 0.75 / (100L << 20)), 50));
            Console.WriteLine($"   内存 {mem>>30}GB, 分配 {n}×100MB\n");
            var cts = new CancellationTokenSource();
            int pass = 0;
            var task = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    pass++; Console.Write($"   第{pass}轮...");
                    var list = new List<byte[]>();
                    for (int i = 0; i < n && !cts.Token.IsCancellationRequested; i++) {
                        var b = new byte[100L << 20]; for (int j = 0; j < b.Length; j += 4096) b[j] = (byte)j; list.Add(b); }
                    foreach (var b in list) for (int j = 0; j < b.Length; j += 4096) _ = b[j];
                    list.Clear(); GC.Collect(); Console.Write("释放\n"); }
            });
            await Task.Delay(sec * 1000); cts.Cancel();
            try { await task; } catch { }
            Console.WriteLine($"\n✅ 内存完成！{pass} 轮");
        }

        // ============ GPU DirectX 11 Compute ============
        static async Task RunGpu(int sec)
        {
            Console.WriteLine($"\n🔥 GPU 压力测试 {sec}秒\n");
            Console.WriteLine("   📋 初始化 DirectX 11...");

            // 创建 D3D11 设备
            var result = D3D11.D3D11CreateDevice(
                null, DriverType.Hardware, DeviceCreationFlags.None,
                new[] { FeatureLevel.Level_11_0 }, out ID3D11Device? device);

            if (result.Failure || device == null)
            {
                Console.WriteLine("   ❌ 无法创建 D3D11 设备（请安装显卡驱动）");
                Console.WriteLine("   回退到 CPU 模拟...\n");
                await CpuSim(sec);
                return;
            }

            var ctx = device.ImmediateContext;
            string gpuName = GetGpuName(device);
            Console.WriteLine($"   🖥️  GPU: {gpuName}\n");

            // HLSL 计算着色器 - 矩阵运算
            string hlsl = @"
RWStructuredBuffer<float> A : register(u0);
RWStructuredBuffer<float> B : register(u1);
RWStructuredBuffer<float> C : register(u2);
[numthreads(64, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    uint idx = id.x;
    float sum = 0;
    for (int k = 0; k < 128; k++) {
        float a = A[idx * 128 + k];
        float b = B[k * 128 + (idx & 127)];
        sum += sin(a) * cos(b) + sqrt(abs(a * b + 0.001f));
    }
    C[idx] = sum;
}";
            Console.WriteLine("   🔧 编译计算着色器...");

            // 编译 - 使用 Vortice 标准 API
            try
            {
                var compResult = Compiler.Compile(hlsl, "CSMain", "cs_5_0");
                if (compResult == null || compResult.HasErrors)
                {
                    string msg = compResult?.Message ?? "未知错误";
                    Console.WriteLine($"   ❌ 编译失败: {msg}");
                    compResult?.Dispose();
                    ctx.Dispose(); device.Dispose();
                    Console.WriteLine("   回退到 CPU 模拟...\n");
                    await CpuSim(sec);
                    return;
                }

                byte[] shaderBytes = compResult.GetBytes();
                var shader = device.CreateComputeShader(shaderBytes);
                compResult.Dispose();
                Console.WriteLine("   ✅ 着色器编译成功\n");

                // 创建 GPU 缓冲区
                int totalElems = 128 * 512;
                int byteSize = totalElems * 4;

                float[] dataA = new float[totalElems];
                float[] dataB = new float[totalElems];
                float[] dataC = new float[totalElems];
                var rnd = new Random(42);
                for (int i = 0; i < totalElems; i++) { dataA[i] = (float)rnd.NextDouble(); dataB[i] = (float)rnd.NextDouble(); }

                var bufA = MakeBuf(device, dataA, byteSize);
                var bufB = MakeBuf(device, dataB, byteSize);
                var bufC = MakeBuf(device, dataC, byteSize);

                var uavA = device.CreateUnorderedAccessView(bufA);
                var uavB = device.CreateUnorderedAccessView(bufB);
                var uavC = device.CreateUnorderedAccessView(bufC);

                ctx.CSSetShader(shader);
                ctx.CSSetUnorderedAccessViews(0, new ID3D11UnorderedAccessView[] { uavA, uavB, uavC });

                Console.WriteLine("   🚀 GPU 计算压力测试中...\n");
                Console.WriteLine("   (任务管理器 → 性能 → GPU 查看使用率)\n");

                var cts = new CancellationTokenSource();
                long dispatches = 0;

                var gpuTask = Task.Run(() => {
                    while (!cts.Token.IsCancellationRequested) {
                        dispatches++;
                        ctx.Dispatch(1024, 1, 1);
                        ctx.Flush();
                        if (dispatches % 10 == 0) Console.Write($"D[{dispatches}] ");
                        else Console.Write(".");
                    }
                });

                await Task.Delay(sec * 1000);
                cts.Cancel();
                try { await gpuTask; } catch { }

                shader.Dispose();
                uavA.Dispose(); uavB.Dispose(); uavC.Dispose();
                bufA.Dispose(); bufB.Dispose(); bufC.Dispose();
                ctx.Dispose(); device.Dispose();

                long totalOps = dispatches * 1024L * 64L * 128L * 4L;
                Console.WriteLine($"\n\n✅ GPU (DirectX 11 Compute) 完成！");
                Console.WriteLine($"   GPU: {gpuName}");
                Console.WriteLine($"   Dispatch: {dispatches} 次");
                Console.WriteLine($"   运算量: ~{totalOps:N0} FLOPs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ 编译错误: {ex.Message}");
                Console.WriteLine("   回退到 CPU 模拟...\n");
                ctx.Dispose(); device.Dispose();
                await CpuSim(sec);
            }
        }

        static ID3D11Buffer MakeBuf(ID3D11Device device, float[] data, int byteSize)
        {
            var desc = new BufferDescription((uint)byteSize, BindFlags.UnorderedAccess,
                ResourceUsage.Default, CpuAccessFlags.None,
                ResourceOptionFlags.BufferStructured, 4);

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try {
                var subData = new SubresourceData(handle.AddrOfPinnedObject(), (uint)byteSize, 0);
                return device.CreateBuffer(desc, subData);
            }
            finally { handle.Free(); }
        }

        static string GetGpuName(ID3D11Device device)
        {
            try {
                using var dxgiDev = device.QueryInterface<IDXGIDevice>();
                using var adapter = dxgiDev.GetAdapter();
                return adapter.Description.Description;
            }
            catch { return "Unknown"; }
        }

        // ============ CPU 模拟 ============
        static async Task CpuSim(int sec)
        {
            const int s = 2048;
            var cts = new CancellationTokenSource();
            long it = 0;
            var t = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    float[,] m1 = new float[s, s], m2 = new float[s, s], r = new float[s, s];
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { m1[i,j]=(float)(i*j%1000)/1000f; m2[i,j]=(float)((i+j)%1000)/1000f; }
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { float sum=0; for(int k=0;k<s;k++) sum+=m1[i,k]*m2[k,j]; r[i,j]=sum; }
                    it++; if (it % 3 == 0) Console.Write($"S[{s}] "); else Console.Write(".");
                }
            });
            await Task.Delay(sec * 1000); cts.Cancel();
            try { await t; } catch { }
            Console.WriteLine($"\n✅ CPU 模拟完成！{it} 轮 {s}x{s}");
        }

        static async Task RunAll(int sec, int thr)
        {
            int t = Math.Max(sec / 3, 10);
            Console.WriteLine($"\n🔥 综合测试，每项 {t} 秒\n");
            await RunCpu(t, thr); Console.WriteLine(); await Task.Delay(2000);
            await RunMem(t); Console.WriteLine(); await Task.Delay(2000);
            await RunGpu(t); Console.WriteLine("\n✅ 全部完成！");
        }
    }
}
