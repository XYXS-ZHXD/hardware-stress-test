using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace HardwareStressTest
{
    class Program
    {
        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, EntryPoint = "D3DCompile")]
        static extern int D3DCompile(byte[] srcData, IntPtr srcDataSize, string? sourceName,
            IntPtr defines, IntPtr include, string entryPoint, string target,
            uint flags1, uint flags2, out IntPtr code, out IntPtr errorMsgs);

        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  硬件压力测试 v1.4 - D3D11 + FurMark CLI 集成       ║");
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
                    case "furmark": await RunFurMark(dur); break;
                    case "gpu-full": await RunGpuFull(dur, thr); break;
                    case "all": await RunAll(dur, thr); break;
                    default: ShowHelp(); break;
                }
            }
            catch (Exception ex) { Console.WriteLine($"\n❌ 错误：{ex.Message}"); }
        }

        static void ShowHelp()
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  用法: HardwareStressTest.exe <类型> [秒数] [线程数]     ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  cpu       CPU 压力测试                                  ║");
            Console.WriteLine("║  memory    内存压力测试                                  ║");
            Console.WriteLine("║  gpu       GPU 压力测试 (D3D11 Compute Shader)           ║");
            Console.WriteLine("║  furmark   调用 FurMark CLI 测 GPU 渲染                   ║");
            Console.WriteLine("║  gpu-full  同时跑 D3D11 Compute + FurMark CLI             ║");
            Console.WriteLine("║  all       CPU + 内存 + D3D11 GPU                         ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  示例:                                                    ║");
            Console.WriteLine("║  HardwareStressTest.exe gpu 120     GPU 测试 2 分钟        ║");
            Console.WriteLine("║  HardwareStressTest.exe furmark 60  FurMark 跑 1 分钟      ║");
            Console.WriteLine("║  HardwareStressTest.exe gpu-full 60 GPU 全面测试 1 分钟    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
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
            Console.WriteLine($"   内存 {mem>>30}GB, {n}块\n");
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

        // ============ GPU D3D11 Compute ============
        static async Task RunGpu(int sec)
        {
            Console.WriteLine($"\n🔥 GPU 压力测试 (D3D11 Compute) {sec}秒\n");
            await RunD3D11Compute(sec, false);
        }

        static async Task<bool> RunD3D11Compute(int sec, bool silent)
        {
            var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware,
                DeviceCreationFlags.None, new[] { FeatureLevel.Level_11_0 },
                out ID3D11Device? device);

            if (result.Failure || device == null)
            {
                if (!silent) Console.WriteLine("❌ D3D11 不可用");
                await CpuSim(sec);
                return false;
            }

            var ctx = device.ImmediateContext;
            string gpuName = GetGpuName(device);
            if (!silent) Console.WriteLine($"   🖥️  GPU: {gpuName}\n");

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
            if (!silent) Console.Write("   🔧 编译着色器...");
            IntPtr GetBlobPtr(IntPtr blob, int idx) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), idx * IntPtr.Size);

            byte[] srcBytes = System.Text.Encoding.UTF8.GetBytes(hlsl);
            int hr = D3DCompile(srcBytes, (IntPtr)srcBytes.Length, null,
                IntPtr.Zero, IntPtr.Zero, "CSMain", "cs_5_0", 0, 0, out var code, out var errors);

            if (hr != 0 || code == IntPtr.Zero)
            {
                if (errors != IntPtr.Zero)
                {
                    var r = Marshal.GetDelegateForFunctionPointer<ReleaseDel>(GetBlobPtr(errors, 2));
                    r(errors);
                }
                if (!silent) Console.WriteLine(" ❌ 失败"); else Console.Write(".");
                ctx.Dispose(); device.Dispose();
                if (!silent) await CpuSim(sec);
                return false;
            }

            var gPtr = Marshal.GetDelegateForFunctionPointer<GetBufferPtrDel>(GetBlobPtr(code, 3));
            var gSize = Marshal.GetDelegateForFunctionPointer<GetSizeDel>(GetBlobPtr(code, 4));
            byte[] shaderBytes = new byte[gSize(code)];
            Marshal.Copy(gPtr(code), shaderBytes, 0, shaderBytes.Length);
            Marshal.GetDelegateForFunctionPointer<ReleaseDel>(GetBlobPtr(code, 2))(code);

            var shader = device.CreateComputeShader(shaderBytes);
            if (!silent) Console.WriteLine(" ✅ 成功\n");

            int totalElems = 128 * 512, bs = totalElems * 4;
            float[] dataA = new float[totalElems], dataB = new float[totalElems], dataC = new float[totalElems];
            var rnd = new Random(42);
            for (int i = 0; i < totalElems; i++) { dataA[i] = (float)rnd.NextDouble(); dataB[i] = (float)rnd.NextDouble(); }

            var bufA = MakeBuf(device, dataA, bs); var bufB = MakeBuf(device, dataB, bs); var bufC = MakeBuf(device, dataC, bs);
            var uavA = device.CreateUnorderedAccessView(bufA); var uavB = device.CreateUnorderedAccessView(bufB); var uavC = device.CreateUnorderedAccessView(bufC);
            ctx.CSSetShader(shader);
            ctx.CSSetUnorderedAccessViews(0, new ID3D11UnorderedAccessView[] { uavA, uavB, uavC });

            if (!silent) Console.WriteLine("   🚀 GPU 计算进行中...\n");

            var cts = new CancellationTokenSource();
            long dispatches = 0;
            var gpuTask = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    dispatches++;
                    ctx.Dispatch(1024, 1, 1);
                    ctx.Flush();
                    if (!silent && dispatches % 10 == 0) Console.Write($"D[{dispatches}] ");
                    if (silent && dispatches % 100 == 0) Console.Write(".");
                }
            });

            await Task.Delay(sec * 1000);
            cts.Cancel();
            try { await gpuTask; } catch { }

            shader.Dispose(); uavA.Dispose(); uavB.Dispose(); uavC.Dispose();
            bufA.Dispose(); bufB.Dispose(); bufC.Dispose();
            ctx.Dispose(); device.Dispose();

            long totalOps = dispatches * 1024L * 64L * 128L * 4L;
            if (!silent) Console.WriteLine($"\n\n✅ D3D11 Compute 完成！Disp:{dispatches} FLOPs:{totalOps:N0}");
            return true;
        }

        // ============ FurMark CLI 集成 ============
        static async Task RunFurMark(int sec)
        {
            string exePath = FindFurMark();
            if (exePath == null)
            {
                Console.WriteLine("\n❌ 未找到 FurMark_CMD.exe");
                Console.WriteLine("   请手动下载后放到本程序同目录：");
                Console.WriteLine("   1. 访问 https://geeks3d.com/furmark/downloads/");
                Console.WriteLine("   2. 下载 FurMark CLI 版本 (FurMark_CMD.exe)");
                Console.WriteLine("   3. 复制到本程序所在目录");
                Console.WriteLine();
                Console.WriteLine("   或者直接运行 gpu 模式使用内置 D3D11 Compute 引擎：");
                Console.WriteLine("   HardwareStressTest.exe gpu 60");
                return;
            }

            Console.WriteLine($"\n🔥 FurMark CLI GPU 渲染压力测试 {sec}秒\n");
            Console.WriteLine($"   程序路径: {exePath}");
            Console.WriteLine($"   分辨率: 自动(桌面分辨率)");
            Console.WriteLine($"   时长: {sec}秒\n");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"/run /duration={sec}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = new Process { StartInfo = psi };
            var outputLines = new List<string>();

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (outputLines) outputLines.Add(e.Data);
                    // 输出关键信息（忽略多余行）
                    if (e.Data.Contains("FPS") || e.Data.Contains("score") ||
                        e.Data.Contains("min") || e.Data.Contains("max") ||
                        e.Data.Contains("温度") || e.Data.Contains("temp"))
                        Console.WriteLine($"   {e.Data}");
                }
            };
            proc.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"   ⚠️  {e.Data}");
            };

            Console.WriteLine("   🚀 FurMark 渲染测试启动中...\n");
            Console.WriteLine("   (任务管理器 → GPU 查看使用率)\n");

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // 等待完成
            await Task.Run(() => proc.WaitForExit());

            Console.WriteLine($"\n✅ FurMark 测试完成！");
            Console.WriteLine($"   退出代码: {proc.ExitCode}");
            Console.WriteLine($"   💡 检查屏幕是否出现 FurMark 渲染窗口");
        }

        static string? FindFurMark()
        {
            // 搜索当前目录和 PATH
            string[] names = { "FurMark_CMD.exe", "FurMark_CMD", "FurMark.exe", "FurMark" };
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;

            foreach (var name in names)
            {
                // 当前目录
                string path = Path.Combine(exeDir, name.EndsWith(".exe") ? name : name + ".exe");
                if (File.Exists(path)) return path;

                // 上级目录
                path = Path.Combine(exeDir, "..", name.EndsWith(".exe") ? name : name + ".exe");
                if (File.Exists(path)) return Path.GetFullPath(path);

                // FurMark 典型安装目录
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                path = Path.Combine(progFiles, "Geeks3D", "FurMark", name.EndsWith(".exe") ? name : name + ".exe");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // ============ GPU 全面测试 ============
        static async Task RunGpuFull(int sec, int thr)
        {
            int half = sec / 2;

            Console.WriteLine($"\n🔥 GPU 全面压力测试（D3D11 Compute + FurMark CLI）\n");

            // 同时跑 D3D11 Compute 和 FurMark
            var d3dTask = RunD3D11Compute(half, true);
            var furmarkTask = RunFurMark(half);

            Console.WriteLine("   📋 并行执行：");
            Console.WriteLine("   - 任务1: D3D11 Compute Shader (GPU 计算压力)");
            Console.WriteLine("   - 任务2: FurMark CLI (GPU 渲染压力)\n");

            await Task.WhenAll(d3dTask, furmarkTask);

            Console.WriteLine("\n✅ GPU 全面测试完成！");
            Console.WriteLine("   💡 请检查任务管理器 GPU 使用率确认负载");
        }

        // ============ 辅助函数 ============
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate IntPtr GetBufferPtrDel(IntPtr blob);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetSizeDel(IntPtr blob);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReleaseDel(IntPtr blob);

        static ID3D11Buffer MakeBuf(ID3D11Device device, float[] data, int bs)
        {
            var desc = new BufferDescription((uint)bs, BindFlags.UnorderedAccess,
                ResourceUsage.Default, CpuAccessFlags.None,
                ResourceOptionFlags.BufferStructured, 4);
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { return device.CreateBuffer(desc, new SubresourceData(handle.AddrOfPinnedObject(), (uint)bs, 0)); }
            finally { handle.Free(); }
        }

        static string GetGpuName(ID3D11Device device)
        {
            try { using var dxgiDev = device.QueryInterface<IDXGIDevice>(); using var adapter = dxgiDev.GetAdapter(); return adapter.Description.Description; }
            catch { return "Unknown"; }
        }

        static async Task CpuSim(int sec)
        {
            const int s = 2048; var cts = new CancellationTokenSource(); long it = 0;
            var t = Task.Run(() => { while (!cts.Token.IsCancellationRequested) {
                var m1 = new float[s, s]; var m2 = new float[s, s]; var r = new float[s, s];
                for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { m1[i,j]=(float)(i*j%1000)/1000f; m2[i,j]=(float)((i+j)%1000)/1000f; }
                for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { float sum=0; for(int k=0;k<s;k++) sum+=m1[i,k]*m2[k,j]; r[i,j]=sum; }
                it++; if (it % 3 == 0) Console.Write($"S[{s}] "); }
            });
            await Task.Delay(sec * 1000); cts.Cancel(); try { await t; } catch { }
            Console.WriteLine($"\n✅ CPU 模拟完成！{it} 轮");
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
