// The LZMA decoder of the LZMA SDK's C# code (CS/7zip/Compress/LZMA/LzmaDecoder.cs, LzmaBase.cs,
// CS/7zip/Compress/RangeCoder/RangeCoder.cs, RangeCoderBit.cs, RangeCoderBitTree.cs and
// CS/7zip/Compress/LZ/LzOutWindow.cs), by Igor Pavlov, https://www.7-zip.org/sdk.html - the
// decoding parts only, put in one file. The LZMA SDK is placed in the public domain by its author
// ("LZMA SDK is placed in the public domain", the SDK's lzma.txt), so it can be part of this
// GPL-3.0 program.
//
// Changed from the SDK, and only so: the names are the SDK's but nested here, out of its
// SevenZip namespaces; the input is a byte array read with bounds (running off its end is a data
// error, where the SDK's stream would hand back 0xFF); the output is a byte array, either of the
// size given (a match running past it, or an end marker before it, is a data error, where the SDK
// would write on) or, with no size, up to the end marker and refused past a limit; a distance
// reaching before the first byte is a data error in a short rep too, which the SDK checks only
// for a match; the window is no larger than the output can need, rather than the full dictionary
// size the header names (it is a user-chosen file, and a header can name up to 4 GiB); and the
// SDK's exceptions are InvalidDataException. The decoding itself - the range coder, the
// probability models, the state machine - is the SDK's, line for line.

namespace Mordor.Core.Extraction;

/// <summary>An LZMA ("LZMA alone": five property bytes, then the range-coded data) decoder; see the file's header.</summary>
public static class Lzma
{
    /// <summary>
    /// Decodes exactly <paramref name="size"/> bytes from the range-coded data in
    /// <paramref name="input"/> (<paramref name="inputLength"/> bytes from
    /// <paramref name="inputOffset"/>), with the five property bytes <paramref name="props"/>;
    /// <paramref name="consumed"/> is how many input bytes it took. Throws
    /// <see cref="InvalidDataException"/> when the data is not a valid stream of that length (an
    /// end marker before <paramref name="size"/> bytes included).
    /// </summary>
    public static byte[] Decode(ReadOnlySpan<byte> props, byte[] input, int inputOffset, int inputLength, int size, out int consumed)
    {
        if (size < 0) throw new InvalidDataException("LZMA: a negative size");
        var d = new Decoder();
        d.SetDecoderProperties(props, size);
        return d.Code(input, inputOffset, inputLength, size, size, out consumed);
    }

    /// <summary>
    /// <see cref="Decode"/> for a stream of no stated size, which ends with the end marker: what it
    /// holds, up to <paramref name="limit"/> bytes. A stream that would hold more is refused
    /// (<see cref="InvalidDataException"/>) as soon as it passes the limit, not after.
    /// </summary>
    public static byte[] DecodeToEnd(ReadOnlySpan<byte> props, byte[] input, int inputOffset, int inputLength, int limit, out int consumed)
    {
        if (limit < 0) throw new InvalidDataException("LZMA: a negative limit");
        var d = new Decoder();
        d.SetDecoderProperties(props, limit);
        return d.Code(input, inputOffset, inputLength, -1, limit, out consumed);
    }

    // ---- LzmaBase.cs ----
    private static class Base
    {
        public const uint kNumRepDistances = 4;
        public const uint kNumStates = 12;

        public struct State
        {
            public uint Index;
            public void Init() { Index = 0; }
            public void UpdateChar()
            {
                if (Index < 4) Index = 0;
                else if (Index < 10) Index -= 3;
                else Index -= 6;
            }
            public void UpdateMatch() { Index = (uint)(Index < 7 ? 7 : 10); }
            public void UpdateRep() { Index = (uint)(Index < 7 ? 8 : 11); }
            public void UpdateShortRep() { Index = (uint)(Index < 7 ? 9 : 11); }
            public bool IsCharState() { return Index < 7; }
        }

        public const int kNumPosSlotBits = 6;
        public const uint kNumLenToPosStatesBits = 2;
        public const uint kNumLenToPosStates = 1 << (int)kNumLenToPosStatesBits;
        public const uint kMatchMinLen = 2;

        public static uint GetLenToPosState(uint len)
        {
            len -= kMatchMinLen;
            if (len < kNumLenToPosStates) return len;
            return kNumLenToPosStates - 1;
        }

