using System.Runtime.CompilerServices;
using System.Text;

namespace Astra.Common;

public static class StreamWriteAddons
{
    public static void WriteValue(this Stream writer, bool value)
    {
        Span<byte> buffer = stackalloc byte[1] { unchecked((byte)(value ? 1 : 0)) };
        writer.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteValueInternal(this Stream writer, void* ptr, int size)
    {
        writer.Write(new ReadOnlySpan<byte>(ptr, size));  
    }
    
    public static void WriteValue(this Stream writer, int value)
    {
        // writer.Write(BitConverter.GetBytes(value));
        unsafe
        {
            writer.WriteValueInternal(&value, sizeof(int));
        }
    }
    
    public static ValueTask WriteValueAsync(this Stream writer, int value, CancellationToken token = default)
    {
        return writer.WriteAsync(BitConverter.GetBytes(value), token);
    }
    
    public static void WriteValue(this Stream writer, uint value)
    {
        // writer.Write(BitConverter.GetBytes(value));
        unsafe
        {
            writer.WriteValueInternal(&value, sizeof(uint));
        }
    }
    
    public static ValueTask WriteValueAsync(this Stream writer, uint value, CancellationToken token = default)
    {
        return writer.WriteAsync(BitConverter.GetBytes(value), token);
    }
    
    public static void WriteValue(this Stream writer, double value)
    {
        // writer.Write(BitConverter.GetBytes(value));
        unsafe
        {
            writer.WriteValueInternal(&value, sizeof(double));
        }
    }
    
    public static void WriteValue(this Stream writer, long value)
    {
        // writer.Write(BitConverter.GetBytes(value));
        unsafe
        {
            writer.WriteValueInternal(&value, sizeof(long));
        }
    }
    
    public static ValueTask WriteValueAsync(this Stream writer, long value, CancellationToken token = default)
    {
        return writer.WriteAsync(BitConverter.GetBytes(value), token);
    }
    
    public static ValueTask WriteValueAsync(this Stream writer, ulong value, CancellationToken token = default)
    {
        return writer.WriteAsync(BitConverter.GetBytes(value), token);
    }
    
    public static void WriteValue(this Stream writer, ulong value)
    {
        unsafe
        {
            writer.WriteValueInternal(&value, sizeof(ulong));
        }
    }

    private static void WriteShortString(this Stream writer, string value)
    {
        // Reference: https://www.rfc-editor.org/rfc/rfc3629 (Section 3. UTF-8 definition)
        // TLDR: The max number of bytes per UTF-8 character is 4 
        Span<byte> bytes = stackalloc byte[value.Length * 4];
        var written = Encoding.UTF8.GetBytes(value.AsSpan(), bytes);
        writer.WriteValue(written);
        writer.Write(bytes[..written]);
    }
    
    private static void WriteLongString(this Stream writer, string value)
    {
        var strArr = Encoding.UTF8.GetBytes(value);
        writer.WriteValue(strArr.Length);
        writer.Write(strArr);
    }
    
    public static void WriteValue(this Stream writer, string? value)
    {
        if (value == null) throw new ArgumentException(nameof(value));
        if (value.Length < CommonProtocol.LongStringThreshold) writer.WriteShortString(value);
        else writer.WriteLongString(value);
    }
    
    public static async ValueTask WriteValueAsync(this Stream writer, string value, CancellationToken token = default)
    {
        if (value == null!) throw new ArgumentException(nameof(value));
        var strArr = Encoding.UTF8.GetBytes(value);
        await writer.WriteValueAsync(strArr.Length, token: token);
        await writer.WriteAsync(strArr, token);
    }

    public static void WriteValue(this Stream writer, DateTime value)
    {
        var kind = value.Kind;
        var epoch = DateTime.MinValue.ToUniversalTime();
        var span = value.ToUniversalTime() - epoch;
        writer.WriteValue(kind == DateTimeKind.Local);
        writer.WriteValue(span.Ticks);
    }
    public static void WriteValue(this Stream writer, BytesCluster array)
    {
        writer.WriteValue(array.LongLength);
        writer.Write(array.Reader);
    }

    public static void WriteValue(this Stream writer, byte[] array)
    {
        writer.WriteValue(array.LongLength);
        writer.Write(array);
    }

    public static async ValueTask WriteValueAsync(this Stream writer, byte[] value, CancellationToken token = default)
    {
        await writer.WriteValueAsync(value.LongLength, token);
        await writer.WriteAsync(value, token);
    }

    public static void WriteValue(this Stream writer, Hash128 hash)
    {
        unsafe
        {
            writer.WriteValueInternal(&hash, Hash128.Size);
        }
    }
}