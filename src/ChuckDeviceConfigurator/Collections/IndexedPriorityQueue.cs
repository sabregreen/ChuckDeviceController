﻿namespace ChuckDeviceConfigurator.Collections
{
    // Credits: http://davidknopp.net/code-samples/indexed-priority-queue-c-unity/
    public sealed class IndexedPriorityQueue<T> where T : IComparable
    {
        #region Variables

        private List<T> _objects;
        private int[] _heap;
        private int[] _heapInverse;
        private int _count;

        #endregion

        #region Properties

        public int Count => _count;

        public T this[int index]
        {
            get
            {
                if (index > _objects.Count || index < 0)
                {
                    throw new IndexOutOfRangeException($"IndexedPriorityQueue.[]: Index '{index}' out of range");
                }
                return _objects[index];
            }
            set
            {
                if (index > _objects.Count || index < 0)
                {
                    throw new IndexOutOfRangeException($"IndexedPriorityQueue.[]: Index '{index}' out of range");
                }
                Set(index, value);
            }
        }

        public IReadOnlyList<T> Values => _objects;

        #endregion

        #region Constructor

        public IndexedPriorityQueue(int maxSize)
        {
            // Note: Does the same as below variable instantiations but removes warning
            Resize(maxSize);

            _objects = new List<T>(maxSize);
            _heap = new int[maxSize + 1];
            _heapInverse = new int[maxSize];
            _count = 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inserts a new value with the given index
        /// </summary>
        /// <param name="index">index to insert at</param>
        /// <param name="value">value to insert</param>
        public void Insert(int index, T value)
        {
            if (index > _objects.Count || index < 0)
            {
                throw new IndexOutOfRangeException($"IndexedPriorityQueue.Insert: Index '{index}' out of range");
            }

            if (index < _objects.Count && index > _count) // TODO: Use OR instead
            {
                // TODO: Fix index out of range error
                index = _count;
            }

            ++_count;

            if (_objects.Contains(value))
            {
                // update index
                Set(index, value);
            }
            else
            {
                // add object
                _objects.Insert(index, value);
            }

            // add to heap
            _heapInverse[index] = _count;
            _heap[_count] = index;

            // update heap
            SortHeapUpward(_count);
        }

        /// <summary>
        /// Gets the top element of the queue
        /// </summary>
        /// <returns>The top element</returns>
        public T Top()
        {
            // top of heap [first element is 1, not 0]
            return _objects[_heap[1]];
        }

        /// <summary>
        /// Removes the top element from the queue
        /// </summary>
        /// <returns>The removed element</returns>
        public T Pop()
        {
            if (_count == 0)
            {
                return default(T);
            }

            // swap front to back for removal
            Swap(1, _count--);

            // re-sort heap
            SortHeapDownward(1);

            // return popped object
            return _objects[_heap[_count + 1]];
        }

        /// <summary>
        /// Removes the last element from the queue
        /// </summary>
        /// <returns>The removed element</returns>
        public T PopLast()
        {
            if (_count == 0)
            {
                return default(T);
            }

            // swap front to back for removal
            Swap(1, _count--);

            var last = _objects[_heap[_count]];

            // re-sort heap
            SortHeapUpward(_count);

            // return popped object
            return _objects[_heap[_count + 1]];
        }

        /// <summary>
        /// Returns <c>true</c> if the element already exists in the queue,
        /// otherwise returns <c>false</c>.
        /// </summary>
        /// <param name="obj">value to compare</param>
        /// <returns>Returns true if it exists, otherwise false.</returns>
        public bool Contains(T obj) => _objects.Contains(obj);

        /// <summary>
        /// Removes an element from the queue based on the index
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            // TODO: Potentially need to lock _objects
            if (index >= 0 && index <= _objects.Count)
            {
                _objects.RemoveAt(index);
            }
            SortHeapDownward(1);
        }

        /// <summary>
        /// Gets the first index of an element in the queue
        /// </summary>
        /// <param name="obj">value to search for</param>
        /// <returns></returns>
        public int IndexOf(T obj) => _objects.IndexOf(obj);

        /// <summary>
        /// Updates the value at the given index. Note that this function is not
        /// as efficient as the DecreaseIndex/IncreaseIndex methods, but is
        /// best when the value at the index is not known
        /// </summary>
        /// <param name="index">index of the value to set</param>
        /// <param name="obj">new value</param>
        public void Set(int index, T obj)
        {
            if (obj.CompareTo(_objects[index]) <= 0)
            {
                DecreaseIndex(index, obj);
            }
            else
            {
                IncreaseIndex(index, obj);
            }
        }

        /// <summary>
        /// Decrease the value at the current index
        /// </summary>
        /// <param name="index">index to decrease value of</param>
        /// <param name="obj">new value</param>
        public void DecreaseIndex(int index, T obj)
        {
            if (index > _objects.Count || index < 0)
            {
                throw new IndexOutOfRangeException($"IndexedPriorityQueue.DecreaseIndex: Index '{index}' out of range");
            }
            if (obj.CompareTo(_objects[index]) != 0)
            {
                throw new IndexOutOfRangeException($"IndexedPriorityQueue.DecreaseIndex: object '{obj}' isn't less than current value '{_objects[index]}'");
            }

            _objects[index] = obj;
            SortUpward(index);
        }

        /// <summary>
        /// Increase the value at the current index
        /// </summary>
        /// <param name="index">index to increase value of</param>
        /// <param name="obj">new value</param>
        public void IncreaseIndex(int index, T obj)
        {
            if (index > _objects.Count || index < 0)
            {
                throw new IndexOutOfRangeException($"IndexedPriorityQueue.IncreaseIndex: Index '{index}' out of range");
            }
            if (obj.CompareTo(_objects[index]) != 0)
            {
                throw new IndexOutOfRangeException($"IndexedPriorityQueue.IncreaseIndex: object '{obj}' isn't greater than current value '{_objects[index]}'");
            }

            _objects[index] = obj;
            SortDownward(index);
        }

        /// <summary>
        /// Clears all items in the priority queue
        /// </summary>
        public void Clear()
        {
            _objects.Clear();
            _count = _objects.Count;
        }

        /// <summary>
        /// Set the maximum capacity of the queue
        /// </summary>
        /// <param name="maxSize">new maximum capacity</param>
        public void Resize(int maxSize)
        {
            if (maxSize < 0)
            {
                throw new ArgumentException($"IndexedPriorityQueue.Resize: Invalid size '{maxSize}'");
            }

            _objects = new List<T>(maxSize);
            _heap = new int[maxSize + 1];
            _heapInverse = new int[maxSize];
            _count = 0;
        }

        #endregion

        #region Private Methods

        private void SortUpward(int index)
        {
            SortHeapUpward(_heapInverse[index]);
        }

        private void SortDownward(int index)
        {
            SortHeapDownward(_heapInverse[index]);
        }

        private void SortHeapUpward(int heapIndex)
        {
            // move toward top if better than parent
            while (heapIndex > 1 &&
                    _objects[_heap[heapIndex]].CompareTo(_objects[_heap[Parent(heapIndex)]]) < 0)
            {
                // swap this node with its parent
                Swap(heapIndex, Parent(heapIndex));

                // reset iterator to be at parents old position
                // (child's new position)
                heapIndex = Parent(heapIndex);
            }
        }

        private void SortHeapDownward(int heapIndex)
        {
            // move node downward if less than children
            while (FirstChild(heapIndex) <= _count)
            {
                int child = FirstChild(heapIndex);

                // find smallest of two children (if 2 exist)
                if (child < _count &&
                     _objects[_heap[child + 1]].CompareTo(_objects[_heap[child]]) < 0)
                {
                    ++child;
                }

                // swap with child if less
                if (_objects[_heap[child]].CompareTo(_objects[_heap[heapIndex]]) < 0)
                {
                    Swap(child, heapIndex);
                    heapIndex = child;
                }
                // no swap necessary
                else
                {
                    break;
                }
            }
        }

        private void Swap(int i, int j)
        {
            // swap elements in heap
            (_heap[j], _heap[i]) = (_heap[i], _heap[j]);

            // reset inverses
            _heapInverse[_heap[i]] = i;
            _heapInverse[_heap[j]] = j;
        }

        private static int Parent(int heapIndex)
        {
            return heapIndex / 2;
        }

        private static int FirstChild(int heapIndex)
        {
            return heapIndex * 2;
        }

        private static int SecondChild(int heapIndex)
        {
            return FirstChild(heapIndex) + 1;
        }

        #endregion
    }
}