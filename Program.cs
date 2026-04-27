using System;
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
        // D3DCompile P/Invoke - 原生 C API，无 COM 问题
        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, EntryPoint = "D3DCompile")]
        static extern int D3DCompile(byte[] srcData, IntPtr srcDataSize, string? sourceName,
            IntPtr defines, IntPtr include, string entryPoint, string target,
            uint flags1, uint flags2, out IntPtr code, out IntPtr errorMsgs);

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

        // ============ GPU: DirectX 11 Compute ============
        static async Task RunGpu(int sec)
        {
            Console.WriteLine($"\n🔥 GPU 压力测试 {sec}秒\n");

            // 创建 D3D11 设备
            var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware,
                DeviceCreationFlags.None, new[] { FeatureLevel.Level_11_0 },
                out ID3D11Device? device);

            if (result.Failure || device == null)
            {
                Console.WriteLine("❌ D3D11 不可用，回退 CPU 模拟\n");
                await CpuSim(sec);
                return;
            }

            var ctx = device.ImmediateContext;
            string gpuName = GetGpuName(device);
            Console.WriteLine($"   🖥️  GPU: {gpuName}\n");

            // HLSL 计算着色器
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
            Console.WriteLine("   🔧 编译着色器(D3DCompile)...");

            // 读取 ID3DBlob COM 接口方法
            IntPtr GetBlobPtr(IntPtr blob, int vtableIdx)
            {
                var vtbl = Marshal.ReadIntPtr(blob);
                return Marshal.ReadIntPtr(vtbl, vtableIdx * IntPtr.Size);
            }

            byte[] srcBytes = System.Text.Encoding.UTF8.GetBytes(hlsl);
            IntPtr code = IntPtr.Zero;
            IntPtr errors = IntPtr.Zero;

            int hr = D3DCompile(srcBytes, (IntPtr)srcBytes.Length, null,
                IntPtr.Zero, IntPtr.Zero, "CSMain", "cs_5_0", 0, 0, out code, out errors);

            if (hr != 0 || code == IntPtr.Zero)
            {
                string errMsg = "unknown";
                if (errors != IntPtr.Zero)
                {
                    // Release blob's buffer ptr via COM
                    // GetBufferPointer at vtable[3], GetBufferSize at vtable[4]
                    var getPtr = Marshal.GetDelegateForFunctionPointer<GetBufferPtrDel>(
                        GetBlobPtr(errors, 3));
                    var getSize = Marshal.GetDelegateForFunctionPointer<GetSizeDel>(
                        GetBlobPtr(errors, 4));
                    IntPtr errData = getPtr(errors);
                    int errSize = getSize(errors);
                    if (errData != IntPtr.Zero && errSize > 0)
                    {
                        byte[] errBytes = new byte[errSize];
                        Marshal.Copy(errData, errBytes, 0, errSize);
                        errMsg = System.Text.Encoding.UTF8.GetString(errBytes).TrimEnd('\0');
                    }
                    // Release errors blob
                    var release = Marshal.GetDelegateForFunctionPointer<ReleaseDel>(
                        GetBlobPtr(errors, 2));
                    release(errors);
                }
                Console.WriteLine($"   ❌ 编译失败: {hr} - {errMsg}");
                ctx.Dispose(); device.Dispose();
                await CpuSim(sec);
                return;
            }

            // 从 ID3DBlob 取数据
            var blobGetPtr = Marshal.GetDelegateForFunctionPointer<GetBufferPtrDel>(
                GetBlobPtr(code, 3));
            var blobGetSize = Marshal.GetDelegateForFunctionPointer<GetSizeDel>(
                GetBlobPtr(code, 4));

            IntPtr shaderData = blobGetPtr(code);
            int shaderSize = blobGetSize(code);

            byte[] shaderBytes = new byte[shaderSize];
            Marshal.Copy(shaderData, shaderBytes, 0, shaderSize);

            // 释放 code blob
            var blobRelease = Marshal.GetDelegateForFunctionPointer<ReleaseDel>(
                GetBlobPtr(code, 2));
            blobRelease(code);

            var shader = device.CreateComputeShader(shaderBytes);
            Console.WriteLine("   ✅ 编译成功\n");

            // 创建缓冲区
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

            Console.WriteLine("   🚀 GPU 计算进行中...\n");
            Console.WriteLine("   (任务管理器 > GPU 查看使用率)\n");

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
            Console.WriteLine($"\n\n✅ GPU (D3D11 Compute) 完成！");
            Console.WriteLine($"   GPU: {gpuName}");
            Console.WriteLine($"   Dispatch: {dispatches} 次");
            Console.WriteLine($"   ⚡ 运算: ~{totalOps:N0} FLOPs");
        }

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
            try {
                return device.CreateBuffer(desc, new SubresourceData(handle.AddrOfPinnedObject(), (uint)bs, 0));
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

        static async Task CpuSim(int sec)
        {
            const int s = 2048;
            var cts = new CancellationTokenSource();
            long it = 0;
            var t = Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    var m1 = new float[s, s]; var m2 = new float[s, s]; var r = new float[s, s];
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { m1[i,j]=(float)(i*j%1000)/1000f; m2[i,j]=(float)((i+j)%1000)/1000f; }
                    for (int i = 0; i < s; i++) for (int j = 0; j < s; j++) { float sum=0; for(int k=0;k<s;k++) sum+=m1[i,k]*m2[k,j]; r[i,j]=sum; }
                    it++; if (it % 3 == 0) Console.Write($"S[{s}] "); else Console.Write(".");
                }
            });
            await Task.Delay(sec * 1000); cts.Cancel();
            try { await t; } catch { }
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
