using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace GeoToolkit.Collections.Examples
{
    /// <summary>
    /// NativeArray64 和 NativeList64 的使用示例
    /// 演示如何处理超大数据（超过 int.MaxValue）
    /// </summary>
    public class Native64Example : MonoBehaviour
    {
        void Start()
        {
            Example_LargeArray();
            Example_Conversion();
            Example_DynamicList();
            Example_Slicing();
            Example_BurstAndParallelJobs();
        }

        /// <summary>
        /// 示例1: 创建超大数组
        /// 演示如何创建和使用超过 int.MaxValue 容量的数组
        /// </summary>
        void Example_LargeArray()
        {
            Debug.Log("=== 示例1: 超大数组 ===");

            // 创建一个可以容纳超过 int.MaxValue 元素的数组
            // 注意：实际分配这么大的内存需要足够的 RAM
            ulong largeSize = (ulong)uint.MaxValue * 4;
            var largeArray = new NativeArray64<byte>(largeSize, Allocator.Temp);
            try
            {
                // 填充一些数据
                for (ulong i = 0; i < Math.Min(largeSize, 10); i++)
                {
                    largeArray[i] = (byte)(i % 127);
                }

                Debug.Log($"已创建包含 {largeArray.Length} 个元素的 NativeArray64");
                Debug.Log($"第一个元素: {largeArray[0]}");
                Debug.Log($"最后一个可访问索引: {largeArray.Length - 1}");
            }
            finally
            {
                largeArray.Dispose();
            }
        }

        /// <summary>
        /// 示例2: 类型转换
        /// 演示 NativeArray 和 NativeArray64 之间的相互转换
        /// </summary>
        void Example_Conversion()
        {
            Debug.Log("=== 示例2: 类型转换 ===");

            // 创建一个普通的 NativeArray
            var regularArray = new NativeArray<int>(100, Allocator.Temp);
            try
            {
                // 填充数据
                for (int i = 0; i < regularArray.Length; i++)
                {
                    regularArray[i] = i;
                }

                // 转换为 NativeArray64
                using (var array64 = regularArray.ToNativeArray64(Allocator.Temp))
                {
                    Debug.Log($"已将 NativeArray（大小: {regularArray.Length}）转换为 NativeArray64（大小: {array64.Length}）");

                    // 验证数据完整性
                    bool dataMatch = true;
                    for (int i = 0; i < regularArray.Length; i++)
                    {
                        if (regularArray[i] != array64[(ulong)i])
                        {
                            dataMatch = false;
                            break;
                        }
                    }
                    Debug.Log($"转换后的数据完整性: {(dataMatch ? "验证通过" : "验证失败")}");

                    // 如果可能，转换回普通数组
                    if (array64.CanConvertToNativeArray())
                    {
                        using (var backToRegular = array64.ToNativeArray(Allocator.Temp))
                        {
                            Debug.Log($"成功转换回普通 NativeArray");
                        }
                    }
                }
            }
            finally
            {
                regularArray.Dispose();
            }
        }

        /// <summary>
        /// 示例3: 动态列表
        /// 演示 NativeList64 的动态扩容和各种操作
        /// </summary>
        void Example_DynamicList()
        {
            Debug.Log("=== 示例3: 动态列表 ===");

            var allocator = new AllocatorManager.AllocatorHandle { Index = (ushort)Allocator.Temp };

            using (var list64 = new NativeList64<double>(10, allocator))
            {
                Debug.Log($"初始容量: {list64.Capacity}, 长度: {list64.Length}");

                // 添加元素
                for (ulong i = 0; i < 100; i++)
                {
                    list64.Add(Math.Sqrt(i));
                }

                Debug.Log($"添加 100 个元素后 - 容量: {list64.Capacity}, 长度: {list64.Length}");

                // 添加一组数据
                var moreData = new double[] { 1.1, 2.2, 3.3, 4.4, 5.5 };
                unsafe
                {
                    fixed (double* ptr = moreData)
                    {
                        list64.AddRange(ptr, (ulong)moreData.Length);
                    }
                }

                Debug.Log($"添加数组后 - 长度: {list64.Length}");

                // 移除元素（使用交换后移除方式）
                list64.RemoveAtSwapBack(0);
                Debug.Log($"移除第一个元素后 - 长度: {list64.Length}");

                // 清空并裁剪
                list64.Clear();
                Debug.Log($"清空后 - 长度: {list64.Length}, 容量: {list64.Capacity}");

                list64.TrimExcess();
                Debug.Log($"裁剪后 - 容量: {list64.Capacity}");
            }
        }

        /// <summary>
        /// 示例4: 数组切片
        /// 演示如何将大数组切片为多个小块进行处理
        /// </summary>
        void Example_Slicing()
        {
            Debug.Log("=== 示例4: 数组切片 ===");

            // 创建一个较大的数组
            ulong totalSize = 10000;
            var largeArray = new NativeArray64<byte>(totalSize, Allocator.Temp);
            try
            {
                // 填充数据
                for (ulong i = 0; i < totalSize; i++)
                {
                    largeArray[i] = (byte)(i % 256);
                }

                // 将大数组切片为可以作为普通 NativeArray 处理的小块
                int chunkSize = 1000;
                var chunks = largeArray.Slice(chunkSize, Allocator.Temp);

                Debug.Log($"已将 {totalSize} 个元素的数组切分为 {chunks.Length} 个块，每块最多 {chunkSize} 个元素");

                // 处理每个块
                for (int i = 0; i < chunks.Length; i++)
                {
                    Debug.Log($"  块 {i}: {chunks[i].Length} 个元素");
                    chunks[i].Dispose();
                }

                // 替代方法: 获取子数组作为普通 NativeArray
                if (totalSize > 100)
                {
                    var subArray = largeArray.GetSubArrayAsNativeArray(50, 100);
                    Debug.Log($"获取了从索引 50 开始的 100 个元素的子数组: {subArray.Length}");
                    // 注意: subArray 与 largeArray 共享内存，不要释放它
                }
            }
            finally
            {
                largeArray.Dispose();
            }
        }

        /// <summary>
        /// 用于演示 IJobParallelFor64 的 Burst 编译 Job
        /// 该 Job 支持 64 位索引，可以处理超大数组
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        unsafe struct ParallelFor64Job : IJobParallelFor64
        {
            [NativeDisableUnsafePtrRestriction]
            public byte* dataPtr;
            public byte addValue;

            /// <summary>
            /// 对每个元素执行操作
            /// </summary>
            /// <param name="index">64 位索引</param>
            public void Execute(ulong index)
            {
                // 对每个元素执行 10 次累加操作
                for (int i = 0; i < 10; ++i)
                {
                    dataPtr[index] = (byte)((dataPtr[index] + addValue) % 256);
                }
            }
        }

        /// <summary>
        /// 示例5: Burst 编译和并行 Job
        /// 演示如何使用 Burst 编译和并行 Job 系统处理超大数组
        /// </summary>
        void Example_BurstAndParallelJobs()
        {
            Debug.Log("=== 示例5: Burst 编译和并行 Job ===");

            // 创建超大数组（uint.MaxValue * 4 字节）
            ulong size = (ulong)uint.MaxValue * 4;
            Debug.Log($"正在分配 {size:N0} 字节（{size / (1024.0 * 1024.0 * 1024.0):F2} GB）...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = new NativeArray64<byte>(size, Allocator.TempJob);
            try
            {
                Debug.Log($"分配耗时 {sw.ElapsedMilliseconds}ms");

                // 使用 IJobParallelFor64 自动在多个 CPU 核心上并行处理
                sw.Restart();
                Debug.Log($"正在调度 IJobParallelFor64（自动并行化到 {UnityEngine.SystemInfo.processorCount} 个 CPU 核心）...");

                unsafe
                {
                    var parallelFor64Job = new ParallelFor64Job
                    {
                        dataPtr = (byte*)data.GetUnsafePtr(),
                        addValue = 7
                    };

                    // 调度 Job，每批处理 100 万个元素
                    JobHandle handle = parallelFor64Job.Schedule64(
                        arrayLength: size,
                        innerloopBatchCount: 1000000
                    );
                    handle.Complete();
                }

                Debug.Log($"IJobParallelFor64 处理耗时 {sw.ElapsedMilliseconds}ms");
                Debug.Log($"已使用自动并行化处理 {size:N0} 个元素");
                Debug.Log($"采样结果: data[0]={data[0]}, data[1000]={data[1000]}, data[{size / 2}]={data[size / 2]}, data[{size - 1}]={data[size - 1]}");
            }
            finally
            {
                data.Dispose();
                Debug.Log("数据已释放");
            }

            Debug.Log("");
            Debug.Log("Burst 和并行 Job 测试完成");
        }
    }
}
