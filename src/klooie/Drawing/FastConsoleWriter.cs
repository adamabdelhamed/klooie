using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal sealed class FastConsoleWriter
{
    private readonly Stream _outputStream;
    private readonly byte[] _byteBuffer;
    private readonly Encoder _encoder;
    private readonly int _maxCharCount;
    private int _bufferPosition;

    private const int Window = 30;
    private static readonly int[] _ring = new int[Window];
    private static int _idx = 0;      // next slot to overwrite
    private static int _count = 0;    // how many valid samples (<= Window)
    private static long _sum = 0;     // running sum (fits comfortably)

    public static int AvgWriteLength => _count == 0 ? 0 : (int)(_sum / _count);

    private static void RecordWriteLength(int length)
    {
        // subtract old value at slot (0 if not yet filled)
        if (_count == Window)
        {
            _sum -= _ring[_idx];
        }
        else
        {
            _count++; // growing phase until full
        }

        _ring[_idx] = length;
        _sum += length;

        _idx++;
        if (_idx == Window) _idx = 0;
    }


    public FastConsoleWriter(int bufferSize = 8192)
    {
        _outputStream = Console.OpenStandardOutput();
        _byteBuffer = new byte[bufferSize];
        _encoder = Encoding.UTF8.GetEncoder();
        _maxCharCount = Encoding.UTF8.GetMaxCharCount(bufferSize);
        _bufferPosition = 0;
    }

    public void Write(char[] buffer, int length)
    {
        RecordWriteLength(length);
        int charsProcessed = 0;
        while (charsProcessed < length)
        {
            int charsToProcess = Math.Min(_maxCharCount, length - charsProcessed);

            bool completed;
            int bytesUsed;
            int charsUsed;

            _encoder.Convert(
                buffer, charsProcessed, charsToProcess,
                _byteBuffer, 0, _byteBuffer.Length,
                false, out charsUsed, out bytesUsed, out completed);

            _outputStream.Write(_byteBuffer, 0, bytesUsed);
            charsProcessed += charsUsed;
        }
    }
}