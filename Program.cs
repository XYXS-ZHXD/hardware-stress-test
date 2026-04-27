using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareStressTest
{
    class Program
    {
        // ==================== OpenCL P/Invoke 声明 ====================
        [DllImport("OpenCL.dll", EntryPoint = "clGetPlatformIDs")]
        private static extern int clGetPlatformIDs(uint numEntries, IntPtr[] platforms, out uint numPlatforms);

        [DllImport("OpenCL.dll", EntryPoint = "clGetPlatformInfo")]
        private static extern int clGetPlatformInfo(IntPtr platform, uint paramName, UIntPtr paramValueSize, byte[] paramValue, out UIntPtr paramValueSizeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clGetDeviceIDs")]
        private static extern int clGetDeviceIDs(IntPtr platform, long deviceType, uint numEntries, IntPtr[] devices, out uint numDevices);

        [DllImport("OpenCL.dll", EntryPoint = "clGetDeviceInfo")]
        private static extern int clGetDeviceInfo(IntPtr device, uint paramName, UIntPtr paramValueSize, byte[] paramValue, out UIntPtr paramValueSizeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clCreateContext")]
        private static extern IntPtr clCreateContext(IntPtr[] properties, uint numDevices, IntPtr[] devices, IntPtr pfnNotify, IntPtr userData, out int errcodeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clCreateCommandQueueWithProperties")]
        private static extern IntPtr clCreateCommandQueueWithProperties(IntPtr context, IntPtr device, IntPtr properties, out int errcodeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clCreateProgramWithSource")]
        private static extern IntPtr clCreateProgramWithSource(IntPtr context, uint count, string[] strings, UIntPtr[] lengths, out int errcodeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clBuildProgram")]
        private static extern int clBuildProgram(IntPtr program, uint numDevices, IntPtr[] deviceList, string options, IntPtr pfnNotify, IntPtr userData);

        [DllImport("OpenCL.dll", EntryPoint = "clGetProgramBuildInfo")]
        private static extern int clGetProgramBuildInfo(IntPtr program, IntPtr device, uint paramName, UIntPtr paramValueSize, byte[] paramValue, out UIntPtr paramValueSizeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clCreateKernel")]
        private static extern IntPtr clCreateKernel(IntPtr program, string kernelName, out int errcodeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clSetKernelArg")]
        private static extern int clSetKernelArg(IntPtr kernel, uint argIndex, UIntPtr argSize, IntPtr argValue);

        [DllImport("OpenCL.dll", EntryPoint = "clCreateBuffer")]
        private static extern IntPtr clCreateBuffer(IntPtr context, long flags, UIntPtr size, IntPtr hostPtr, out int errcodeRet);

        [DllImport("OpenCL.dll", EntryPoint = "clEnqueueWriteBuffer")]
        private static extern int clEnqueueWriteBuffer(IntPtr commandQueue, IntPtr buffer, int blockingWrite, UIntPtr offset, UIntPtr size, IntPtr ptr, uint numEventsInWaitList, IntPtr eventWaitList, IntPtr eventPtr);

        [DllImport("OpenCL.dll", EntryPoint = "clEnqueueReadBuffer")]
        private static extern int clEnqueueReadBuffer(IntPtr commandQueue, IntPtr buffer, int blockingRead, UIntPtr offset, UIntPtr size, IntPtr ptr, uint numEventsInWaitList, IntPtr eventWaitList, IntPtr eventPtr);

        [DllImport("OpenCL.dll", EntryPoint = "clEnqueueNDRangeKernel")]
        private static extern int clEnqueueNDRangeKernel(IntPtr commandQueue, IntPtr kernel, uint workDim, UIntPtr[] globalWorkOffset, UIntPtr[] globalWorkSize, UIntPtr[] localWorkSize, uint numEventsInWaitList, IntPtr eventWaitList, IntPtr eventPtr);

        [DllImport("OpenCL.dll", EntryPoint = "clFinish")]
        private static extern int clFinish(IntPtr commandQueue);

        [DllImport("OpenCL.dll", EntryPoint = "clReleaseMemObject")]
        private static extern int clReleaseMemObject(IntPtr memObj);

        [DllImport("OpenCL.dll", EntryPoint = "clReleaseKernel")]
        private static extern int clReleaseKernel(IntPtr kernel);

        [DllImport("OpenCL.dll", EntryPoint = "clReleaseProgram")]
        private static extern int clReleaseProgram(IntPtr program);

        [DllImport("OpenCL.dll", EntryPoint = "clReleaseCommandQueue")]
        private static extern int clReleaseCommandQueue(IntPtr commandQueue);

        [DllImport("OpenCL.dll", EntryPoint = "clReleaseContext")]
        private static extern int clReleaseContext(IntPtr context);

        // OpenCL 常量
        const int CL_SUCCESS = 0;
        const long CL_DEVICE_TYPE_GPU = 1L << 2;
        const uint CL_PLATFORM_NAME = 0x0902;
        const uint CL_DEVICE_NAME = 0x1023;
        const uint CL_DEVICE_VENDOR = 0x1024;
        const uint CL_PROGRAM_BUILD_LOG = 0x1183;
        const long CL_MEM_READ_WRITE = 1;

        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     电脑 DIY 硬件压力测试工具 v1.2 - 绿色版           ║");
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
            Console.WriteLine("  gpu     - GPU 压力测试（OpenCL 真实 GPU 计算）");
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

        // ==================== GPU 压力测试（真实 OpenCL） ====================
        static async Task RunGpuTest(int durationSeconds)
        {
            Console.WriteLine($"\n🔥 开始 GPU 压力测试（OpenCL 真实 GPU 计算）");
            Console.WriteLine($"   持续时间：{durationSeconds}秒\n");

            // 尝试真正的 OpenCL GPU 测试
            if (TryRunOpenCLGpuTest(durationSeconds))
                return;

            // 回退到增强型 CPU 模拟
            Console.WriteLine("\n⚠️  OpenCL 不可用，回退到增强型 CPU 模拟 GPU 压力...\n");
            await RunEnhancedGpuSimulation(durationSeconds);
        }

        // 真正的 OpenCL GPU 压力测试
        static bool TryRunOpenCLGpuTest(int durationSeconds)
        {
            try
            {
                // 1. 获取平台
                uint numPlatforms;
                if (clGetPlatformIDs(0, null, out numPlatforms) != CL_SUCCESS || numPlatforms == 0)
                {
                    Console.WriteLine("   ❌ 未找到 OpenCL 平台");
                    return false;
                }

                IntPtr[] platforms = new IntPtr[numPlatforms];
                clGetPlatformIDs(numPlatforms, platforms, out numPlatforms);

                IntPtr gpuPlatform = IntPtr.Zero;
                IntPtr gpuDevice = IntPtr.Zero;
                string gpuName = "";

                // 2. 遍历平台找 GPU
                for (int p = 0; p < (int)numPlatforms; p++)
                {
                    uint numGpus;
                    if (clGetDeviceIDs(platforms[p], CL_DEVICE_TYPE_GPU, 0, null, out numGpus) == CL_SUCCESS && numGpus > 0)
                    {
                        IntPtr[] gpus = new IntPtr[numGpus];
                        clGetDeviceIDs(platforms[p], CL_DEVICE_TYPE_GPU, numGpus, gpus, out numGpus);
                        gpuPlatform = platforms[p];
                        gpuDevice = gpus[0];

                        // 获取 GPU 名称
                        UIntPtr sizeRet;
                        clGetDeviceInfo(gpuDevice, CL_DEVICE_NAME, UIntPtr.Zero, null, out sizeRet);
                        byte[] nameBytes = new byte[(int)sizeRet];
                        clGetDeviceInfo(gpuDevice, CL_DEVICE_NAME, (UIntPtr)nameBytes.Length, nameBytes, out sizeRet);
                        gpuName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                        break;
                    }
                }

                if (gpuDevice == IntPtr.Zero)
                {
                    Console.WriteLine("   ❌ 未找到 GPU 设备");
                    return false;
                }

                Console.WriteLine($"   ✅ 检测到 GPU：{gpuName}");
                Console.WriteLine($"   🚀 正在加载 OpenCL 计算内核...\n");

                // 3. 创建 OpenCL 上下文
                int err;
                IntPtr context = clCreateContext(null, 1, new[] { gpuDevice }, IntPtr.Zero, IntPtr.Zero, out err);
                if (context == IntPtr.Zero || err != CL_SUCCESS)
                {
                    Console.WriteLine($"   ❌ 创建上下文失败 (错误码: {err})");
                    return false;
                }

                // 4. 创建命令队列
                IntPtr queue = clCreateCommandQueueWithProperties(context, gpuDevice, IntPtr.Zero, out err);
                if (queue == IntPtr.Zero || err != CL_SUCCESS)
                {
                    Console.WriteLine($"   ❌ 创建命令队列失败 (错误码: {err})");
                    clReleaseContext(context);
                    return false;
                }

                // 5. OpenCL 内核源码 - 大规模矩阵乘法
                string kernelSource = @"
__kernel void matrixMul(__global float* A, __global float* B, __global float* C, int N) {
    int row = get_global_id(0);
    int col = get_global_id(1);
    
    if (row < N && col < N) {
        float sum = 0.0f;
        for (int k = 0; k < N; k++) {
            sum += A[row * N + k] * B[k * N + col];
        }
        C[row * N + col] = sum;
    }
}

__kernel void vectorAdd(__global float* A, __global float* B, __global float* C, int N) {
    int i = get_global_id(0);
    if (i < N) {
        C[i] = A[i] + B[i] + sin(A[i]) * cos(B[i]);
    }
}

__kernel void computePi(__global float* results, int steps) {
    int i = get_global_id(0);
    if (i < steps) {
        float x = (i + 0.5f) / steps;
        results[i] = 4.0f / (1.0f + x * x);
    }
}
";
                string[] kernelStrings = { kernelSource };
                UIntPtr[] kernelLengths = { (UIntPtr)kernelSource.Length };

                // 6. 创建程序
                IntPtr program = clCreateProgramWithSource(context, 1, kernelStrings, kernelLengths, out err);
                if (program == IntPtr.Zero || err != CL_SUCCESS)
                {
                    Console.WriteLine($"   ❌ 创建程序失败 (错误码: {err})");
                    clReleaseCommandQueue(queue);
                    clReleaseContext(context);
                    return false;
                }

                // 7. 编译程序
                err = clBuildProgram(program, 1, new[] { gpuDevice }, null, IntPtr.Zero, IntPtr.Zero);
                if (err != CL_SUCCESS)
                {
                    // 获取编译日志
                    clGetProgramBuildInfo(program, gpuDevice, CL_PROGRAM_BUILD_LOG, UIntPtr.Zero, null, out var logSize);
                    byte[] logBytes = new byte[(int)logSize];
                    clGetProgramBuildInfo(program, gpuDevice, CL_PROGRAM_BUILD_LOG, (UIntPtr)logBytes.Length, logBytes, out logSize);
                    string buildLog = Encoding.ASCII.GetString(logBytes).TrimEnd('\0');
                    Console.WriteLine($"   ❌ 内核编译失败:\n   {buildLog}");

                    clReleaseProgram(program);
                    clReleaseCommandQueue(queue);
                    clReleaseContext(context);
                    return false;
                }

                Console.WriteLine("   ✅ OpenCL 内核编译成功！开始 GPU 压力测试...\n");

                // 8. 创建内核
                IntPtr kernel = clCreateKernel(program, "matrixMul", out err);
                if (kernel == IntPtr.Zero || err != CL_SUCCESS)
                {
                    Console.WriteLine($"   ❌ 创建内核失败 (错误码: {err})");
                    clReleaseProgram(program);
                    clReleaseCommandQueue(queue);
                    clReleaseContext(context);
                    return false;
                }

                // 9. 开始 GPU 压力测试循环
                int matrixSize = 1024;
                int totalSize = matrixSize * matrixSize;
                int dataSize = totalSize * sizeof(float);

                // 分配 GPU 内存
                IntPtr d_A = clCreateBuffer(context, CL_MEM_READ_WRITE, (UIntPtr)dataSize, IntPtr.Zero, out err);
                IntPtr d_B = clCreateBuffer(context, CL_MEM_READ_WRITE, (UIntPtr)dataSize, IntPtr.Zero, out err);
                IntPtr d_C = clCreateBuffer(context, CL_MEM_READ_WRITE, (UIntPtr)dataSize, IntPtr.Zero, out err);

                // 设置内核参数
                clSetKernelArg(kernel, 0, (UIntPtr)IntPtr.Size, d_A);
                clSetKernelArg(kernel, 1, (UIntPtr)IntPtr.Size, d_B);
                clSetKernelArg(kernel, 2, (UIntPtr)IntPtr.Size, d_C);
                int nVal = matrixSize;
                GCHandle nHandle = GCHandle.Alloc(nVal, GCHandleType.Pinned);
                clSetKernelArg(kernel, 3, (UIntPtr)sizeof(int), nHandle.AddrOfPinnedObject());

                // 主机端数据
                float[] h_A = new float[totalSize];
                float[] h_B = new float[totalSize];
                var random = new Random();
                for (int i = 0; i < totalSize; i++)
                {
                    h_A[i] = (float)random.NextDouble();
                    h_B[i] = (float)random.NextDouble();
                }

                // 上传到 GPU
                GCHandle aHandle = GCHandle.Alloc(h_A, GCHandleType.Pinned);
                GCHandle bHandle = GCHandle.Alloc(h_B, GCHandleType.Pinned);
                clEnqueueWriteBuffer(queue, d_A, 1, UIntPtr.Zero, (UIntPtr)dataSize, aHandle.AddrOfPinnedObject(), 0, IntPtr.Zero, IntPtr.Zero);
                clEnqueueWriteBuffer(queue, d_B, 1, UIntPtr.Zero, (UIntPtr)dataSize, bHandle.AddrOfPinnedObject(), 0, IntPtr.Zero, IntPtr.Zero);

                long totalOps = 0;
                int gpuPasses = 0;
                var cts = new CancellationTokenSource();

                var gpuTask = Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        gpuPasses++;

                        // 执行 GPU 矩阵乘法
                        UIntPtr[] globalSize = { (UIntPtr)matrixSize, (UIntPtr)matrixSize };
                        UIntPtr[] localSize = { (UIntPtr)16, (UIntPtr)16 };
                        
                        err = clEnqueueNDRangeKernel(queue, kernel, 2, null, globalSize, localSize, 0, IntPtr.Zero, IntPtr.Zero);
                        
                        if (err == CL_SUCCESS)
                        {
                            // 等待完成
                            clFinish(queue);
                            
                            long opsPerPass = (long)matrixSize * matrixSize * matrixSize * 2;
                            totalOps += opsPerPass;

                            if (gpuPasses % 5 == 0)
                                Console.Write($"G[{gpuPasses}] ");
                            else
                                Console.Write("G");
                        }
                        else
                        {
                            Console.Write("!");
                        }
                    }
                    sw.Stop();
                });

                // 等待指定时间
                Task.Delay(durationSeconds * 1000).Wait();
                cts.Cancel();
                try { gpuTask.Wait(); } catch (AggregateException) { }

                // 清理
                aHandle.Free();
                bHandle.Free();
                nHandle.Free();
                clReleaseMemObject(d_A);
                clReleaseMemObject(d_B);
                clReleaseMemObject(d_C);
                clReleaseKernel(kernel);
                clReleaseProgram(program);
                clReleaseCommandQueue(queue);
                clReleaseContext(context);

                Console.WriteLine($"\n✅ GPU 压力测试完成！持续时间：{durationSeconds}秒");
                Console.WriteLine($"   共执行 {gpuPasses} 次 GPU 矩阵乘法 ({matrixSize}x{matrixSize})");
                Console.WriteLine($"   总浮点运算量：{totalOps:N0} FLOPs");
                Console.WriteLine($"   GPU 型号：{gpuName}");
                Console.WriteLine($"   ✅ 说明：已完成真实 GPU 计算压力测试，请检查 GPU 负载");
                return true;
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("   ❌ 未找到 OpenCL.dll，您的显卡驱动未安装 OpenCL");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ OpenCL 测试失败：{ex.Message}");
                return false;
            }
        }

        // 增强型 CPU 模拟 GPU（作为回退）
        static async Task RunEnhancedGpuSimulation(int durationSeconds)
        {
            Console.WriteLine("💡 提示：请安装 Intel 显卡驱动（含 OpenCL 支持）以使用真实 GPU 测试");
            Console.WriteLine("   下载地址：https://www.intel.com/content/www/us/en/download/785597/\n");

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
                        Console.Write("S");
                }
            });

            await Task.Delay(durationSeconds * 1000);
            cts.Cancel();
            try { await simTask; } catch (OperationCanceledException) { }

            Console.WriteLine($"\n✅ GPU 压力测试（CPU 模拟）完成！持续时间：{durationSeconds}秒，{iterations} 轮");
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
}
