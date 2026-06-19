using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GeoToolkit.Collections
{
    /// <summary>
    /// A native array that supports 64-bit indexing for ultra-large data storage.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the array.</typeparam>
    [NativeContainer]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [DebuggerTypeProxy(typeof(NativeArray64DebugView<>))]
    [DebuggerDisplay("Length = {m_Length}")]
    public struct NativeArray64<T> : IDisposable, IEnumerable<T>, IEnumerable where T : struct
    {
        /// <summary>
        /// Represents a NativeArray64 interface constrained to read-only operations.
        /// </summary>
        [NativeContainerIsReadOnly]
        [DebuggerTypeProxy(typeof(NativeArray64ReadOnlyDebugView<>))]
        [NativeContainer]
        [DebuggerDisplay("Length = {Length}")]
        public struct ReadOnly : IEnumerable<T>, IEnumerable
        {
            [NativeDisableUnsafePtrRestriction]
            internal unsafe void* m_Buffer;

            internal ulong m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public ulong Length
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Length;
                }
            }

            public unsafe T this[ulong index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    CheckElementReadAccess(index);
                    return UnsafeUtility.ReadArrayElement<T>(m_Buffer, (int)(index % int.MaxValue));
                }
            }

            public unsafe T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return this[(ulong)index];
                }
            }

            public unsafe bool IsCreated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Buffer != null;
                }
            }

            internal unsafe ReadOnly(void* buffer, ulong length
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                , ref AtomicSafetyHandle safety
#endif
            )
            {
                m_Buffer = buffer;
                m_Length = length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = safety;
#endif
            }

            public void CopyTo(T[] array)
            {
                NativeArray64<T>.Copy(this, array);
            }

            public void CopyTo(NativeArray64<T> array)
            {
                NativeArray64<T>.Copy(this, array);
            }

            public T[] ToArray()
            {
                if (m_Length > int.MaxValue)
                {
                    throw new InvalidOperationException($"Cannot convert ReadOnly with length {m_Length} to managed array (max size: {int.MaxValue})");
                }
                T[] array = new T[m_Length];
                NativeArray64<T>.Copy(this, array, m_Length);
                return array;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckElementReadAccess(ulong index)
            {
                if (index >= m_Length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is out of range (must be between 0 and {m_Length - 1}).");
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            }

            public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
            {
                private ReadOnly m_Array;
                private ulong m_Index;
                private T value;

                public T Current
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return value;
                    }
                }

                object IEnumerator.Current => Current;

                public Enumerator(in ReadOnly array)
                {
                    m_Array = array;
                    m_Index = ulong.MaxValue;
                    value = default(T);
                }

                public void Dispose()
                {
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public unsafe bool MoveNext()
                {
                    m_Index++;
                    if (m_Index < m_Array.m_Length)
                    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        AtomicSafetyHandle.CheckReadAndThrow(m_Array.m_Safety);
#endif
                        value = m_Array[m_Index];
                        return true;
                    }

                    value = default(T);
                    return false;
                }

                public void Reset()
                {
                    m_Index = ulong.MaxValue;
                }
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(in this);
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [NativeDisableUnsafePtrRestriction]
        internal unsafe void* m_Buffer;

        internal ulong m_Length;

        internal ulong m_MinIndex;

        internal ulong m_MaxIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        internal Allocator m_AllocatorLabel;

        private static int s_staticSafetyId;

        /// <summary>
        /// The length of the array (64-bit).
        /// </summary>
        public ulong Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_Length;
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index (64-bit).
        /// </summary>
        public unsafe T this[ulong index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, (int)(index % int.MaxValue));
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckElementWriteAccess(index);
                UnsafeUtility.WriteArrayElement(m_Buffer, (int)(index % int.MaxValue), value);
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index (32-bit compatibility).
        /// </summary>
        public unsafe T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this[(ulong)index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this[(ulong)index] = value;
            }
        }

        /// <summary>
        /// Whether this array has been created.
        /// </summary>
        public unsafe bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_Buffer != null;
            }
        }

        /// <summary>
        /// Creates a new NativeArray64.
        /// </summary>
        public unsafe NativeArray64(ulong length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(m_Buffer, (long)Length * (long)UnsafeUtility.SizeOf<T>());
            }
        }

        /// <summary>
        /// Creates a new NativeArray64 (32-bit compatibility constructor).
        /// </summary>
        public unsafe NativeArray64(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            : this((ulong)length, allocator, options)
        {
        }

        /// <summary>
        /// Creates a NativeArray64 from a managed array.
        /// </summary>
        public NativeArray64(T[] array, Allocator allocator)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            Allocate((ulong)array.Length, allocator, out this);
            Copy(array, this);
        }

        /// <summary>
        /// Creates a NativeArray64 from another NativeArray64.
        /// </summary>
        public NativeArray64(NativeArray64<T> array, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(array.m_Safety);
#endif
            Allocate(array.Length, allocator, out this);
            Copy(array, 0, this, 0, array.Length);
        }

        /// <summary>
        /// Creates a NativeArray64 from a regular NativeArray.
        /// </summary>
        public NativeArray64(NativeArray<T> array, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var handle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
            AtomicSafetyHandle.CheckReadAndThrow(handle);
#endif
            Allocate((ulong)array.Length, allocator, out this);
            CopyFromNativeArray(array, this);
        }

        [BurstDiscard]
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static void InitStaticSafetyId(ref AtomicSafetyHandle handle)
        {
            if (s_staticSafetyId == 0)
            {
                s_staticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<NativeArray64<T>>();
            }

            AtomicSafetyHandle.SetStaticSafetyId(ref handle, s_staticSafetyId);
        }