        public const int kNumAlignBits = 4;
        public const uint kStartPosModelIndex = 4;
        public const uint kEndPosModelIndex = 14;
        public const uint kNumFullDistances = 1 << ((int)kEndPosModelIndex / 2);

        public const int kNumPosStatesBitsMax = 4;
        public const uint kNumPosStatesMax = 1 << kNumPosStatesBitsMax;

        public const int kNumLowLenBits = 3;
        public const int kNumMidLenBits = 3;
        public const int kNumHighLenBits = 8;
        public const uint kNumLowLenSymbols = 1 << kNumLowLenBits;
        public const uint kNumMidLenSymbols = 1 << kNumMidLenBits;
    }

    // ---- RangeCoder.cs (the decoder) ----
    private sealed class RangeDecoder
    {
        public const uint kTopValue = 1 << 24;
        public uint Range;
        public uint Code;
        private byte[] _in = Array.Empty<byte>();
        private int _pos;
        private int _end;

        public int Position => _pos;

        public byte ReadByte()
        {
            if (_pos >= _end) throw new InvalidDataException("LZMA: the compressed data ends too soon");
            return _in[_pos++];
        }

        public void Init(byte[] input, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > input.Length || length > input.Length - offset)
                throw new InvalidDataException("LZMA: the compressed data is not where it is said to be");
            _in = input;
            _pos = offset;
            _end = offset + length;
            Code = 0;
            Range = 0xFFFFFFFF;
            for (int i = 0; i < 5; i++)
                Code = (Code << 8) | ReadByte();
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            uint range = Range;
            uint code = Code;
            uint result = 0;
            for (int i = numTotalBits; i > 0; i--)
            {
                range >>= 1;
                uint t = (code - range) >> 31;
                code -= range & (t - 1);
                result = (result << 1) | (1 - t);
                if (range < kTopValue)
                {
                    code = (code << 8) | ReadByte();
                    range <<= 8;
                }
            }
            Range = range;
            Code = code;
            return result;
        }
    }

    // ---- RangeCoderBit.cs (the decoder) ----
    private struct BitDecoder
    {
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = 1 << kNumBitModelTotalBits;
        private const int kNumMoveBits = 5;

        private uint Prob;

        public void Init() { Prob = kBitModelTotal >> 1; }

        public uint Decode(RangeDecoder rangeDecoder)
        {
            uint newBound = (rangeDecoder.Range >> kNumBitModelTotalBits) * Prob;
            if (rangeDecoder.Code < newBound)
            {
                rangeDecoder.Range = newBound;
                Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
                if (rangeDecoder.Range < RangeDecoder.kTopValue)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | rangeDecoder.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 0;
            }
            else
            {
                rangeDecoder.Range -= newBound;
                rangeDecoder.Code -= newBound;
                Prob -= Prob >> kNumMoveBits;
                if (rangeDecoder.Range < RangeDecoder.kTopValue)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | rangeDecoder.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 1;
            }
        }
    }

    // ---- RangeCoderBitTree.cs (the decoder) ----
    private struct BitTreeDecoder
    {
        private readonly BitDecoder[] Models;
        private readonly int NumBitLevels;

        public BitTreeDecoder(int numBitLevels)
        {
            NumBitLevels = numBitLevels;
            Models = new BitDecoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << NumBitLevels); i++)
                Models[i].Init();
        }

        public uint Decode(RangeDecoder rangeDecoder)
        {
            uint m = 1;
            for (int bitIndex = NumBitLevels; bitIndex > 0; bitIndex--)
                m = (m << 1) + Models[m].Decode(rangeDecoder);
            return m - ((uint)1 << NumBitLevels);
        }

        public uint ReverseDecode(RangeDecoder rangeDecoder)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                uint bit = Models[m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= bit << bitIndex;
            }
            return symbol;
        }

        public static uint ReverseDecode(BitDecoder[] Models, uint startIndex, RangeDecoder rangeDecoder, int NumBitLevels)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                uint bit = Models[startIndex + m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= bit << bitIndex;
            }
            return symbol;
        }
    }

    // ---- LzOutWindow.cs, writing into a memory stream ----
    private sealed class OutWindow
    {
        private byte[] _buffer = Array.Empty<byte>();
        private uint _pos;
        private uint _windowSize;
        private uint _streamPos;
        private MemoryStream _stream = new();

        public void Create(uint windowSize)
        {
            if (_windowSize != windowSize)
                _buffer = new byte[windowSize];
            _windowSize = windowSize;
            _pos = 0;
            _streamPos = 0;
        }

        public void Init(MemoryStream stream)
        {
            _stream = stream;
            _streamPos = 0;
            _pos = 0;
        }

        public void Flush()
        {
            uint size = _pos - _streamPos;
            if (size == 0) return;
            _stream.Write(_buffer, (int)_streamPos, (int)size);
            if (_pos >= _windowSize) _pos = 0;
            _streamPos = _pos;
        }

        public void CopyBlock(uint distance, uint len)
        {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize) pos += _windowSize;
            for (; len > 0; len--)
            {
                if (pos >= _windowSize) pos = 0;
                _buffer[_pos++] = _buffer[pos++];
                if (_pos >= _windowSize) Flush();
            }
        }

        public void PutByte(byte b)
        {
            _buffer[_pos++] = b;
            if (_pos >= _windowSize) Flush();
        }

        public byte GetByte(uint distance)
        {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize) pos += _windowSize;
            return _buffer[pos];
        }
    }

    // ---- LzmaDecoder.cs ----
    private sealed class Decoder
    {
        private sealed class LenDecoder
        {
            private BitDecoder m_Choice = new();
            private BitDecoder m_Choice2 = new();
            private readonly BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private readonly BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private BitTreeDecoder m_HighCoder = new(Base.kNumHighLenBits);
            private uint m_NumPosStates;

            public void Create(uint numPosStates)
            {
                for (uint posState = m_NumPosStates; posState < numPosStates; posState++)
                {
                    m_LowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
                    m_MidCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
                }
                m_NumPosStates = numPosStates;
            }

            public void Init()
            {
                m_Choice.Init();
                for (uint posState = 0; posState < m_NumPosStates; posState++)
                {
                    m_LowCoder[posState].Init();
                    m_MidCoder[posState].Init();
                }
                m_Choice2.Init();
                m_HighCoder.Init();
            }

            public uint Decode(RangeDecoder rangeDecoder, uint posState)
            {
                if (m_Choice.Decode(rangeDecoder) == 0)
                    return m_LowCoder[posState].Decode(rangeDecoder);
                uint symbol = Base.kNumLowLenSymbols;
                if (m_Choice2.Decode(rangeDecoder) == 0)
                    symbol += m_MidCoder[posState].Decode(rangeDecoder);
                else
                {
                    symbol += Base.kNumMidLenSymbols;
                    symbol += m_HighCoder.Decode(rangeDecoder);
                }
                return symbol;
            }
        }

        private sealed class LiteralDecoder
        {
            private struct Decoder2
            {
                private BitDecoder[] m_Decoders;
                public void Create() { m_Decoders = new BitDecoder[0x300]; }
                public void Init() { for (int i = 0; i < 0x300; i++) m_Decoders[i].Init(); }

                public byte DecodeNormal(RangeDecoder rangeDecoder)
                {
                    uint symbol = 1;
                    do
                        symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                    while (symbol < 0x100);
                    return (byte)symbol;
                }

                public byte DecodeWithMatchByte(RangeDecoder rangeDecoder, byte matchByte)
                {
                    uint symbol = 1;
                    do
                    {
                        uint matchBit = (uint)(matchByte >> 7) & 1;
                        matchByte <<= 1;
                        uint bit = m_Decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                                symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                            break;
                        }
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }
            }

            private Decoder2[] m_Coders = Array.Empty<Decoder2>();
            private int m_NumPrevBits;
            private int m_NumPosBits;
            private uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders.Length > 0 && m_NumPrevBits == numPrevBits && m_NumPosBits == numPosBits)
                    return;
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                m_Coders = new Decoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Create();
            }

            public void Init()
            {
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Init();
            }

            private uint GetState(uint pos, byte prevByte) =>
                ((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits));

            public byte DecodeNormal(RangeDecoder rangeDecoder, uint pos, byte prevByte) =>
                m_Coders[GetState(pos, prevByte)].DecodeNormal(rangeDecoder);

            public byte DecodeWithMatchByte(RangeDecoder rangeDecoder, uint pos, byte prevByte, byte matchByte) =>
                m_Coders[GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
        }

        private readonly OutWindow m_OutWindow = new();
        private readonly RangeDecoder m_RangeDecoder = new();

        private readonly BitDecoder[] m_IsMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
        private readonly BitDecoder[] m_IsRepDecoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG0Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG1Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG2Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

        private readonly BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
        private readonly BitDecoder[] m_PosDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];

        private BitTreeDecoder m_PosAlignDecoder = new(Base.kNumAlignBits);

        private readonly LenDecoder m_LenDecoder = new();
        private readonly LenDecoder m_RepLenDecoder = new();

        private readonly LiteralDecoder m_LiteralDecoder = new();

        private uint m_DictionarySize;
        private uint m_DictionarySizeCheck;

        private uint m_PosStateMask;

        public Decoder()
        {
            m_DictionarySize = 0xFFFFFFFF;
            for (int i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
        }

        private void SetDictionarySize(uint dictionarySize, int outSize)
        {
            if (m_DictionarySize != dictionarySize)
            {
                m_DictionarySize = dictionarySize;
                m_DictionarySizeCheck = Math.Max(m_DictionarySize, 1);
                // the SDK's Math.Max(m_DictionarySizeCheck, 1 << 12), but no larger than the output
                // can reach back: a distance beyond what has been written is a data error anyway
                uint blockSize = Math.Max(Math.Min(m_DictionarySizeCheck, (uint)Math.Max(outSize, 1)), 1 << 12);
                m_OutWindow.Create(blockSize);
            }
        }

        private void SetLiteralProperties(int lp, int lc)
        {
            if (lp > 8) throw new InvalidDataException("LZMA: bad properties");
            if (lc > 8) throw new InvalidDataException("LZMA: bad properties");
            m_LiteralDecoder.Create(lp, lc);
        }

        private void SetPosBitsProperties(int pb)
        {
            if (pb > Base.kNumPosStatesBitsMax) throw new InvalidDataException("LZMA: bad properties");
            uint numPosStates = (uint)1 << pb;
            m_LenDecoder.Create(numPosStates);
            m_RepLenDecoder.Create(numPosStates);
            m_PosStateMask = numPosStates - 1;
        }

        private void Init(byte[] input, int inputOffset, int inputLength, MemoryStream output)
        {
            m_RangeDecoder.Init(input, inputOffset, inputLength);
            m_OutWindow.Init(output);

            uint i;
            for (i = 0; i < Base.kNumStates; i++)
            {
                for (uint j = 0; j <= m_PosStateMask; j++)
                {
                    uint index = (i << Base.kNumPosStatesBitsMax) + j;
                    m_IsMatchDecoders[index].Init();
                    m_IsRep0LongDecoders[index].Init();
                }
                m_IsRepDecoders[i].Init();
                m_IsRepG0Decoders[i].Init();
                m_IsRepG1Decoders[i].Init();
                m_IsRepG2Decoders[i].Init();
            }

            m_LiteralDecoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i].Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
                m_PosDecoders[i].Init();

            m_LenDecoder.Init();
            m_RepLenDecoder.Init();
            m_PosAlignDecoder.Init();
        }

        /// <summary>
        /// The SDK's Code: <paramref name="outSize"/> bytes, or with <paramref name="outSize"/> -1 up
        /// to the end marker, refused past <paramref name="limit"/> bytes.
        /// </summary>
        public byte[] Code(byte[] input, int inputOffset, int inputLength, long outSize, int limit, out int consumed)
        {
            bool toEnd = outSize < 0;
            var output = new MemoryStream(toEnd ? Math.Min(limit, 1 << 20) : (int)outSize);
            Init(input, inputOffset, inputLength, output);

            var state = new Base.State();
            state.Init();
            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

            ulong nowPos64 = 0;
            ulong outSize64 = toEnd ? (ulong)limit : (ulong)outSize;
            if (nowPos64 < outSize64)
            {
                if (m_IsMatchDecoders[state.Index << Base.kNumPosStatesBitsMax].Decode(m_RangeDecoder) != 0)
                    throw new InvalidDataException("LZMA: the data does not decode");
                state.UpdateChar();
                byte b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, 0, 0);
                m_OutWindow.PutByte(b);
                nowPos64++;
            }
            while (toEnd || nowPos64 < outSize64)
            {
                uint posState = (uint)nowPos64 & m_PosStateMask;
                if (m_IsMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                {
                    if (nowPos64 >= outSize64) throw new InvalidDataException("LZMA: the data is larger than " + limit + " bytes");
                    byte b;
                    byte prevByte = m_OutWindow.GetByte(0);
                    if (!state.IsCharState())
                        b = m_LiteralDecoder.DecodeWithMatchByte(m_RangeDecoder, (uint)nowPos64, prevByte, m_OutWindow.GetByte(rep0));
                    else
                        b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, (uint)nowPos64, prevByte);
                    m_OutWindow.PutByte(b);
                    state.UpdateChar();
                    nowPos64++;
                }
                else
                {
                    uint len;
                    if (m_IsRepDecoders[state.Index].Decode(m_RangeDecoder) == 1)
                    {
                        if (m_IsRepG0Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                        {
                            if (m_IsRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                            {
                                // the SDK reads back rep0 here without the check below a match has; a
                                // stream may not reach back before its own first byte
                                if (rep0 >= nowPos64) throw new InvalidDataException("LZMA: the data does not decode");
                                if (nowPos64 >= outSize64) throw new InvalidDataException("LZMA: the data is larger than " + limit + " bytes");
                                state.UpdateShortRep();
                                m_OutWindow.PutByte(m_OutWindow.GetByte(rep0));
                                nowPos64++;
                                continue;
                            }
                        }
                        else
                        {
                            uint distance;
                            if (m_IsRepG1Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                                distance = rep1;
                            else
                            {
                                if (m_IsRepG2Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                                    distance = rep2;
                                else
                                {
                                    distance = rep3;
                                    rep3 = rep2;
                                }
                                rep2 = rep1;
                            }
                            rep1 = rep0;
                            rep0 = distance;
                        }
                        len = m_RepLenDecoder.Decode(m_RangeDecoder, posState) + Base.kMatchMinLen;
                        state.UpdateRep();
                    }
                    else
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;
                        len = Base.kMatchMinLen + m_LenDecoder.Decode(m_RangeDecoder, posState);
                        state.UpdateMatch();
                        uint posSlot = m_PosSlotDecoder[Base.GetLenToPosState(len)].Decode(m_RangeDecoder);
                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            int numDirectBits = (int)((posSlot >> 1) - 1);
                            rep0 = (2 | (posSlot & 1)) << numDirectBits;
                            if (posSlot < Base.kEndPosModelIndex)
                                rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders, rep0 - posSlot - 1, m_RangeDecoder, numDirectBits);
                            else
                            {
                                rep0 += m_RangeDecoder.DecodeDirectBits(numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits;
                                rep0 += m_PosAlignDecoder.ReverseDecode(m_RangeDecoder);
                            }
                        }
                        else
                            rep0 = posSlot;
                    }
                    // the SDK's TrainSize is 0 here: nothing is decoded on top of earlier data
                    if (rep0 >= nowPos64 || rep0 >= m_DictionarySizeCheck)
                    {
                        // the end marker: the end, or, with the size known, an end too soon
                        if (rep0 == 0xFFFFFFFF && toEnd) break;
                        throw new InvalidDataException(rep0 == 0xFFFFFFFF ? "LZMA: the data ends before its stated size" : "LZMA: the data does not decode");
                    }
                    if (nowPos64 + len > outSize64)
                        throw new InvalidDataException(toEnd ? "LZMA: the data is larger than " + limit + " bytes" : "LZMA: the data runs past its stated size");
                    m_OutWindow.CopyBlock(rep0, len);
                    nowPos64 += len;
                }
            }
            m_OutWindow.Flush();
            consumed = m_RangeDecoder.Position - inputOffset;
            return output.ToArray();
        }

        public void SetDecoderProperties(ReadOnlySpan<byte> properties, int outSize)
        {
            if (properties.Length < 5) throw new InvalidDataException("LZMA: bad properties");
            int lc = properties[0] % 9;
            int remainder = properties[0] / 9;
            int lp = remainder % 5;
            int pb = remainder / 5;
            if (pb > Base.kNumPosStatesBitsMax) throw new InvalidDataException("LZMA: bad properties");
            uint dictionarySize = 0;
            for (int i = 0; i < 4; i++)
                dictionarySize += ((uint)properties[1 + i]) << (i * 8);
            SetDictionarySize(dictionarySize, outSize);
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
        }
    }
}
