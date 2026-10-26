using System.Text;

namespace Mordor.Core;

/// <summary>
/// Reads Mordor's VB3 random-access (.MDR) data files.
/// VB3 stores fixed-length records; you seek by 1-based record number and read
/// a whole record into a buffer, then parse fields from it. All integers are
/// little-endian. Strings are length-prefixed (16-bit) unless a fixed length is given.
/// Faithful port of libDejenol's RecordReader.h.
/// </summary>
public sealed class RecordReader : IDisposable
{
    private readonly byte[] _buffer;
    private readonly FileStream _file;
    private readonly int _recordLength;
    private int _cursor;

    public RecordReader(string filename, int recordLength)
    {
        _recordLength = recordLength;
        _buffer = new byte[recordLength == 0 ? 65536 : recordLength];
        _file = File.OpenRead(filename);
    }

    /// <summary>Read one record (recordLength bytes, or <paramref name="length"/>) into the buffer.</summary>
    public void Read(int length = -1)
    {
        if (length < 0) length = _recordLength;
        _cursor = 0;
        if (length == 0) return;
        int total = 0;
        while (total < length)
        {
            int n = _file.Read(_buffer, total, length - total);
            if (n == 0) break; // EOF; VB files pad, tolerate short final read
            total += n;
        }
        // zero any unread tail so stale bytes don't leak between records
        for (int i = total; i < length; i++) _buffer[i] = 0;
    }

    /// <summary>Seek to the 1-based record number.</summary>
    public void Seek(long recnum) => _file.Seek((long)_recordLength * (recnum - 1), SeekOrigin.Begin);

    public byte GetByte()
    {
        byte v = _buffer[_cursor];
        _cursor += 1;
        return v;
    }

    public short GetWord()
    {
        short v = (short)(_buffer[_cursor] | (_buffer[_cursor + 1] << 8));
        _cursor += 2;
        return v;
    }

    public ushort GetUWord()
    {
        ushort v = (ushort)(_buffer[_cursor] | (_buffer[_cursor + 1] << 8));
        _cursor += 2;
        return v;
    }

    public int GetDword()
    {
        int v = _buffer[_cursor]
              | (_buffer[_cursor + 1] << 8)
              | (_buffer[_cursor + 2] << 16)
              | (_buffer[_cursor + 3] << 24);
        _cursor += 4;
        return v;
    }

    public long GetInt64()
    {
        long v = 0;
        for (int i = 7; i >= 0; i--) v = (v << 8) + _buffer[_cursor + i];
        _cursor += 8;
        return v;
    }

    public float GetFloat()
    {
        float v = BitConverter.ToSingle(_buffer, _cursor);
        _cursor += 4;
        return v;
    }

    /// <summary>VB3 Currency: 64-bit integer in 1/10000 units. Raw value.</summary>
    public long GetCurrency() => GetInt64();

    /// <summary>VB3 Currency rounded to its logical integer value (raw / 10000).</summary>
    public long GetIntCurrency() => GetInt64() / 10000;

    /// <summary>Read a string. If length is 0, a 16-bit length prefix is read first.</summary>
    public string GetString(int length = 0)
    {
        if (length == 0) length = GetUWord();
        string s = Encoding.Latin1.GetString(_buffer, _cursor, length);
        _cursor += length;
        return s;
    }

    public short[] GetWords(int count)
    {
        var a = new short[count];
        for (int i = 0; i < count; i++) a[i] = GetWord();
        return a;
    }

    public void Dispose() => _file.Dispose();
}
