using GeoToolkit.Collections.LowLevel.Unsafe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace GeoToolkit.Collections
{
    /// <summary>
    /// An indexable collection that supports 64-bit indexing.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    public interface IIndexable64<T> where T : unmanaged
    {
        /// <summary>
        /// The current number of elements in the collection.
        /// </summary>
        ulong Length { get; set; }

        /// <summary>
        /// Returns a reference to the element at a given index.
        /// </summary>
        ref T ElementAt(ulong index);
    }

    /// <summary>
    /// A resizable list that supports 64-bit indexing.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    public interface INativeList64<T> : IIndexable64<T> where T : unmanaged
    {
        /// <summary>
        /// The number of elements that fit in the current allocation.
        /// </summary>
        ulong Capacity { get; set; }

        /// <summary>
        /// Whether this list is empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// The element at an index.
        /// </summary>
        T this[ulong index] { get; set; }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// An unmanaged, resizable list that supports 64-bit indexing.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Length = {m_ListData == null ? default : m_ListData->Length}, Capacity = {m_ListData == null ? default : m_ListData->Capacity}")]
    [DebuggerTypeProxy(typeof(NativeList64DebugView<>))]
    public unsafe struct NativeList64<T>
        : INativeDisposable
        , INativeList64<T>
        , IEnumerable<T>
        where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal int m_SafetyIndexHint;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeList64<T>>();
#endif

        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList64<T>* m_ListData;

        /// <summary>
        /// Initializes and returns a NativeList64 with a capacity of one.
        /// </summary>
        public NativeList64(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator)
        {
        }

        /// <summary>
        /// Initializes and returns a NativeList64.
        /// </summary>
        public NativeList64(ulong initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp);
        }

        /// <summary>
        /// Initializes and returns a NativeList64 (32-bit compatibility).
        /// </summary>
        public NativeList64(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
            : this((ulong)initialCapacity, allocator)
        {
        }

        internal void Initialize<U>(ulong initialCapacity, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
            var totalSize = (ulong)sizeof(T) * initialCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);
            CheckTotalSize(initialCapacity, totalSize);

            m_Safety = AtomicSafetyHandle.Create();
            InitStaticSafetyId(ref m_Safety);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            var allocHandle = allocator.Handle;
            m_ListData = UnsafeList64<T>.Create(initialCapacity, ref allocHandle, NativeArrayOptions.UninitializedMemory);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [BurstDiscard]
        private static void InitStaticSafetyId(ref AtomicSafetyHandle handle)
        {
            if (s_staticSafetyId.Data == 0)
            {
                s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<NativeList64<T>>();
            }

            AtomicSafetyHandle.SetStaticSafetyId(ref handle, s_staticSafetyId.Data);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckAllocator(AllocatorManager.AllocatorHandle allocator)
        {
            if (allocator.ToAllocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }
        }
#endif

        /// <summary>
        /// The element at a given index.
        /// </summary>
        public T this[ulong index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return (*m_ListData)[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                (*m_ListData)[index] = value;
            }
        }

        /// <summary>
        /// The element at a given index (32-bit compatibility).
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[(ulong)index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[(ulong)index] = value;
        }

        /// <summary>
        /// Returns a reference to the element at an index.
        /// </summary>
        public ref T ElementAt(ulong index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return ref m_ListData->ElementAt(index);
        }

        /// <summary>
        /// Returns a reference to the element at an index (32-bit compatibility).
        /// </summary>
        public ref T ElementAt(int index)
        {
            return ref ElementAt((ulong)index);
        }

        /// <summary>
        /// The count of elements.
        /// </summary>
        public ulong Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_ListData->Length;
            }

            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && UNITY_2022_2_16F1_OR_NEWER
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                m_ListData->Resize(value, NativeArrayOptions.ClearMemory);
            }
        }

        /// <summary>
        /// The number of elements that fit in the current allocation.
        /// </summary>
        public ulong Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_ListData->Capacity;
            }

            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                m_ListData->Capacity = value;
            }
        }

        /// <summary>
        /// Returns the internal unsafe list.
        /// </summary>
        public UnsafeList64<T>* GetUnsafeList() => m_ListData;

        /// <summary>
        /// Appends an element to the end of this list.
        /// </summary>
        public void AddNoResize(T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_ListData->AddNoResize(value);
        }

        /// <summary>
        /// Appends elements from a buffer to the end of this list.
        /// </summary>
        public void AddRangeNoResize(void* ptr, ulong count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_ListData->AddRangeNoResize(ptr, count);
        }

        /// <summary>
        /// Appends elements from a buffer to the end of this list (32-bit compatibility).
        /// </summary>
        public void AddRangeNoResize(void* ptr, int count)
        {
            AddRangeNoResize(ptr, (ulong)count);
        }

        /// <summary>
        /// Appends the elements of another list to the end of this list.
        /// </summary>
        public void AddRangeNoResize(NativeList64<T> list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_ListData->AddRangeNoResize(*list.m_ListData);
        }

        /// <summary>
        /// Appends an element to the end of this list.
        /// </summary>
        public void Add(in T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->Add(in value);
        }

        /// <summary>
        /// Appends the elements of an array to the end of this list.
        /// </summary>
        public void AddRange(NativeArray<T> array)
        {
            AddRange(array.GetUnsafeReadOnlyPtr(), (ulong)array.Length);
        }

        /// <summary>
        /// Appends the elements of an array to the end of this list.
        /// </summary>
        public void AddRange(NativeArray64<T> array)
        {
            unsafe
            {
                AddRange(array.m_Buffer, array.Length);
            }
        }

        /// <summary>
        /// Appends the elements of a buffer to the end of this list.
        /// </summary>
        public void AddRange(void* ptr, ulong count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->AddRange(ptr, count);
        }

        /// <summary>
        /// Appends the elements of a buffer to the end of this list (32-bit compatibility).
        /// </summary>
        public void AddRange(void* ptr, int count)
        {
            AddRange(ptr, (ulong)count);
        }

        /// <summary>
        /// Appends value count times to the end of this list.
        /// </summary>
        public void AddReplicate(in T value, ulong count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->AddReplicate(in value, count);
        }

        /// <summary>
        /// Appends value count times to the end of this list (32-bit compatibility).
        /// </summary>
        public void AddReplicate(in T value, int count)
        {
            AddReplicate(in value, (ulong)count);
        }

        /// <summary>
        /// Shifts elements toward the end of this list, increasing its length.
        /// </summary>
        public void InsertRangeWithBeginEnd(ulong begin, ulong end)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->InsertRangeWithBeginEnd(begin, end);
        }

        /// <summary>
        /// Shifts elements toward the end of this list, increasing its length.
        /// </summary>
        public void InsertRange(ulong index, ulong count)
        {
            InsertRangeWithBeginEnd(index, index + count);
        }

        /// <summary>
        /// Shifts elements toward the end of this list, increasing its length (32-bit compatibility).
        /// </summary>
        public void InsertRange(int index, int count)
        {
            InsertRange((ulong)index, (ulong)count);
        }

        /// <summary>
        /// Copies the last element of this list to the specified index. Decrements the length by 1.
        /// </summary>
        public void RemoveAtSwapBack(ulong index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveAtSwapBack(index);
        }

        /// <summary>
        /// Copies the last element of this list to the specified index (32-bit compatibility).
        /// </summary>
        public void RemoveAtSwapBack(int index)
        {
            RemoveAtSwapBack((ulong)index);
        }

        /// <summary>
        /// Copies the last N elements of this list to a range in this list.
        /// </summary>
        public void RemoveRangeSwapBack(ulong index, ulong count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRangeSwapBack(index, count);
        }

        /// <summary>
        /// Copies the last N elements of this list to a range in this list (32-bit compatibility).
        /// </summary>
        public void RemoveRangeSwapBack(int index, int count)
        {
            RemoveRangeSwapBack((ulong)index, (ulong)count);
        }

        /// <summary>
        /// Removes the element at an index, shifting everything above it down by one.
        /// </summary>
        public void RemoveAt(ulong index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveAt(index);
        }

        /// <summary>
        /// Removes the element at an index (32-bit compatibility).
        /// </summary>
        public void RemoveAt(int index)
        {
            RemoveAt((ulong)index);
        }

        /// <summary>
        /// Removes N elements in a range, shifting everything above the range down by N.
        /// </summary>
        public void RemoveRange(ulong index, ulong count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRange(index, count);
        }

        /// <summary>
        /// Removes N elements in a range (32-bit compatibility).
        /// </summary>
        public void RemoveRange(int index, int count)
        {
            RemoveRange((ulong)index, (ulong)count);
        }

        /// <summary>
        /// Whether this list is empty.
        /// </summary>
        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_ListData == null || m_ListData->Length == 0;
        }

        /// <summary>
        /// Whether this list has been allocated.
        /// </summary>
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_ListData != null;
        }

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            }
#endif
            if (!IsCreated)
            {
                return;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            UnsafeList64<T>.Destroy(m_ListData);
            m_ListData = null;
        }

        /// <summary>
        /// Creates and schedules a job that releases all resources.
        /// </summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            }
