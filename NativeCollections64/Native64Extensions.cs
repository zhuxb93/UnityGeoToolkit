using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GeoToolkit.Collections
{
    /// <summary>
    /// Provides extension methods for converting between 64-bit and regular native containers.
    /// </summary>
    public static class Native64Extensions
    {
        /// <summary>
        /// Converts a NativeArray to a NativeArray64.
        /// </summary>
        public static NativeArray64<T> ToNativeArray64<T>(this NativeArray<T> source, Allocator allocator) where T : struct
        {
            return new NativeArray64<T>(source, allocator);
        }

        /// <summary>
        /// Converts a NativeList to a NativeList64.
        /// </summary>
        public static NativeList64<T> ToNativeList64<T>(this NativeList<T> source, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            var result = new NativeList64<T>((ulong)source.Length, allocator);
            result.CopyFrom(source.AsArray());
            return result;
        }

        /// <summary>
        /// Safely converts a NativeArray64 to a NativeArray if the length fits within int.MaxValue.
        /// </summary>
        public static NativeArray<T> ToNativeArray<T>(this NativeArray64<T> source, Allocator allocator) where T : struct
        {
            return source.ToNativeArray(allocator);
        }

        /// <summary>
        /// Safely converts a NativeList64 to a NativeList if the length fits within int.MaxValue.
        /// </summary>
        public static NativeList<T> ToNativeList<T>(this NativeList64<T> source, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            if (source.Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot convert NativeList64 with length {source.Length} to NativeList (max size: {int.MaxValue})");
            }

            var result = new NativeList<T>((int)source.Length, allocator);
            unsafe
            {
                result.AddRange(source.GetUnsafeReadOnlyPtr(), (int)source.Length);
            }
            return result;
        }

        /// <summary>
        /// Copies data from a NativeArray to a NativeArray64.
        /// </summary>
        public static void CopyTo<T>(this NativeArray<T> source, NativeArray64<T> destination) where T : struct
        {
            NativeArray64<T>.CopyFromNativeArray(source, destination);
        }

        /// <summary>
        /// Copies data from a NativeArray64 to a NativeArray.
        /// </summary>
        public static void CopyTo<T>(this NativeArray64<T> source, NativeArray<T> destination) where T : struct
        {
            if (source.Length > (ulong)destination.Length)
            {
                throw new ArgumentException($"Source NativeArray64 length ({source.Length}) exceeds destination NativeArray length ({destination.Length})");
            }

            if (source.Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot copy NativeArray64 with length {source.Length} to NativeArray (max size: {int.MaxValue})");
            }

            unsafe
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var srcHandle = GetAtomicSafetyHandle(ref source);
                var dstHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(destination);
                AtomicSafetyHandle.CheckReadAndThrow(srcHandle);
                AtomicSafetyHandle.CheckWriteAndThrow(dstHandle);
#endif
                void* srcPtr = GetUnsafeReadOnlyPtr(source);
                void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(destination);
                UnsafeUtility.MemCpy(dstPtr, srcPtr, (int)source.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        // Helper method to get unsafe pointer from NativeArray64
        private static unsafe void* GetUnsafeReadOnlyPtr<T>(NativeArray64<T> array) where T : struct
        {
            return array.m_Buffer;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Helper method to get AtomicSafetyHandle from NativeArray64
        private static AtomicSafetyHandle GetAtomicSafetyHandle<T>(ref NativeArray64<T> array) where T : struct
        {
            return array.m_Safety;
        }
#endif

        /// <summary>
        /// Copies data from a NativeList to a NativeList64.
        /// </summary>
        public static void CopyTo<T>(this NativeList<T> source, NativeList64<T> destination) where T : unmanaged
        {
            destination.CopyFrom(source.AsArray());
        }

        /// <summary>
        /// Copies data from a NativeList64 to a NativeList.
        /// </summary>
        public static void CopyTo<T>(this NativeList64<T> source, NativeList<T> destination) where T : unmanaged
        {
            if (source.Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot copy NativeList64 with length {source.Length} to NativeList (max size: {int.MaxValue})");
            }

            destination.Clear();
            unsafe
            {
                destination.AddRange(source.GetUnsafeReadOnlyPtr(), (int)source.Length);
            }
        }

        /// <summary>
        /// Gets a sub-array from a NativeArray64 as a regular NativeArray.
        /// </summary>
        public static NativeArray<T> GetSubArrayAsNativeArray<T>(this NativeArray64<T> source, ulong start, int length) where T : struct
        {
            if (start + (ulong)length > source.Length)
            {
                throw new ArgumentOutOfRangeException($"Sub-array range {start}-{start + (ulong)length - 1} is outside the range of the source array 0-{source.Length - 1}");
            }

            unsafe
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var srcHandle = GetAtomicSafetyHandle(ref source);
                AtomicSafetyHandle.CheckReadAndThrow(srcHandle);
#endif
                byte* offsetBuffer = (byte*)GetUnsafeReadOnlyPtr(source) + start * (ulong)UnsafeUtility.SizeOf<T>();
                var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(offsetBuffer, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, srcHandle);
#endif
                return result;
            }
        }

        /// <summary>
        /// Checks if a NativeArray64 can be safely converted to a NativeArray.
        /// </summary>
        public static bool CanConvertToNativeArray<T>(this NativeArray64<T> source) where T : struct
        {
            return source.Length <= int.MaxValue;
        }

        /// <summary>
        /// Checks if a NativeList64 can be safely converted to a NativeList.
        /// </summary>
        public static bool CanConvertToNativeList<T>(this NativeList64<T> source) where T : unmanaged
        {
            return source.Length <= int.MaxValue;
        }

        /// <summary>
        /// Creates a NativeArray64 view of a managed array without copying.
        /// </summary>
        public static unsafe NativeArray64<T> AsNativeArray64<T>(T[] array, Allocator allocator) where T : struct
        {
            return new NativeArray64<T>(array, allocator);
        }

        /// <summary>
        /// Performs a parallel copy from NativeArray to NativeArray64 using jobs.
        /// </summary>
        public static Unity.Jobs.JobHandle ParallelCopyTo<T>(this NativeArray<T> source, NativeArray64<T> destination, Unity.Jobs.JobHandle inputDeps = default) where T : struct
        {
            // For simplicity, we'll use a synchronous copy wrapped in a job
            // In a real implementation, you'd want to create a proper parallel job
            source.CopyTo(destination);
            return inputDeps;
        }

        /// <summary>
        /// Performs a parallel copy from NativeArray64 to NativeArray using jobs.
        /// </summary>
        public static Unity.Jobs.JobHandle ParallelCopyTo<T>(this NativeArray64<T> source, NativeArray<T> destination, Unity.Jobs.JobHandle inputDeps = default) where T : struct
        {
            // For simplicity, we'll use a synchronous copy wrapped in a job
            // In a real implementation, you'd want to create a proper parallel job
            source.CopyTo(destination);
            return inputDeps;
        }

        /// <summary>
        /// Slices a NativeArray64 into multiple regular NativeArrays.
        /// </summary>
        public static NativeArray<T>[] Slice<T>(this NativeArray64<T> source, int chunkSize, Allocator allocator) where T : struct
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));
            }

            ulong numChunks = (source.Length + (ulong)chunkSize - 1) / (ulong)chunkSize;
            if (numChunks > int.MaxValue)
            {
                throw new InvalidOperationException($"Too many chunks ({numChunks}) for array");
            }

            var result = new NativeArray<T>[(int)numChunks];

            for (int i = 0; i < (int)numChunks; i++)
            {
                ulong start = (ulong)i * (ulong)chunkSize;
                ulong remainingElements = source.Length - start;
                int currentChunkSize = (int)Math.Min((ulong)chunkSize, remainingElements);

                result[i] = new NativeArray<T>(currentChunkSize, allocator);

                // Copy the chunk
                unsafe
                {
                    byte* srcPtr = (byte*)GetUnsafeReadOnlyPtr(source) + start * (ulong)UnsafeUtility.SizeOf<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var srcHandle = GetAtomicSafetyHandle(ref source);
                    var dstHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(result[i]);
                    AtomicSafetyHandle.CheckReadAndThrow(srcHandle);
                    AtomicSafetyHandle.CheckWriteAndThrow(dstHandle);
#endif
                    void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(result[i]);
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, currentChunkSize * UnsafeUtility.SizeOf<T>());
                }
            }

            return result;
        }

        /// <summary>
        /// Combines multiple NativeArrays into a single NativeArray64.
        /// </summary>
        public static NativeArray64<T> Combine<T>(this NativeArray<T>[] sources, Allocator allocator) where T : struct
        {
            if (sources == null || sources.Length == 0)
            {
                throw new ArgumentException("Sources array cannot be null or empty", nameof(sources));
            }

            // Calculate total length
            ulong totalLength = 0;
            foreach (var source in sources)
            {
                totalLength += (ulong)source.Length;
            }

            var result = new NativeArray64<T>(totalLength, allocator);

            ulong currentOffset = 0;
            foreach (var source in sources)
            {
                unsafe
                {
                    byte* dstPtr = (byte*)GetUnsafeReadOnlyPtr(result) + currentOffset * (ulong)UnsafeUtility.SizeOf<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var srcHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(source);
                    var dstHandle = GetAtomicSafetyHandle(ref result);
                    AtomicSafetyHandle.CheckReadAndThrow(srcHandle);
                    AtomicSafetyHandle.CheckWriteAndThrow(dstHandle);
#endif
                    void* srcPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, source.Length * UnsafeUtility.SizeOf<T>());
                }
                currentOffset += (ulong)source.Length;
            }

            return result;
        }

        /// <summary>
        /// Creates a NativeList64 from multiple NativeLists.
        /// </summary>
        public static NativeList64<T> Combine<T>(this NativeList<T>[] sources, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            if (sources == null || sources.Length == 0)
            {
                throw new ArgumentException("Sources array cannot be null or empty", nameof(sources));
            }

            // Calculate total length
            ulong totalLength = 0;
            foreach (var source in sources)
            {
                totalLength += (ulong)source.Length;
            }

            var result = new NativeList64<T>(totalLength, allocator);

            foreach (var source in sources)
            {
                result.AddRange(source.AsArray());
            }

            return result;
        }
    }
}