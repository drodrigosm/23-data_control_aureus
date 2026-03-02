// PageCache.cs
// Cache LRU sencilla para páginas. Evita releer páginas recientes al hacer scroll.

using System;
using System.Collections.Generic;

namespace AureusControl.Core.Services
{
    public sealed class PageCache<T>
    {
        private readonly int _capacity;
        private readonly Dictionary<int, T> _data = new();
        private readonly LinkedList<int> _lru = new();

        public PageCache(int capacity)
        {
            _capacity = Math.Max(2, capacity);
        }

        public bool TryGet(int pageIndex, out T value)
        {
            if (_data.TryGetValue(pageIndex, out value))
            {
                Touch(pageIndex);
                return true;
            }
            return false;
        }

        public void Set(int pageIndex, T value)
        {
            if (_data.ContainsKey(pageIndex))
            {
                _data[pageIndex] = value;
                Touch(pageIndex);
                return;
            }

            _data[pageIndex] = value;
            _lru.AddFirst(pageIndex);

            if (_data.Count > _capacity)
            {
                var last = _lru.Last;
                if (last != null)
                {
                    var toRemove = last.Value;
                    _lru.RemoveLast();
                    _data.Remove(toRemove);
                }
            }
        }

        private void Touch(int pageIndex)
        {
            var node = _lru.Find(pageIndex);
            if (node != null) _lru.Remove(node);
            _lru.AddFirst(pageIndex);
        }

        public void Clear()
        {
            _data.Clear();
            _lru.Clear();
        }
    }
}