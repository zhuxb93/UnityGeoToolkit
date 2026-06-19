#define POOL_DOWNLOAD_HANDLERS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

#if WINDOWS_UWP
using System.Threading.Tasks;
#endif

namespace Unity3DTiles
{
    public class ObjectPool<T> where T : class, new()
    {
        List<WeakReference<T>> pool = new List<WeakReference<T>>();

        public T Acquire()
        {
            T obj = null;
            foreach (var wr in pool)
            {
                if (wr.TryGetTarget(out obj))
                {
                    wr.SetTarget(null);
                    break;
                }
            }
            if (obj == null)
            {
                obj = new T();
            }
            if (obj is PooledObject<T>)
            {
                (obj as PooledObject<T>).Reinit(this);
            }
            return obj;
        }

        public void Return(T obj)
        {
            foreach (var wr in pool)
            {
                if (!wr.TryGetTarget(out T _))
                {
                    wr.SetTarget(obj);
                    return;
                }
            }
            pool.Add(new WeakReference<T>(obj));
        }

        public int PoolSize()
        {
            return pool.Count;
        }
    }

    public interface PooledObject<T> where T : class, new()
    {
        void Reinit(ObjectPool<T> pool);
    }

    public class PooledMemoryStream : MemoryStream, PooledObject<PooledMemoryStream>
    {
        private ObjectPool<PooledMemoryStream> pool;
        private bool returned;

        public PooledMemoryStream() : base(32768)
        {
        }

        public override void Close()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pool != null)
                {
                    if (!returned)
                    {
                        returned = true;
                        pool.Return(this);
                        //Debug.Log("memory stream pool size " + pool.PoolSize() + ", returned stream size " + Length);
                    }
                }
                else
                {
                    base.Dispose(true);
                }
            }
            else
            {
                base.Dispose(false);
            }
        }

        public void Reinit(ObjectPool<PooledMemoryStream> pool)
        {
            this.pool = pool;
            returned = false;
            Position = 0;
            SetLength(0);
        }
    }

    public class PooledBuffer
    {
        public byte[] Buffer = new byte[32768];
    }

    public class DownloadHandlerPooled : DownloadHandlerScript
    {
        public MemoryStream Stream { get; private set; }

        public DownloadHandlerPooled(MemoryStream stream, byte[] buf) : base(buf)
        {
            Stream = stream;
        }

        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            ulong cap = (ulong)(Stream.Capacity);
            if (cap < contentLength)
            {
                cap = 32768 * (ulong)Math.Ceiling(contentLength / 32768.0);
                if (cap > int.MaxValue)
                {
                    throw new OverflowException();
                }
            }
            if (Stream.Capacity < (int)cap)
            {
                Stream.Capacity = (int)cap;
            }
        }

        protected override bool ReceiveData(byte[] data, int len)
        {
            Stream.Write(data, 0, len);
            return true;
        }

        protected override void CompleteContent()
        {
            Stream.Position = 0;
        }

        protected override byte[] GetData()
        {
            throw new NotImplementedException();
        }

        protected override string GetText()
        {
            throw new NotImplementedException();
        }

        protected override float GetProgress()
        {
            throw new NotImplementedException();
        }
    }
}
