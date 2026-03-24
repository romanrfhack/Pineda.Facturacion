using System.Text;

namespace Pineda.Facturacion.Infrastructure.Documents;

// Minimal QR Code Model 2 byte-mode encoder based on the same algorithmic approach
// described by Project Nayuki's MIT-licensed QR Code generator library.
internal static class SimpleQrCodeGenerator
{
    private enum Ecc
    {
        Low = 1,
        Medium = 0,
        Quartile = 3,
        High = 2
    }

    public static QrMatrix? TryEncode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return EncodeBytes(Encoding.UTF8.GetBytes(text));
        }
        catch
        {
            return null;
        }
    }

    private static QrMatrix EncodeBytes(byte[] data)
    {
        const Ecc ecl = Ecc.Medium;

        var version = 1;
        var dataUsedBits = 0;
        for (; version <= Code.MaxVersion; version++)
        {
            var dataCapacityBits = Code.GetNumDataCodewords(version, ecl) * 8;
            var usedBits = 4 + GetByteModeCharCountBits(version) + (data.Length * 8);
            if (usedBits <= dataCapacityBits)
            {
                dataUsedBits = usedBits;
                break;
            }
        }

        if (version > Code.MaxVersion)
        {
            throw new InvalidOperationException("QR payload too long.");
        }

        var bits = new List<int>(dataUsedBits + 64);
        AppendBits(0x4, 4, bits);
        AppendBits(data.Length, GetByteModeCharCountBits(version), bits);
        foreach (var value in data)
        {
            AppendBits(value, 8, bits);
        }

        var dataCapacity = Code.GetNumDataCodewords(version, ecl) * 8;
        AppendBits(0, Math.Min(4, dataCapacity - bits.Count), bits);
        AppendBits(0, (8 - (bits.Count % 8)) % 8, bits);

        for (var padByte = 0xEC; bits.Count < dataCapacity; padByte ^= 0xEC ^ 0x11)
        {
            AppendBits(padByte, 8, bits);
        }

        var dataCodewords = new byte[bits.Count / 8];
        for (var index = 0; index < bits.Count; index++)
        {
            dataCodewords[index >> 3] |= (byte)(bits[index] << (7 - (index & 7)));
        }

        return new Code(version, ecl, dataCodewords, -1).ToMatrix();
    }

    private static int GetByteModeCharCountBits(int version) => version <= 9 ? 8 : 16;

    private static void AppendBits(int value, int length, List<int> bits)
    {
        if (length is < 0 or > 31 || (value >> length) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        for (var i = length - 1; i >= 0; i--)
        {
            bits.Add((value >> i) & 1);
        }
    }

    internal sealed class QrMatrix
    {
        private readonly bool[][] _modules;

        public QrMatrix(bool[][] modules)
        {
            _modules = modules;
            Size = modules.Length;
        }

        public int Size { get; }

        public bool GetModule(int x, int y)
            => y >= 0 && y < _modules.Length && x >= 0 && x < _modules[y].Length && _modules[y][x];
    }

    private sealed class Code
    {
        public const int MinVersion = 1;
        public const int MaxVersion = 40;

        private const int PenaltyN1 = 3;
        private const int PenaltyN2 = 3;
        private const int PenaltyN3 = 40;
        private const int PenaltyN4 = 10;

        private readonly bool[][] _modules;
        private bool[][] _isFunction;

        public Code(int version, Ecc errorCorrectionLevel, IReadOnlyList<byte> dataCodewords, int mask)
        {
            if (version is < MinVersion or > MaxVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (mask is < -1 or > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(mask));
            }

            Version = version;
            ErrorCorrectionLevel = errorCorrectionLevel;
            Size = version * 4 + 17;
            _modules = CreateMatrix(Size);
            _isFunction = CreateMatrix(Size);

            DrawFunctionPatterns();
            var allCodewords = AddEccAndInterleave(dataCodewords);
            DrawCodewords(allCodewords);

            if (mask == -1)
            {
                var minPenalty = int.MaxValue;
                for (var i = 0; i < 8; i++)
                {
                    ApplyMask(i);
                    DrawFormatBits(i);
                    var penalty = GetPenaltyScore();
                    if (penalty < minPenalty)
                    {
                        mask = i;
                        minPenalty = penalty;
                    }

                    ApplyMask(i);
                }
            }

            Mask = mask;
            ApplyMask(mask);
            DrawFormatBits(mask);
            _isFunction = [];
        }

        public int Version { get; }
        public Ecc ErrorCorrectionLevel { get; }
        public int Size { get; }
        public int Mask { get; }

        public QrMatrix ToMatrix()
        {
            var copy = new bool[Size][];
            for (var y = 0; y < Size; y++)
            {
                copy[y] = new bool[Size];
                Array.Copy(_modules[y], copy[y], Size);
            }

            return new QrMatrix(copy);
        }

        public static int GetNumDataCodewords(int version, Ecc ecl)
            => GetNumRawDataModules(version) / 8 - (EccCodewordsPerBlock[(int)ecl][version] * NumErrorCorrectionBlocks[(int)ecl][version]);

        private static int GetNumRawDataModules(int version)
        {
            var result = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                var numAlign = version / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (version >= 7)
                {
                    result -= 36;
                }
            }

            return result;
        }

        private static bool[][] CreateMatrix(int size)
        {
            var result = new bool[size][];
            for (var i = 0; i < size; i++)
            {
                result[i] = new bool[size];
            }

            return result;
        }

        private void DrawFunctionPatterns()
        {
            for (var i = 0; i < Size; i++)
            {
                SetFunctionModule(6, i, i % 2 == 0);
                SetFunctionModule(i, 6, i % 2 == 0);
            }

            DrawFinderPattern(3, 3);
            DrawFinderPattern(Size - 4, 3);
            DrawFinderPattern(3, Size - 4);

            var alignPatternPositions = GetAlignmentPatternPositions();
            for (var i = 0; i < alignPatternPositions.Length; i++)
            {
                for (var j = 0; j < alignPatternPositions.Length; j++)
                {
                    var isCorner = (i == 0 && j == 0)
                        || (i == 0 && j == alignPatternPositions.Length - 1)
                        || (i == alignPatternPositions.Length - 1 && j == 0);
                    if (!isCorner)
                    {
                        DrawAlignmentPattern(alignPatternPositions[i], alignPatternPositions[j]);
                    }
                }
            }

            DrawFormatBits(0);
            DrawVersion();
        }

        private void DrawFormatBits(int mask)
        {
            var data = ((int)ErrorCorrectionLevel << 3) | mask;
            var remainder = data;
            for (var i = 0; i < 10; i++)
            {
                remainder = (remainder << 1) ^ (((remainder >> 9) & 1) * 0x537);
            }

            var bits = ((data << 10) | remainder) ^ 0x5412;

            for (var i = 0; i <= 5; i++)
            {
                SetFunctionModule(8, i, GetBit(bits, i));
            }

            SetFunctionModule(8, 7, GetBit(bits, 6));
            SetFunctionModule(8, 8, GetBit(bits, 7));
            SetFunctionModule(7, 8, GetBit(bits, 8));

            for (var i = 9; i < 15; i++)
            {
                SetFunctionModule(14 - i, 8, GetBit(bits, i));
            }

            for (var i = 0; i < 8; i++)
            {
                SetFunctionModule(Size - 1 - i, 8, GetBit(bits, i));
            }

            for (var i = 8; i < 15; i++)
            {
                SetFunctionModule(8, Size - 15 + i, GetBit(bits, i));
            }

            SetFunctionModule(8, Size - 8, true);
        }

        private void DrawVersion()
        {
            if (Version < 7)
            {
                return;
            }

            var remainder = Version;
            for (var i = 0; i < 12; i++)
            {
                remainder = (remainder << 1) ^ (((remainder >> 11) & 1) * 0x1F25);
            }

            var bits = (Version << 12) | remainder;
            for (var i = 0; i < 18; i++)
            {
                var color = GetBit(bits, i);
                var a = Size - 11 + (i % 3);
                var b = i / 3;
                SetFunctionModule(a, b, color);
                SetFunctionModule(b, a, color);
            }
        }

        private void DrawFinderPattern(int x, int y)
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    var xx = x + dx;
                    var yy = y + dy;
                    if (xx >= 0 && xx < Size && yy >= 0 && yy < Size)
                    {
                        SetFunctionModule(xx, yy, dist is not 2 and not 4);
                    }
                }
            }
        }

        private void DrawAlignmentPattern(int x, int y)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    SetFunctionModule(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            }
        }

        private void SetFunctionModule(int x, int y, bool dark)
        {
            _modules[y][x] = dark;
            _isFunction[y][x] = true;
        }

        private byte[] AddEccAndInterleave(IReadOnlyList<byte> data)
        {
            if (data.Count != GetNumDataCodewords(Version, ErrorCorrectionLevel))
            {
                throw new ArgumentException("Invalid codeword count.", nameof(data));
            }

            var numBlocks = NumErrorCorrectionBlocks[(int)ErrorCorrectionLevel][Version];
            var blockEccLen = EccCodewordsPerBlock[(int)ErrorCorrectionLevel][Version];
            var rawCodewords = GetNumRawDataModules(Version) / 8;
            var numShortBlocks = numBlocks - rawCodewords % numBlocks;
            var shortBlockLen = rawCodewords / numBlocks;

            var blocks = new List<byte[]>(numBlocks);
            var rsDiv = ReedSolomonComputeDivisor(blockEccLen);
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                var datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                var dat = data.Skip(k).Take(datLen).ToList();
                k += datLen;

                var ecc = ReedSolomonComputeRemainder(dat, rsDiv);
                if (i < numShortBlocks)
                {
                    dat.Add(0);
                }

                dat.AddRange(ecc);
                blocks.Add(dat.ToArray());
            }

            var result = new List<byte>(rawCodewords);
            for (var i = 0; i < blocks[0].Length; i++)
            {
                for (var j = 0; j < blocks.Count; j++)
                {
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                    {
                        result.Add(blocks[j][i]);
                    }
                }
            }

            return result.ToArray();
        }

        private void DrawCodewords(IReadOnlyList<byte> data)
        {
            var bitIndex = 0;
            for (var right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right = 5;
                }

                for (var vert = 0; vert < Size; vert++)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        var x = right - j;
                        var upward = ((right + 1) & 2) == 0;
                        var y = upward ? Size - 1 - vert : vert;
                        if (!_isFunction[y][x] && bitIndex < data.Count * 8)
                        {
                            _modules[y][x] = GetBit(data[bitIndex >> 3], 7 - (bitIndex & 7));
                            bitIndex++;
                        }
                    }
                }
            }
        }

        private void ApplyMask(int mask)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var invert = mask switch
                    {
                        0 => (x + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => x % 3 == 0,
                        3 => (x + y) % 3 == 0,
                        4 => ((x / 3) + (y / 2)) % 2 == 0,
                        5 => x * y % 2 + x * y % 3 == 0,
                        6 => (x * y % 2 + x * y % 3) % 2 == 0,
                        7 => ((x + y) % 2 + x * y % 3) % 2 == 0,
                        _ => throw new ArgumentOutOfRangeException(nameof(mask))
                    };

                    if (!_isFunction[y][x] && invert)
                    {
                        _modules[y][x] = !_modules[y][x];
                    }
                }
            }
        }

        private int GetPenaltyScore()
        {
            var result = 0;

            for (var y = 0; y < Size; y++)
            {
                var runColor = false;
                var runLength = 0;
                var runHistory = new int[7];
                for (var x = 0; x < Size; x++)
                {
                    if (_modules[y][x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }

                        runColor = _modules[y][x];
                        runLength = 1;
                    }
                }

                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory) * PenaltyN3;
            }

            for (var x = 0; x < Size; x++)
            {
                var runColor = false;
                var runLength = 0;
                var runHistory = new int[7];
                for (var y = 0; y < Size; y++)
                {
                    if (_modules[y][x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }

                        runColor = _modules[y][x];
                        runLength = 1;
                    }
                }

                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory) * PenaltyN3;
            }

            for (var y = 0; y < Size - 1; y++)
            {
                for (var x = 0; x < Size - 1; x++)
                {
                    var color = _modules[y][x];
                    if (color == _modules[y][x + 1] && color == _modules[y + 1][x] && color == _modules[y + 1][x + 1])
                    {
                        result += PenaltyN2;
                    }
                }
            }

            var dark = 0;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (_modules[y][x])
                    {
                        dark++;
                    }
                }
            }

            var total = Size * Size;
            var k = (int)Math.Ceiling(Math.Abs(dark * 20 - total * 10) / (double)total) - 1;
            result += k * PenaltyN4;
            return result;
        }

        private int[] GetAlignmentPatternPositions()
        {
            if (Version == 1)
            {
                return [];
            }

            var numAlign = Version / 7 + 2;
            var step = ((Version * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4)) * 2;
            var result = new List<int> { 6 };
            for (var pos = Size - 7; result.Count < numAlign; pos -= step)
            {
                result.Insert(1, pos);
            }

            return result.ToArray();
        }

        private static byte[] ReedSolomonComputeDivisor(int degree)
        {
            var result = new byte[degree];
            result[^1] = 1;
            byte root = 1;
            for (var i = 0; i < degree; i++)
            {
                for (var j = 0; j < result.Length; j++)
                {
                    result[j] = ReedSolomonMultiply(result[j], root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }

                root = ReedSolomonMultiply(root, 0x02);
            }

            return result;
        }

        private static byte[] ReedSolomonComputeRemainder(IReadOnlyList<byte> data, IReadOnlyList<byte> divisor)
        {
            var result = new byte[divisor.Count];
            foreach (var value in data)
            {
                var factor = (byte)(value ^ result[0]);
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[^1] = 0;
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] ^= ReedSolomonMultiply(divisor[i], factor);
                }
            }

            return result;
        }

        private static byte ReedSolomonMultiply(byte x, byte y)
        {
            var z = 0;
            for (var i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ (((z >> 7) & 1) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }

            return (byte)z;
        }

        private int FinderPenaltyCountPatterns(IReadOnlyList<int> runHistory)
        {
            var n = runHistory[1];
            var core = n > 0
                && runHistory[2] == n
                && runHistory[3] == n * 3
                && runHistory[4] == n
                && runHistory[5] == n;

            return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
                 + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
        }

        private int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] runHistory)
        {
            if (currentRunColor)
            {
                FinderPenaltyAddHistory(currentRunLength, runHistory);
                currentRunLength = 0;
            }

            currentRunLength += Size;
            FinderPenaltyAddHistory(currentRunLength, runHistory);
            return FinderPenaltyCountPatterns(runHistory);
        }

        private void FinderPenaltyAddHistory(int currentRunLength, int[] runHistory)
        {
            if (runHistory[0] == 0)
            {
                currentRunLength += Size;
            }

            Array.Copy(runHistory, 0, runHistory, 1, runHistory.Length - 1);
            runHistory[0] = currentRunLength;
        }

        private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

        private static readonly int[][] EccCodewordsPerBlock =
        [
            [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
            [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
            [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
            [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30]
        ];

        private static readonly int[][] NumErrorCorrectionBlocks =
        [
            [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
            [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
            [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
            [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81]
        ];
    }
}