#endif

        private unsafe static void Allocate(ulong length, Allocator allocator, out NativeArray64<T> array)
        {
            CheckAllocateArguments(length, allocator);

            // For very large allocations, we need to be careful about the size calculation
            ulong totalSize = (ulong)UnsafeUtility.SizeOf<T>() * length;
            if (totalSize > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException("length", $"Total size ({totalSize} bytes) exceeds maximum allocatable size ({long.MaxValue} bytes)");
            }

            array = default(NativeArray64<T>);
            IsUnmanagedAndThrow();

            // Note: UnsafeUtility.MallocTracked still uses long for size, not ulong
            array.m_Buffer = UnsafeUtility.MallocTracked((long)totalSize, UnsafeUtility.AlignOf<T>(), allocator, 0);
            array.m_Length = length;
            array.m_AllocatorLabel = allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_Safety = AtomicSafetyHandle.Create();
            InitStaticSafetyId(ref array.m_Safety);
            InitNestedNativeContainer(array.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckAllocateArguments(ulong length, Allocator allocator)
        {
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }

            if (allocator >= Allocator.FirstUserIndex)
            {
                throw new ArgumentException("Use CollectionHelper.CreateNativeArray in com.unity.collections package for custom allocator", "allocator");
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal static void InitNestedNativeContainer(AtomicSafetyHandle handle)
        {
            if (UnsafeUtility.IsNativeContainerType<T>())
            {
                AtomicSafetyHandle.SetNestedContainer(handle, isNestedContainer: true);
            }
        }
#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [BurstDiscard]
        internal static void IsUnmanagedAndThrow()
        {
            if (!UnsafeUtility.IsUnmanaged<T>())
            {
                throw new InvalidOperationException($"{typeof(T)} used in NativeArray64<{typeof(T)}> must be unmanaged (contain no managed types).");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElementReadAccess(ulong index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
            {
                FailOutOfRangeError(index);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElementWriteAccess(ulong index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
            {
                FailOutOfRangeError(index);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void FailOutOfRangeError(ulong index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
            {
                throw new IndexOutOfRangeException($"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.");
            }

            throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
        }

        /// <summary>
        /// Disposes of this array and deallocates its memory.
        /// </summary>
        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_AllocatorLabel != Allocator.None && !AtomicSafetyHandle.IsDefaultValue(in m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(in m_Safety);
            }
#endif

            if (IsCreated)
            {
                if (m_AllocatorLabel == Allocator.Invalid)
                {
                    throw new InvalidOperationException("The NativeArray64 can not be Disposed because it was not allocated with a valid allocator.");
                }

                if (m_AllocatorLabel >= Allocator.FirstUserIndex)
                {
                    throw new InvalidOperationException("The NativeArray64 can not be Disposed because it was allocated with a custom allocator.");
                }

                if (m_AllocatorLabel > Allocator.None)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.Release(m_Safety);
#endif
                    UnsafeUtility.FreeTracked(m_Buffer, m_AllocatorLabel);
                    m_AllocatorLabel = Allocator.Invalid;
                }

                m_Buffer = null;
            }
        }

        /// <summary>
        /// Copies data from a source array to this array.
        /// </summary>
        public void CopyFrom(T[] array)
        {
            Copy(array, this);
        }

        /// <summary>
        /// Copies data from another NativeArray64 to this array.
        /// </summary>
        public void CopyFrom(NativeArray64<T> array)
        {
            Copy(array, this);
        }

        /// <summary>
        /// Copies data from a regular NativeArray to this array.
        /// </summary>
        public void CopyFrom(NativeArray<T> array)
        {
            CopyFromNativeArray(array, this);
        }

        /// <summary>
        /// Gets the unsafe pointer to the array data.
        /// WARNING: Use with caution! No safety checks are performed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetUnsafePtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return m_Buffer;
        }

        /// <summary>
        /// Gets the unsafe read-only pointer to the array data.
        /// WARNING: Use with caution! No safety checks are performed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetUnsafeReadOnlyPtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return m_Buffer;
        }

        /// <summary>
        /// Copies data from this array to a managed array.
        /// </summary>
        public void CopyTo(T[] array)
        {
            Copy(this, array);
        }

        /// <summary>
        /// Copies data from this array to another NativeArray64.
        /// </summary>
        public void CopyTo(NativeArray64<T> array)
        {
            Copy(this, array);
        }

        /// <summary>
        /// Converts this array to a managed array.
        /// </summary>
        public T[] ToArray()
        {
            if (Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot convert NativeArray64 with length {Length} to managed array (max size: {int.MaxValue})");
            }
            T[] array = new T[Length];
            Copy(this, array);
            return array;
        }

        /// <summary>
        /// Returns a sub-array of this array.
        /// </summary>
        public unsafe NativeArray64<T> GetSubArray(ulong start, ulong length)
        {
            CheckGetSubArrayArguments(start, length);

            // Calculate the offset in bytes
            ulong offsetBytes = (ulong)UnsafeUtility.SizeOf<T>() * start;
            byte* offsetBuffer = (byte*)m_Buffer + offsetBytes;

            NativeArray64<T> subArray = default(NativeArray64<T>);
            subArray.m_Buffer = offsetBuffer;
            subArray.m_Length = length;
            subArray.m_AllocatorLabel = Allocator.None;
            subArray.m_MinIndex = 0;
            subArray.m_MaxIndex = length - 1;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            subArray.m_Safety = m_Safety;
#endif

            return subArray;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckGetSubArrayArguments(ulong start, ulong length)
        {
            if (start + length > Length)
            {
                throw new ArgumentOutOfRangeException("length", $"Sub array range {start}-{start + length - 1} is outside the range of the native array 0-{Length - 1}");
            }
        }

        /// <summary>
        /// Returns a read-only version of this array.
        /// </summary>
        public unsafe ReadOnly AsReadOnly()
        {
            return new ReadOnly(m_Buffer, m_Length
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                , ref m_Safety
#endif
            );
        }

        // Copy methods
        public static void Copy(NativeArray64<T> src, NativeArray64<T> dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            CopySafe(src, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, NativeArray64<T> dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            CopySafe(src, 0, dst, 0, src.Length);
        }

        public static void Copy(T[] src, NativeArray64<T> dst)
        {
            CheckCopyLengths((ulong)src.Length, dst.Length);
            CopySafe(src, 0, dst, 0, (ulong)src.Length);
        }

        public static void Copy(NativeArray64<T> src, T[] dst)
        {
            CheckCopyLengths(src.Length, (ulong)dst.Length);
            CopySafe(src, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, T[] dst)
        {
            CheckCopyLengths(src.Length, (ulong)dst.Length);
            CopySafe(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray64<T> src, ulong srcIndex, NativeArray64<T> dst, ulong dstIndex, ulong length)
        {
            CopySafe(src, srcIndex, dst, dstIndex, length);
        }

        public static void Copy(ReadOnly src, T[] dst, ulong length)
        {
            CopySafe(src, 0, dst, 0, length);
        }

        public static void Copy(ReadOnly src, NativeArray64<T> dst, ulong length)
        {
            CopySafe(src, 0, dst, 0, length);
        }

        // Compatibility with regular NativeArray
        public static void CopyFromNativeArray(NativeArray<T> src, NativeArray64<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var srcHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(src);
            AtomicSafetyHandle.CheckReadAndThrow(srcHandle);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif

            unsafe
            {
                ulong length = (ulong)src.Length;
                if (length > dst.Length)
                {
                    throw new ArgumentException("Source array is larger than destination");
                }

                void* srcPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src);
                UnsafeUtility.MemCpy(dst.m_Buffer, srcPtr, src.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        public NativeArray<T> ToNativeArray(Allocator allocator)
        {
            if (Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Cannot convert NativeArray64 with length {Length} to NativeArray (max size: {int.MaxValue})");
            }

            NativeArray<T> result = new NativeArray<T>((int)Length, allocator);
            unsafe
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                var resultHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(result);
                AtomicSafetyHandle.CheckWriteAndThrow(resultHandle);
#endif
                void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(dstPtr, m_Buffer, (int)Length * UnsafeUtility.SizeOf<T>());
            }
            return result;
        }

        private unsafe static void CopySafe(NativeArray64<T> src, ulong srcIndex, NativeArray64<T> dst, ulong dstIndex, ulong length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);

            // Calculate offsets
            ulong srcOffsetBytes = srcIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong dstOffsetBytes = dstIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong copySize = length * (ulong)UnsafeUtility.SizeOf<T>();

            if (copySize > long.MaxValue)
            {
                // For very large copies, we need to do it in chunks
                ulong remaining = copySize;
                ulong chunkSize = (ulong)int.MaxValue;
                ulong currentSrcOffset = srcOffsetBytes;
                ulong currentDstOffset = dstOffsetBytes;

                while (remaining > 0)
                {
                    ulong toCopy = Math.Min(remaining, chunkSize);
                    UnsafeUtility.MemCpy((byte*)dst.m_Buffer + currentDstOffset,
                                       (byte*)src.m_Buffer + currentSrcOffset,
                                       (long)toCopy);
                    remaining -= toCopy;
                    currentSrcOffset += toCopy;
                    currentDstOffset += toCopy;
                }
            }
            else
            {
                UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstOffsetBytes,
                                   (byte*)src.m_Buffer + srcOffsetBytes,
                                   (long)copySize);
            }
        }

        private unsafe static void CopySafe(T[] src, ulong srcIndex, NativeArray64<T> dst, ulong dstIndex, ulong length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            CheckCopyPtr(src);
            CheckCopyArguments((ulong)src.Length, srcIndex, dst.Length, dstIndex, length);

            GCHandle gCHandle = GCHandle.Alloc(src, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();

            ulong srcOffsetBytes = srcIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong dstOffsetBytes = dstIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong copySize = length * (ulong)UnsafeUtility.SizeOf<T>();

            UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstOffsetBytes,
                               (byte*)(void*)intPtr + srcOffsetBytes,
                               (long)copySize);
            gCHandle.Free();
        }

        private unsafe static void CopySafe(NativeArray64<T> src, ulong srcIndex, T[] dst, ulong dstIndex, ulong length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
#endif
            CheckCopyPtr(dst);
            CheckCopyArguments(src.Length, srcIndex, (ulong)dst.Length, dstIndex, length);

            GCHandle gCHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();

            ulong srcOffsetBytes = srcIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong dstOffsetBytes = dstIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong copySize = length * (ulong)UnsafeUtility.SizeOf<T>();

            UnsafeUtility.MemCpy((byte*)(void*)intPtr + dstOffsetBytes,
                               (byte*)src.m_Buffer + srcOffsetBytes,
                               (long)copySize);
            gCHandle.Free();
        }

        private unsafe static void CopySafe(ReadOnly src, ulong srcIndex, NativeArray64<T> dst, ulong dstIndex, ulong length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);

            ulong srcOffsetBytes = srcIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong dstOffsetBytes = dstIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong copySize = length * (ulong)UnsafeUtility.SizeOf<T>();

            if (copySize > long.MaxValue)
            {
                // For very large copies, we need to do it in chunks
                ulong remaining = copySize;
                ulong chunkSize = (ulong)int.MaxValue;
                ulong currentSrcOffset = srcOffsetBytes;
                ulong currentDstOffset = dstOffsetBytes;

                while (remaining > 0)
                {
                    ulong toCopy = Math.Min(remaining, chunkSize);
                    UnsafeUtility.MemCpy((byte*)dst.m_Buffer + currentDstOffset,
                                       (byte*)src.m_Buffer + currentSrcOffset,
                                       (long)toCopy);
                    remaining -= toCopy;
                    currentSrcOffset += toCopy;
                    currentDstOffset += toCopy;
                }
            }
            else
            {
                UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstOffsetBytes,
                                   (byte*)src.m_Buffer + srcOffsetBytes,
                                   (long)copySize);
            }
        }

        private unsafe static void CopySafe(ReadOnly src, ulong srcIndex, T[] dst, ulong dstIndex, ulong length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
#endif
            CheckCopyPtr(dst);
            CheckCopyArguments(src.Length, srcIndex, (ulong)dst.Length, dstIndex, length);

            GCHandle gCHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();

            ulong srcOffsetBytes = srcIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong dstOffsetBytes = dstIndex * (ulong)UnsafeUtility.SizeOf<T>();
            ulong copySize = length * (ulong)UnsafeUtility.SizeOf<T>();

            UnsafeUtility.MemCpy((byte*)(void*)intPtr + dstOffsetBytes,
                               (byte*)src.m_Buffer + srcOffsetBytes,
                               (long)copySize);
            gCHandle.Free();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyPtr(T[] ptr)
        {
            if (ptr == null)
            {
                throw new ArgumentNullException("ptr");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyLengths(ulong srcLength, ulong dstLength)
        {
            if (srcLength != dstLength)
            {
                throw new ArgumentException("Source and destination length must be the same");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyArguments(ulong srcLength, ulong srcIndex, ulong dstLength, ulong dstIndex, ulong length)
        {
            if (srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
            {
                throw new ArgumentOutOfRangeException("srcIndex", "srcIndex is outside the range of valid indexes for the source NativeArray64.");
            }

            if (dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
            {
                throw new ArgumentOutOfRangeException("dstIndex", "dstIndex is outside the range of valid indexes for the destination NativeArray64.");
            }

            if (srcIndex + length > srcLength)
            {
                throw new ArgumentException("Length is greater than the number of elements from srcIndex to the end of the source NativeArray64.", "length");
            }

            if (dstIndex + length > dstLength)
            {
                throw new ArgumentException("Length is greater than the number of elements from dstIndex to the end of the destination NativeArray64.", "length");
            }
        }

        // Enumerator support
        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private NativeArray64<T> m_Array;
            private ulong m_Index;
            private T value;

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return value; }
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Current; }
            }

            public Enumerator(ref NativeArray64<T> array)
            {
                m_Array = array;
                m_Index = ulong.MaxValue; // Will wrap to 0 on first MoveNext
                value = default(T);
            }

            public void Dispose() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool MoveNext()
            {
                m_Index++;
                if (m_Index < m_Array.m_Length)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Array.m_Safety);
#endif
                    value = m_Array[m_Index];
                    return true;
                }

                value = default(T);
                return false;
            }

            public void Reset()
            {
                m_Index = ulong.MaxValue;
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public unsafe bool Equals(NativeArray64<T> other)
        {
            return m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is NativeArray64<T> && Equals((NativeArray64<T>)obj);
        }

        public unsafe override int GetHashCode()
        {
            return ((int)m_Buffer * 397) ^ m_Length.GetHashCode();
        }

        public static bool operator ==(NativeArray64<T> left, NativeArray64<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativeArray64<T> left, NativeArray64<T> right)
        {
            return !left.Equals(right);
        }
    }

    // Debug view for the debugger
    sealed unsafe class NativeArray64DebugView<T> where T : struct
    {
        private NativeArray64<T> m_Array;

        public NativeArray64DebugView(NativeArray64<T> array)
        {
            m_Array = array;
        }

        public T[] Items
        {
            get
            {
                if (!m_Array.IsCreated)
                {
                    return null;
                }

                // Only show first 1000 elements in debugger to avoid performance issues
                ulong count = Math.Min(m_Array.Length, 1000);
                T[] result = new T[count];
                for (ulong i = 0; i < count; i++)
                {
                    result[i] = m_Array[i];
                }
                return result;
            }
        }
    }

    // Debug view for ReadOnly
    sealed unsafe class NativeArray64ReadOnlyDebugView<T> where T : struct
    {
        private NativeArray64<T>.ReadOnly m_Array;

        public NativeArray64ReadOnlyDebugView(NativeArray64<T>.ReadOnly array)
        {
            m_Array = array;
        }

        public T[] Items
        {
            get
            {
                if (!m_Array.IsCreated)
                {
                    return null;
                }

                // Only show first 1000 elements in debugger to avoid performance issues
                ulong count = Math.Min(m_Array.Length, 1000);
                T[] result = new T[count];
                for (ulong i = 0; i < count; i++)
                {
                    result[i] = m_Array[i];
                }
                return result;
            }
        }
    }
}