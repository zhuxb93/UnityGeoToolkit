using System;
using Unity.Burst;
using Unity.Jobs;

namespace GeoToolkit.Collections
{
    /// <summary>
    /// Job interface for parallel processing with 64-bit indices.
    /// Supports arrays larger than int.MaxValue.
    /// </summary>
    public interface IJobParallelFor64
    {
        /// <summary>
        /// Execute the job for a specific element index.
        /// </summary>
        /// <param name="index">The 64-bit index of the element to process.</param>
        void Execute(ulong index);
    }

    /// <summary>
    /// Extension methods for scheduling IJobParallelFor64 jobs.
    /// </summary>
    public static class IJobParallelFor64Extensions
    {
        /// <summary>
        /// Schedule a parallel job for processing 64-bit indexed data.
        /// The job will be split into multiple parallel chunks.
        /// </summary>
        /// <param name="jobData">The job to execute.</param>
        /// <param name="arrayLength">Total number of elements to process.</param>
        /// <param name="innerloopBatchCount">Number of elements to process per batch.</param>
        /// <param name="dependsOn">Optional dependency.</param>
        /// <returns>JobHandle for the scheduled jobs.</returns>
        public static JobHandle Schedule64<T>(
            this T jobData,
            ulong arrayLength,
            ulong innerloopBatchCount,
            JobHandle dependsOn = default) where T : struct, IJobParallelFor64
        {
            if (arrayLength == 0)
                return dependsOn;

            // Determine number of worker threads (use CPU core count)
            int workerThreads = UnityEngine.SystemInfo.processorCount;

            // Calculate chunk size per worker
            ulong chunkSize = (arrayLength + (ulong)workerThreads - 1) / (ulong)workerThreads;

            // Ensure chunk size respects batch count
            if (chunkSize < innerloopBatchCount)
                chunkSize = innerloopBatchCount;

            // Schedule jobs for each chunk
            // Collect all handles and combine them at the end
            var handles = new System.Collections.Generic.List<JobHandle>(workerThreads);

            for (int i = 0; i < workerThreads; i++)
            {
                ulong startIndex = (ulong)i * chunkSize;
                if (startIndex >= arrayLength)
                    break;

                ulong endIndex = Math.Min(startIndex + chunkSize, arrayLength);

                var wrapper = new JobParallelFor64Wrapper<T>
                {
                    JobData = jobData,
                    StartIndex = startIndex,
                    EndIndex = endIndex
                };

                JobHandle handle = wrapper.Schedule(dependsOn);
                handles.Add(handle);
            }

            // Combine all handles
            if (handles.Count == 0)
                return dependsOn;
            if (handles.Count == 1)
                return handles[0];

            JobHandle combinedHandle = JobHandle.CombineDependencies(handles[0], handles[1]);
            for (int i = 2; i < handles.Count; i++)
            {
                combinedHandle = JobHandle.CombineDependencies(combinedHandle, handles[i]);
            }

            return combinedHandle;
        }

        /// <summary>
        /// Run the job immediately on the main thread.
        /// </summary>
        public static void Run64<T>(this T jobData, ulong arrayLength) where T : struct, IJobParallelFor64
        {
            for (ulong i = 0; i < arrayLength; i++)
            {
                jobData.Execute(i);
            }
        }
    }

    /// <summary>
    /// Internal wrapper that executes a chunk of the parallel job.
    /// </summary>
    [BurstCompile]
    internal struct JobParallelFor64Wrapper<T> : IJob where T : struct, IJobParallelFor64
    {
        public T JobData;
        public ulong StartIndex;
        public ulong EndIndex;

        public void Execute()
        {
            for (ulong i = StartIndex; i < EndIndex; i++)
            {
                JobData.Execute(i);
            }
        }
    }
}
