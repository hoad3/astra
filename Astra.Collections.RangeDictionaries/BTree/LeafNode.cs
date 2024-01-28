using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Astra.Collections.RangeDictionaries.BTree;

public sealed partial class BTreeMap<TKey, TValue> where TKey : INumber<TKey>
{
    [DebuggerDisplay("PrimaryKey = {PrimaryKey}, KeyCount = {KeyCount}, Pairs = {Pairs}")]
    private partial class LeafNode(int degree)
    {
        private readonly int _splitSize = degree / 2;
        private readonly KeyValuePair<TKey, TValue>[] _pairs = new KeyValuePair<TKey, TValue>[degree + 1];
        
        public InternalNode? Parent { get; set; }
        public bool IsInternal => false;
        public bool IsLeaf => true;
        public bool IsOverweight => KeyCount == degree + 1;
        public bool IsFull => KeyCount >= degree;
        public int KeyCount { get; private set; }
        public int Degree => degree;
        private Span<KeyValuePair<TKey, TValue>> Pairs => new(_pairs, 0, KeyCount);

        IEnumerable<TKey> INode.Keys
        {
            get
            {
                for (var i = 0; i < KeyCount; i++)
                {
                    yield return _pairs[i].Key;
                }
            }
        }
        public TKey PrimaryKey => _pairs[0].Key;
        public TKey AutoFirstKey => _pairs[0].Key;
        public int AutoKeyCount => KeyCount;

        IEnumerable<TValue> INode.Values
        {
            get
            {
                for (var i = 0; i < KeyCount; i++)
                {
                    yield return _pairs[i].Value;
                }
            }
        }
        
        public LeafNode(int degree, KeyValuePair<TKey, TValue> pair) : this(degree)
        {
            this[0] = pair;
            KeyCount++;
        }

        private static int BinarySearch(Span<KeyValuePair<TKey, TValue>> span, TKey target)
        {
            var left = 0;
            var right = span.Length - 1;
            while (left <= right)
            {
                var mid = left + (right - left) / 2;
                var comparison = span[mid].Key.CompareTo(target);
                switch (comparison)
                {
                    case 0: // Equal
                        return mid;
                    case -1: // mid < target
                        left = mid + 1;
                        break;
                    case 1: // mid > target
                        right = mid - 1;
                        break;
                }
            }

            return -1;
        }
        
        private static (int index, bool isExact) NearestBinarySearch(Span<KeyValuePair<TKey, TValue>> span, TKey target)
        {
            var left = 0;
            var ceil = span.Length - 1;
            var right = ceil;
            var result = -1;
            while (left <= right)
            {
                var mid = left + (right - left) / 2;
                var comparison = span[mid].Key.CompareTo(target);
                switch (comparison)
                {
                    case 0: // Equal
                        return (mid, true);
                    case -1: // mid < target
                        left = mid + 1;
                        break;
                    case 1: // mid > target
                        right = mid - 1;
                        result = mid;
                        break;
                }
            }

            return (result, false);
        }

        private KeyValuePair<TKey, TValue> this[int index]
        {
            get => _pairs[index];
            set => _pairs[index] = value;
        }
        // The new node is always at the right side
        private LeafNode Split()
        {
            var newNode = new LeafNode(degree)
            {
                KeyCount = _splitSize,
                Parent = Parent,
            };
            var moveIndex = KeyCount - _splitSize;
            for (var i = 0; i < _splitSize; i++)
            {
                newNode[i] = this[moveIndex + i];
            }
            KeyCount -= _splitSize;
            return newNode;
        }
        public InsertionResultPayload Insert(TKey key, TValue value)
        {
            var (index, isExactMatch) = NearestBinarySearch(Pairs, key);
            // if (index == -1) // Empty
            // {
            //     throw new UnreachableException();
            // }

            if (isExactMatch)
            {
                _pairs[index] = new (key, value);
                return new(InsertionResult.NoSizeChange, null);
            }

            var oldFirst = AutoFirstKey;
            if (index == -1)
            {
                index = key.CompareTo(_pairs[0].Key) < 0 ? 0 : KeyCount;
            }
            // if (key.CompareTo(_keys[index]) > 0) 
            //     index++;
            
            for (var i = KeyCount; i > index; i--)
            {
                this[i] = this[i - 1];
            }

            this[index] = new(key, value);
            KeyCount++;
            if (index == 0 && Parent != null && Parent.PrimaryKey.Equals(oldFirst))
                Parent.PrimaryKey = key;
            if (!IsOverweight) return new(InsertionResult.SizeChanged, null);
            var newNode = Split();
            return new(InsertionResult.NodeSplit, newNode);
        }