#endif
            if (!IsCreated)
            {
                return inputDeps;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var jobHandle = new NativeList64DisposeJob { Data = new NativeList64Dispose { m_ListData = (void*)m_ListData, m_Safety = m_Safety } }.Schedule(inputDeps);
            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new NativeList64DisposeJob { Data = new NativeList64Dispose { m_ListData = (void*)m_ListData } }.Schedule(inputDeps);
#endif
            m_ListData = null;

            return jobHandle;
        }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->Clear();
        }

        /// <summary>
        /// Returns a native array that aliases the content of this list.
        /// </summary>
        public NativeArray64<T> AsArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
            NativeArray64<T> array = default(NativeArray64<T>);
            array.m_Buffer = m_ListData->Ptr;
            array.m_Length = m_ListData->Length;
            array.m_AllocatorLabel = Allocator.None;
            array.m_MinIndex = 0;
            array.m_MaxIndex = m_ListData->Length - 1;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_Safety = arraySafety;
#endif
            return array;
        }

        /// <summary>
        /// Converts to a regular NativeArray (requires length to fit in int.MaxValue).
        /// </summary>
        public NativeArray<T> ToNativeArray(AllocatorManager.AllocatorHandle allocator)
        {
            if (Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot convert NativeList64 with length {Length} to NativeArray (max size: {int.MaxValue})");
            }

            NativeArray<T> result = new NativeArray<T>((int)Length, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            var resultHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(result);
            AtomicSafetyHandle.CheckWriteAndThrow(resultHandle);
#endif
            unsafe
            {
                void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(dstPtr, (byte*)m_ListData->Ptr, (int)Length * UnsafeUtility.SizeOf<T>());
            }
            return result;
        }

        /// <summary>
        /// Returns an array containing a copy of this list's content.
        /// </summary>
        public NativeArray64<T> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            NativeArray64<T> result = new NativeArray64<T>(Length, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(result.m_Safety);
#endif
            unsafe
            {
                ulong sizeInBytes = Length * (ulong)UnsafeUtility.SizeOf<T>();
                if (sizeInBytes > long.MaxValue)
                {
                    // For very large copies, do it in chunks
                    ulong remaining = sizeInBytes;
                    ulong chunkSize = (ulong)int.MaxValue;
                    ulong currentOffset = 0;

                    while (remaining > 0)
                    {
                        ulong toCopy = Math.Min(remaining, chunkSize);
                        UnsafeUtility.MemCpy((byte*)result.m_Buffer + currentOffset, (byte*)m_ListData->Ptr + currentOffset, (long)toCopy);
                        remaining -= toCopy;
                        currentOffset += toCopy;
                    }
                }
                else
                {
                    UnsafeUtility.MemCpy((byte*)result.m_Buffer, (byte*)m_ListData->Ptr, (long)sizeInBytes);
                }
            }
            return result;
        }

        /// <summary>
        /// Copies all elements of specified container to this container.
        /// </summary>
        public void CopyFrom(in NativeArray<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            var otherHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(other);
            AtomicSafetyHandle.CheckReadAndThrow(otherHandle);
#endif
            m_ListData->CopyFrom(other);
        }

        /// <summary>
        /// Copies all elements of specified container to this container.
        /// </summary>
        public void CopyFrom(in NativeArray64<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(other.m_Safety);
#endif
            m_ListData->CopyFrom(other);
        }

        /// <summary>
        /// Copies all elements of specified container to this container.
        /// </summary>
        public void CopyFrom(in UnsafeList64<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->CopyFrom(other);
        }

        /// <summary>
        /// Copies all elements of specified container to this container.
        /// </summary>
        public void CopyFrom(in NativeList64<T> other)
        {
            CopyFrom(*other.m_ListData);
        }

        /// <summary>
        /// Returns an enumerator over the elements of this list.
        /// </summary>
        public NativeArray64<T>.Enumerator GetEnumerator()
        {
            var array = AsArray();
            return new NativeArray64<T>.Enumerator(ref array);
        }

        /// <summary>
        /// This method is not implemented. Use GetEnumerator() instead.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented. Use GetEnumerator() instead.
        /// </summary>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary.
        /// </summary>
        public void Resize(ulong length, NativeArrayOptions options)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->Resize(length, options);
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary (32-bit compatibility).
        /// </summary>
        public void Resize(int length, NativeArrayOptions options)
        {
            Resize((ulong)length, options);
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary.
        /// </summary>
        public void ResizeUninitialized(ulong length)
        {
            Resize(length, NativeArrayOptions.UninitializedMemory);
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary (32-bit compatibility).
        /// </summary>
        public void ResizeUninitialized(int length)
        {
            ResizeUninitialized((ulong)length);
        }

        /// <summary>
        /// Sets the capacity.
        /// </summary>
        public void SetCapacity(ulong capacity)
        {
            m_ListData->SetCapacity(capacity);
        }

        /// <summary>
        /// Sets the capacity (32-bit compatibility).
        /// </summary>
        public void SetCapacity(int capacity)
        {
            SetCapacity((ulong)capacity);
        }

        /// <summary>
        /// Sets the capacity to match the length.
        /// </summary>
        public void TrimExcess()
        {
            m_ListData->TrimExcess();
        }

        /// <summary>
        /// Returns a read only of this list.
        /// </summary>
        public NativeArray64<T>.ReadOnly AsReadOnly()
        {
            return new NativeArray64<T>.ReadOnly(m_ListData->Ptr, m_ListData->Length
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                , ref m_Safety
#endif
            );
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckInitialCapacity(ulong initialCapacity)
        {
            // No upper limit check for ulong capacity
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckTotalSize(ulong initialCapacity, ulong totalSize)
        {
            if (totalSize > long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {long.MaxValue} bytes");
        }
    }

    [NativeContainer]
    internal unsafe struct NativeList64Dispose
    {
        [NativeDisableUnsafePtrRestriction]
        public void* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public void Dispose()
        {
            UnsafeList64<int>.Destroy((UnsafeList64<int>*)m_ListData);
        }
    }

    [BurstCompile]
    internal unsafe struct NativeList64DisposeJob : IJob
    {
        internal NativeList64Dispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }

    sealed unsafe class NativeList64DebugView<T> where T : unmanaged
    {
        UnsafeList64<T>* Data;

        public NativeList64DebugView(NativeList64<T> array)
        {
            Data = array.m_ListData;
        }

        public T[] Items
        {
            get
            {
                if (Data == null)
                {
                    return default;
                }

                // Only show first 1000 elements in debugger to avoid performance issues
                ulong count = Math.Min(Data->Length, 1000);
                var dst = new T[count];

                for (ulong i = 0; i < count; i++)
                {
                    dst[i] = Data->Ptr[i];
                }

                return dst;
            }
        }
    }

    /// <summary>
    /// Provides extension methods for NativeList64.
    /// </summary>
    public unsafe static class NativeList64Extensions
    {
        /// <summary>
        /// Returns true if a particular value is present in this list.
        /// </summary>
        public static bool Contains<T, U>(this NativeList64<T> list, U value)
            where T : unmanaged, IEquatable<U>
        {
            return IndexOf<T, U>(list, value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in this list.
        /// </summary>
        public static long IndexOf<T, U>(this NativeList64<T> list, U value)
            where T : unmanaged, IEquatable<U>
        {
            for (ulong i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(value))
                    return (long)i;
            }
            return -1;
        }

        /// <summary>
        /// Returns a pointer to this list's internal buffer.
        /// </summary>
        public static T* GetUnsafePtr<T>(this NativeList64<T> list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

        /// <summary>
        /// Returns a pointer to this list's internal buffer.
        /// </summary>
        public static T* GetUnsafeReadOnlyPtr<T>(this NativeList64<T> list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Returns this list's AtomicSafetyHandle.
        /// </summary>
        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(ref NativeList64<T> list) where T : unmanaged
        {
            return list.m_Safety;
        }
#endif

        /// <summary>
        /// Returns a pointer to this list's internal unsafe list.
        /// </summary>
        public static void* GetInternalListDataPtrUnchecked<T>(ref NativeList64<T> list) where T : unmanaged
        {
            return list.m_ListData;
        }
    }
}