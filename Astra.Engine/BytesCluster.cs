using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Astra.Engine;

public readonly struct BytesCluster : IReadOnlyCollection<byte>, IDisposable
{
    private readonly byte[] _raw;

    public Span<byte> Writer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _raw;
    }
    public ReadOnlySpan<byte> Reader 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _raw;
    }

    public Memory<byte> WriterMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _raw;
    }
    public ReadOnlyMemory<byte> ReaderMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _raw;
    }
    public long LongLength => _raw.LongLength;

    private BytesCluster(byte[] raw) => _raw = raw;

    public static BytesCluster Empty => Rent(0);
    
    /// <summary>
    /// It's rentin' time!
    /// </summary>
    /// <param name="size">The size of the byte cluster to rent.</param>
    /// <returns>A <see cref="BytesCluster"/> representing the rented byte cluster.</returns>
    /// <remarks>
    /// This method rents a byte cluster from the shared <see cref="ArrayPool{T}"/> with the specified <paramref name="size"/>.
    /// The returned <see cref="BytesCluster"/> should be returned to the pool using the appropriate methods to avoid memory leaks.
    /// </remarks>
    public static BytesCluster Rent(int size)
    {
        var arr = ArrayPool<byte>.Shared.Rent(size);
        return new(arr);
    }
    
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_raw);
    }

    public IEnumerator<byte> GetEnumerator()
    {
        return ((IEnumerable<byte>)_raw).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _raw.GetEnumerator();
    }

    public int Count => _raw.Length;

    public void CopyTo(Span<byte> target) => _raw.CopyTo(target);

    public BytesClusterStream Promote() => BytesClusterStream.Consume(this);
}

public class BytesClusterStream : Stream
{
    private readonly BytesCluster _cluster;
    private long _pos;
    
    private BytesClusterStream(BytesCluster cluster) => _cluster = cluster;

    public static BytesClusterStream Consume(BytesCluster cluster) => new(cluster); 
    public static BytesClusterStream Rent(int size) => new(BytesCluster.Rent(size));
    
    public override void Flush()
    {
        
    }

    public override void Close()
    {
        _cluster.Dispose();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = offset + count;
        read = read >= _cluster.LongLength ? (int)_cluster.LongLength : read; 
        _cluster.Reader[(int)_pos..((int)_pos + read)].CopyTo(new Span<byte>(buffer, offset, count));
        _pos += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPos = offset;
        switch (origin)
        {
            case SeekOrigin.Begin:
                break;
            case SeekOrigin.Current:
                newPos += _pos;
                break;
            case SeekOrigin.End:
                newPos = _cluster.LongLength - newPos;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        if (newPos < 0 || newPos >= _cluster.LongLength) throw new IndexOutOfRangeException();
        _pos = newPos;
        return _pos;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var write = offset + count;
        if (write >= _cluster.LongLength) throw new EndOfStreamException();
        new ReadOnlySpan<byte>(buffer, offset, count)
            .CopyTo(_cluster.Writer[(int)_pos..((int)_pos + write)]);
        _pos += write;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => _cluster.LongLength;
    public override long Position
    {
        get => _pos;
        set => _pos = value;
    }
}
