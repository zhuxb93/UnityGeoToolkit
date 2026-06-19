using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GeoToolkit.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// An unsafe list that supports 64-bit indexing for ultra-large data storage.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the list.</typeparam>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}")]
    [DebuggerTypeProxy(typeof(UnsafeList64DebugView<>))]
    public unsafe struct UnsafeList64<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        public T* Ptr;

        public ulong m_length;
        public ulong m_capacity;
        public AllocatorManager.AllocatorHandle Allocator;

        /// <summary>
        /// The number of elements in the list.
        /// </summary>
        public ulong Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_length;
            set
            {
                if (value > m_capacity)
                {
                    Resize(value);
                }
                else
                {
                    m_length = value;
                }
            }
        }

        /// <summary>
        /// The capacity of the list.
        /// </summary>
        public ulong Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_capacity;
            set => SetCapacity(value);
        }

        /// <summary>
        /// The element at the specified index.
        /// </summary>
        public T this[ulong index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckIndexInRange(index);
                return Ptr[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckIndexInRange(index);
                Ptr[index] = value;
            }
        }

        /// <summary>
        /// The element at the specified index (32-bit compatibility).
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
            CheckIndexInRange(index);
            return ref Ptr[index];
        }

        /// <summary>
        /// Creates a new UnsafeList64.
        /// </summary>
        public static UnsafeList64<T>* Create(ulong capacity, ref AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            UnsafeList64<T>* listData = (UnsafeList64<T>*)allocator.Allocate(sizeof(UnsafeList64<T>), UnsafeUtility.AlignOf<UnsafeList64<T>>(), 1);

            listData->Allocator = allocator;
            listData->m_length = 0;
            listData->m_capacity = 0;
            listData->Ptr = null;

            if (capacity != 0)
            {
                listData->SetCapacity(capacity);
            }

            if (options == NativeArrayOptions.ClearMemory && listData->Ptr != null && capacity > 0)
            {
                UnsafeUtility.MemClear(listData->Ptr, (long)(capacity * (ulong)sizeof(T)));
            }

            return listData;
        }

        /// <summary>
        /// Destroys an UnsafeList64.
        /// </summary>
        public static void Destroy(UnsafeList64<T>* listData)
        {
            if (listData == null)
                return;

            var allocator = listData->Allocator;
            listData->Dispose();
            AllocatorManager.Free(allocator, listData);
        }

        /// <summary>
        /// Destroys an UnsafeList64 using the provided allocator.
        /// </summary>
        public static void Destroy<U>(UnsafeList64<T>* listData, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
            if (listData == null)
                return;

            listData->Dispose();
            AllocatorManager.Free(allocator.Handle, listData);
        }

        /// <summary>
        /// Disposes of this list.
        /// </summary>
        public void Dispose()
        {
            if (Ptr != null && IsAllocatorValid())
            {
                AllocatorManager.Free(Allocator, Ptr);
                Ptr = null;
                m_capacity = 0;
                m_length = 0;
            }
        }

        private bool IsAllocatorValid()
        {
            return Allocator.Index != 0;
        }

        /// <summary>
        /// Sets the capacity.
        /// </summary>
        public void SetCapacity(ulong capacity)
        {
            if (capacity == m_capacity)
                return;

            if (capacity < m_length)
                throw new ArgumentOutOfRangeException($"Cannot set capacity {capacity} less than length {m_length}");

            Realloc(capacity);
        }

        /// <summary>
        /// Resizes the list.
        /// </summary>
        public void Resize(ulong length, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            if (length > m_capacity)
            {
                SetCapacityWithGrow(length);
            }

            ulong oldLength = m_length;
            m_length = length;

            if (options == NativeArrayOptions.ClearMemory && length > oldLength && Ptr != null)
            {
                UnsafeUtility.MemClear(Ptr + oldLength, (long)((length - oldLength) * (ulong)sizeof(T)));
            }
        }

        /// <summary>
        /// Clears the list.
        /// </summary>
        public void Clear()
        {
            m_length = 0;
        }

        /// <summary>
        /// Adds an element to the end of the list.
        /// </summary>
        public void Add(in T value)
        {
            if (m_length >= m_capacity)
            {
                SetCapacityWithGrow(m_length + 1);
            }

            Ptr[m_length++] = value;
        }

        /// <summary>
        /// Adds an element to the end of the list without resizing.
        /// </summary>
        public void AddNoResize(T value)
        {
            if (m_length >= m_capacity)
                throw new InvalidOperationException($"AddNoResize assumes that list capacity is sufficient (capacity: {m_capacity}, length: {m_length})!");

            Ptr[m_length++] = value;
        }

        /// <summary>
        /// Adds a range of elements to the end of the list.
        /// </summary>
        public void AddRange(void* ptr, ulong count)
        {
            if (count == 0)
                return;

            ulong newLength = m_length + count;
            if (newLength > m_capacity)
            {
                SetCapacityWithGrow(newLength);
            }

            ulong sizeInBytes = count * (ulong)sizeof(T);
            if (sizeInBytes > long.MaxValue)
            {
                // For very large copies, do it in chunks
                ulong remaining = sizeInBytes;
                ulong chunkSize = (ulong)int.MaxValue;
                ulong currentOffset = 0;

                while (remaining > 0)
                {
                    ulong toCopy = Math.Min(remaining, chunkSize);
                    UnsafeUtility.MemCpy(Ptr + m_length, (byte*)ptr + currentOffset, (long)toCopy);
                    remaining -= toCopy;
                    currentOffset += toCopy;
                }
            }
            else
            {
                UnsafeUtility.MemCpy(Ptr + m_length, ptr, (long)sizeInBytes);
            }

            m_length = newLength;
        }

        /// <summary>
        /// Adds a range of elements to the end of the list without resizing.
        /// </summary>
        public void AddRangeNoResize(void* ptr, ulong count)
        {
            if (m_length + count > m_capacity)
                throw new InvalidOperationException($"AddRangeNoResize assumes that list capacity is sufficient!");

            if (count == 0)
                return;

            ulong sizeInBytes = count * (ulong)sizeof(T);
            UnsafeUtility.MemCpy(Ptr + m_length, ptr, (long)sizeInBytes);
            m_length += count;
        }

        /// <summary>
        /// Adds a range from another UnsafeList64.
        /// </summary>
        public void AddRange(UnsafeList64<T> other)
        {
            AddRange(other.Ptr, other.Length);
        }

        /// <summary>
        /// Adds a range from another UnsafeList64 without resizing.
        /// </summary>
        public void AddRangeNoResize(UnsafeList64<T> other)
        {
            AddRangeNoResize(other.Ptr, other.Length);
        }

        /// <summary>
        /// Adds replicated values to the end of the list.
        /// </summary>
        public void AddReplicate(in T value, ulong count)
        {
            if (count == 0)
                return;

            ulong newLength = m_length + count;
            if (newLength > m_capacity)
            {
                SetCapacityWithGrow(newLength);
            }

            for (ulong i = 0; i < count; i++)
            {
                Ptr[m_length + i] = value;
            }

            m_length = newLength;
        }

        /// <summary>
        /// Removes an element at the specified index.
        /// </summary>
        public void RemoveAt(ulong index)
        {
            CheckIndexInRange(index);

            if (index < m_length - 1)
            {
                // Move elements after index down by one
                ulong elementsToMove = m_length - index - 1;
                ulong sizeInBytes = elementsToMove * (ulong)sizeof(T);
                UnsafeUtility.MemCpy(Ptr + index, Ptr + index + 1, (long)sizeInBytes);
            }

            m_length--;
        }

        /// <summary>
        /// Removes an element at the specified index by swapping it with the last element.
        /// </summary>
        public void RemoveAtSwapBack(ulong index)
        {
            CheckIndexInRange(index);

            if (index < m_length - 1)
            {
                Ptr[index] = Ptr[m_length - 1];
            }

            m_length--;
        }

        /// <summary>
        /// Removes a range of elements.
        /// </summary>
        public void RemoveRange(ulong index, ulong count)
        {
            if (count == 0)
                return;

            CheckIndexAndCount(index, count);

            if (index + count < m_length)
            {
                // Move elements after the range down
                ulong elementsToMove = m_length - (index + count);
                ulong sizeInBytes = elementsToMove * (ulong)sizeof(T);
                UnsafeUtility.MemCpy(Ptr + index, Ptr + index + count, (long)sizeInBytes);
            }

            m_length -= count;
        }

        /// <summary>
        /// Removes a range of elements by swapping with elements from the end.
        /// </summary>
        public void RemoveRangeSwapBack(ulong index, ulong count)
        {
            if (count == 0)
                return;

            CheckIndexAndCount(index, count);

            ulong copyFrom = Math.Max(index + count, m_length - count);
            ulong copyCount = m_length - copyFrom;

            if (copyCount > 0)
            {
                ulong sizeInBytes = copyCount * (ulong)sizeof(T);
                UnsafeUtility.MemCpy(Ptr + index, Ptr + copyFrom, (long)sizeInBytes);
            }

            m_length -= count;
        }

        /// <summary>
        /// Inserts a range at the specified index.
        /// </summary>
        public void InsertRange(ulong index, ulong count)
        {
            if (count == 0)
                return;

            if (index > m_length)
                throw new ArgumentOutOfRangeException($"Index {index} is out of range (0..{m_length})");

            ulong newLength = m_length + count;
            if (newLength > m_capacity)
            {
                SetCapacityWithGrow(newLength);
            }

            if (index < m_length)
            {
                // Move elements at and after index up by count
                ulong elementsToMove = m_length - index;
                ulong sizeInBytes = elementsToMove * (ulong)sizeof(T);
                UnsafeUtility.MemCpy(Ptr + index + count, Ptr + index, (long)sizeInBytes);
            }

            m_length = newLength;
        }

        /// <summary>
        /// Inserts a range at the specified index.
        /// </summary>
        public void InsertRangeWithBeginEnd(ulong begin, ulong end)
        {
            if (end < begin)
                throw new ArgumentException($"End {end} must be >= begin {begin}");

            InsertRange(begin, end - begin);
        }

        /// <summary>
        /// Trims excess capacity.
        /// </summary>
        public void TrimExcess()
        {
            if (m_length < m_capacity)
            {
                SetCapacity(m_length);
            }
        }

        /// <summary>
        /// Copies data from a NativeArray.
        /// </summary>
        public void CopyFrom(in NativeArray<T> array)
        {
            Resize((ulong)array.Length, NativeArrayOptions.UninitializedMemory);
            unsafe
            {
                void* srcPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
                UnsafeUtility.MemCpy(Ptr, srcPtr, array.Length * sizeof(T));
            }
        }

        /// <summary>
        /// Copies data from a NativeArray64.
        /// </summary>
        public void CopyFrom(in NativeArray64<T> array)
        {
            Resize(array.Length, NativeArrayOptions.UninitializedMemory);
            unsafe
            {
                ulong sizeInBytes = array.Length * (ulong)sizeof(T);
                if (sizeInBytes > long.MaxValue)
                {
                    // For very large copies, do it in chunks
                    ulong remaining = sizeInBytes;
                    ulong chunkSize = (ulong)int.MaxValue;
                    ulong currentOffset = 0;

                    while (remaining > 0)
                    {
                        ulong toCopy = Math.Min(remaining, chunkSize);
                        UnsafeUtility.MemCpy((byte*)Ptr + currentOffset, (byte*)array.m_Buffer + currentOffset, (long)toCopy);
                        remaining -= toCopy;
                        currentOffset += toCopy;
                    }
                }
                else
                {
                    UnsafeUtility.MemCpy(Ptr, array.m_Buffer, (long)sizeInBytes);
                }
            }
        }

        /// <summary>
        /// Copies data from another UnsafeList64.
        /// </summary>
        public void CopyFrom(in UnsafeList64<T> other)
        {
            Resize(other.Length, NativeArrayOptions.UninitializedMemory);
            if (other.Length > 0)
            {
                ulong sizeInBytes = other.Length * (ulong)sizeof(T);
                if (sizeInBytes > long.MaxValue)
                {
                    // For very large copies, do it in chunks
                    ulong remaining = sizeInBytes;
                    ulong chunkSize = (ulong)int.MaxValue;
                    ulong currentOffset = 0;

                    while (remaining > 0)
                    {
                        ulong toCopy = Math.Min(remaining, chunkSize);
                        UnsafeUtility.MemCpy((byte*)Ptr + currentOffset, (byte*)other.Ptr + currentOffset, (long)toCopy);
                        remaining -= toCopy;
                        currentOffset += toCopy;
                    }
                }
                else
                {
                    UnsafeUtility.MemCpy(Ptr, other.Ptr, (long)sizeInBytes);
                }
            }
        }

        private void Realloc(ulong capacity)
        {
            ulong newSizeInBytes = capacity * (ulong)sizeof(T);

            // Check if the allocation size is too large
            if (newSizeInBytes > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Cannot allocate {newSizeInBytes} bytes (max: {long.MaxValue})");
            }

            T* newPtr = null;
            if (capacity > 0)
            {
                newPtr = (T*)AllocatorManager.Allocate(Allocator, sizeof(T), UnsafeUtility.AlignOf<T>(), (int)capacity);
            }

            if (m_length > 0 && Ptr != null && newPtr != null)
            {
                ulong copySize = Math.Min(m_length, capacity) * (ulong)sizeof(T);
                if (copySize > long.MaxValue)
                {
                    // For very large copies, do it in chunks
                    ulong remaining = copySize;
                    ulong chunkSize = (ulong)int.MaxValue;
                    ulong currentOffset = 0;

                    while (remaining > 0)
                    {
                        ulong toCopy = Math.Min(remaining, chunkSize);
                        UnsafeUtility.MemCpy((byte*)newPtr + currentOffset, (byte*)Ptr + currentOffset, (long)toCopy);
                        remaining -= toCopy;
                        currentOffset += toCopy;
                    }
                }
                else
                {
                    UnsafeUtility.MemCpy(newPtr, Ptr, (long)copySize);
                }
            }

            if (Ptr != null)
            {
                AllocatorManager.Free(Allocator, Ptr);
            }

            Ptr = newPtr;
            m_capacity = capacity;
            m_length = Math.Min(m_length, capacity);
        }

        private void SetCapacityWithGrow(ulong requestedCapacity)
        {
            // Growth strategy: double the capacity or use requested capacity, whichever is larger
            ulong newCapacity = Math.Max(requestedCapacity, m_capacity * 2);

            // Start with at least 4 elements
            if (newCapacity < 4)
                newCapacity = 4;

            SetCapacity(newCapacity);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexInRange(ulong index)
        {
            if (index >= m_length)
                throw new IndexOutOfRangeException($"Index {index} is out of range (0..{m_length - 1})");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexAndCount(ulong index, ulong count)
        {
            if (index >= m_length)
                throw new IndexOutOfRangeException($"Index {index} is out of range (0..{m_length - 1})");
            if (index + count > m_length)
                throw new ArgumentException($"Range [{index}..{index + count}) is out of range (0..{m_length})");
        }
    }

    // Debug view for the debugger
    sealed unsafe class UnsafeList64DebugView<T> where T : unmanaged
    {
        private UnsafeList64<T>* m_Ptr;

        public UnsafeList64DebugView(UnsafeList64<T>* ptr)
        {
            m_Ptr = ptr;
        }

        public T[] Items
        {
            get
            {
                if (m_Ptr == null || m_Ptr->Ptr == null)
                    return null;

                // Only show first 1000 elements in debugger to avoid performance issues
                ulong count = Math.Min(m_Ptr->Length, 1000);
                T[] result = new T[count];
                for (ulong i = 0; i < count; i++)
                {
                    result[i] = m_Ptr->Ptr[i];
                }
                return result;
            }
        }
    }
}