        public RemovalResultPayload Remove(TKey key)
        {
            var index = BinarySearch(Pairs, key);
            if (index == -1) return new(RemovalResult.NoSizeChange, default);
            var value = _pairs[index].Value;
            var oldFirst = AutoFirstKey;
            for (var i = index; i < KeyCount - 1; i++)
            {
                this[i] = this[i + 1];
            }

            KeyCount--;
            if (KeyCount == 0) return new(RemovalResult.Empty, value);
            if (index == 0 && Parent != null && Parent.PrimaryKey.Equals(oldFirst))
                Parent.PrimaryKey = AutoFirstKey;
            return new(RemovalResult.SizeChanged, value);
        }

        public bool CanMergeWith<T>(T node) where T : INode
        {
            return node.IsLeaf && KeyCount >= node.KeyCount && KeyCount + node.KeyCount <= degree;
        }

        // left = node
        public void LeftMergeWith(INode node)
        {
            var target = (LeafNode)node;
            var oldFirst = AutoFirstKey;
            var targetSize = target.KeyCount;
            var oldSize = KeyCount;
            var newSize = oldSize + targetSize;
            _pairs.ShiftElements(0, oldSize - 1, targetSize);
            for (var i = 0; i < targetSize; i++)
            {
                this[i] = target[i];
            }

            KeyCount = newSize;
            if (Parent != null && Parent.PrimaryKey.Equals(oldFirst))
                Parent.PrimaryKey = AutoFirstKey;
        }

        // right = node
        public void RightMergeWith(INode node)
        {
            var target = (LeafNode)node;
            var targetSize = target.KeyCount;
            var oldSize = KeyCount;
            for (var i = 0; i < targetSize; i++)
            {
                this[oldSize + i] = target[i];
            }

            KeyCount = oldSize + targetSize;
        }

        public bool Contains(TKey key)
        {
            return BinarySearch(Pairs, key) != -1;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var index = BinarySearch(Pairs, key);
            if (index == -1)
            {
                value = default;
                return false;
            }

            value = _pairs[index].Value;
            return true;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < KeyCount; i++)
                yield return _pairs[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> Collect(TKey leftBound, TKey rightBound, CollectionMode mode)
        {
            switch (mode)
            {
                case CollectionMode.ClosedInterval:
                {
                    var (index, _) = NearestBinarySearch(Pairs, leftBound);
                    if (index == -1)
                    {
                        index = leftBound.CompareTo(_pairs[0].Key) < 0 ? 0 : KeyCount;
                    }
                    while (index < KeyCount && _pairs[index].Key.CompareTo(rightBound) <= 0)
                    {
                        if (_pairs[index].Key.CompareTo(leftBound) >= 0)
                            yield return this[index];
                        index++;
                    }
                    break;
                }
                case CollectionMode.HalfClosedLeftInterval:
                // {
                //     break;
                // }
                case CollectionMode.HalfClosedRightInterval:
                // {
                //     break;
                // }
                case CollectionMode.OpenInterval:
                // {
                //     break;
                // }
                case CollectionMode.UnboundedClosedInterval:
                // {
                //     break;
                // }
                case CollectionMode.UnboundedHalfClosedLeftInterval:
                case CollectionMode.UnboundedHalfClosedRightInterval:
                case CollectionMode.UnboundedOpenInterval:
                {
                    throw new NotSupportedException();
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}