// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Encodings.Web;
using BenchmarkDotNet.Attributes;
using MicroBenchmarks;

namespace System.Text.Json.Tests
{
    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 3)]
    public unsafe class Test_EscapingComparison
    {
        private string _source;
        private byte[] _sourceUtf8;
        private byte[] _sourceNegativeUtf8;
        private JavaScriptEncoder _encoder;

        [Params(8, 9, 10, 11, 12, 13, 14, 15, 16, 100)]
        public int DataLength { get; set; }

        [Params(-1)]
        public int NegativeIndex { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);
            var array = new char[DataLength];
            for (int i = 0; i < DataLength; i++)
            {
                array[i] = (char)random.Next(97, 123);
            }
            _source = new string(array);
            _sourceUtf8 = Encoding.UTF8.GetBytes(_source);

            if (NegativeIndex != -1)
            {
                array[NegativeIndex] = '<';
                _source = new string(array);
            }
            _sourceNegativeUtf8 = Encoding.UTF8.GetBytes(_source);
            _encoder = null;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public int NeedsEscapingCurrent()
        {
            return NeedsEscaping(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewFixed()
        {
            return NeedsEscaping_New_Fixed(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int NeedsEscapingBackFill_New()
        {
            return NeedsEscaping_BackFill_New(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewFixedReorder()
        {
            return NeedsEscaping_New_Fixed_Reorder(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewFixedLoopUnrolled()
        {
            return NeedsEscaping_New_Fixed_LoopUnrolled(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewLoopUnrolled()
        {
            return NeedsEscaping_New_LoopUnrolled(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewDoWhile()
        {
            return NeedsEscaping_New_DoWhile(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingBackFill()
        {
            return NeedsEscaping_BackFill(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingJumpTable()
        {
            return NeedsEscaping_JumpTable(_source, _encoder);
        }

        public static int NeedsEscaping_New_Fixed_Reorder(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (char* ptr = value)
            {
                int idx = 0;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (encoder != null && !value.IsEmpty)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                if (value.Length < 8 || !Sse2.IsSupported)
                {
                    for (; idx < value.Length; idx++)
                    {
                        if (NeedsEscaping(*(ptr + idx)))
                        {
                            goto Return;
                        }
                    }
                }
                else
                {
                    short* startingAddress = (short*)ptr;
                    while (value.Length - 8 >= idx)
                    {
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = CreateEscapingMask(sourceValue);

                        int index = Sse2.MoveMask(mask.AsByte());
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += BitOperations.TrailingZeroCount(index) >> 1;
                            goto Return;
                        }
                        idx += 8;
                        startingAddress += 8;
                    }

                    for (; idx < value.Length; idx++)
                    {
                        if (NeedsEscaping(*(ptr + idx)))
                        {
                            goto Return;
                        }
                    }
                }

                idx = -1; // all characters allowed

            Return:
                return idx;
            }
        }

        private static ReadOnlySpan<byte> AllowList => new byte[byte.MaxValue + 1]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0000..U+000F
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0010..U+001F
            1, 1, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, // U+0020..U+002F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0, 1, // U+0030..U+003F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0040..U+004F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, // U+0050..U+005F
            0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0060..U+006F
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, // U+0070..U+007F

            // Also include the ranges from U+0080 to U+00FF for performance to avoid UTF8 code from checking boundary.
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+00F0..U+00FF
        };

        private static bool NeedsEscaping(byte value) => AllowList[value] == 0;

        public static int NeedsEscaping(ReadOnlySpan<byte> value, JavaScriptEncoder encoder)
        {
            int idx;

            if (encoder != null)
            {
                idx = encoder.FindFirstCharacterToEncodeUtf8(value);
                goto Return;
            }

            for (idx = 0; idx < value.Length; idx++)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public static unsafe int NeedsEscaping(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            int idx;

            // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
            // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
            if (encoder != null && !value.IsEmpty)
            {
                fixed (char* ptr = value)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                }
                goto Return;
            }

            for (idx = 0; idx < value.Length; idx++)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public const int LastAsciiCharacter = 0x7F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsEscaping(char value) => value > LastAsciiCharacter || AllowList[value] == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsEscaping_NoBoundsCheck(char value) => AllowList[value] == 0;

        public static readonly ushort[] TrailingAlignmentMask = new ushort[64]
        {
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0xFFFF,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0xFFFF, 0xFFFF,

            0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
            0x0000, 0x0000, 0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
            0x0000, 0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
            0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> CreateEscapingMask(Vector128<short> sourceValue)
        {
            Vector128<short> mask = Sse2.CompareLessThan(sourceValue, Mask_UInt16_0x20); // Control characters

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x22)); // Quotation Mark "
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x26)); // Ampersand &
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x27)); // Apostrophe '
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x2B)); // Plus sign +

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x3C)); // Less Than Sign <
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x3E)); // Greater Than Sign >
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x5C)); // Reverse Solidus \
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x60)); // Grave Access `

            mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, Mask_UInt16_0x7E)); // Tilde ~, anything above the ASCII range

            return mask;
        }

        private static unsafe int BackFillReadAndProcess(short* startingAddress, short* trailingMaskIndex)
        {
            Vector128<short> sourceValueWithBackFill = Sse2.LoadVector128(startingAddress);
            Vector128<short> trailingMask = Sse2.LoadVector128(trailingMaskIndex);
            Vector128<short> sourceValue = Sse2.And(sourceValueWithBackFill, trailingMask);

            Vector128<short> mask = CreateEscapingMask(sourceValue);

            mask = Sse2.And(mask, trailingMask);

            return Sse2.MoveMask(Vector128.AsByte(mask));
        }

        public static unsafe int NeedsEscaping_JumpTable(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (char* ptr = value)
            {
                int idx;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (encoder != null && !value.IsEmpty)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                idx = -1;
                switch (value.Length)
                {
                    case 7:
                        if (NeedsEscaping(*(ptr + 6))) idx = 6;
                        goto case 6;
                    case 6:
                        if (NeedsEscaping(*(ptr + 5))) idx = 5;
                        goto case 5;
                    case 5:
                        if (NeedsEscaping(*(ptr + 4))) idx = 4;
                        goto case 4;
                    case 4:
                        if (NeedsEscaping(*(ptr + 3))) idx = 3;
                        goto case 3;
                    case 3:
                        if (NeedsEscaping(*(ptr + 2))) idx = 2;
                        goto case 2;
                    case 2:
                        if (NeedsEscaping(*(ptr + 1))) idx = 1;
                        goto case 1;
                    case 1:
                        if (NeedsEscaping(*(ptr + 0))) idx = 0;
                        break;
                    case 0:
                        break;
                    default:
                        {
                            short* startingAddress = (short*)ptr;
                            idx = 0;
                            while (value.Length - 8 >= idx)
                            {
                                Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                                Vector128<short> mask = CreateEscapingMask(sourceValue);

                                int index = Sse2.MoveMask(mask.AsByte());
                                // TrailingZeroCount is relatively expensive, avoid it if possible.
                                if (index != 0)
                                {
                                    idx += BitOperations.TrailingZeroCount(index) >> 1;
                                    goto Return;
                                }
                                idx += 8;
                                startingAddress += 8;
                            }

                            int remainder = value.Length - idx;
                            if (remainder > 0)
                            {
                                for (; idx < value.Length; idx++)
                                {
                                    if (NeedsEscaping(value[idx]))
                                    {
                                        goto Return;
                                    }
                                }
                            }
                            idx = -1;
                            break;
                        }
                }

            Return:
                return idx;
            }
        }

        public static unsafe int NeedsEscaping_BackFill_New(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (char* ptr = value)
            {
                int idx = 0;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (encoder != null && !value.IsEmpty)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                if (value.Length < 8)
                {
                    for (; idx < value.Length; idx++)
                    {
                        if (NeedsEscaping(*(ptr + idx)))
                        {
                            goto Return;
                        }
                    }
                }
                else
                {
                    short* startingAddress = (short*)ptr;
                    while (value.Length - 8 >= idx)
                    {
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = CreateEscapingMask(sourceValue);

                        int index = Sse2.MoveMask(mask.AsByte());
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += (BitOperations.TrailingZeroCount(index) >> 1);
                            goto Return;
                        }
                        idx += 8;
                        startingAddress += 8;
                    }

                    int remainder = value.Length - idx;
                    if (remainder > 0)
                    {
                        int backFillCount = 8 - remainder;
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress - backFillCount);

                        Vector128<short> mask = CreateEscapingMask(sourceValue);

                        int index = Sse2.MoveMask(mask.AsByte());
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += (BitOperations.TrailingZeroCount(index) >> 1) - backFillCount;
                            goto Return;
                        }
                    }
                }

                idx = -1; // all characters allowed

            Return:
                return idx;
            }
        }

        public static unsafe int NeedsEscaping_BackFill(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (ushort* pTrailingAlignmentMask = &TrailingAlignmentMask[0])
            fixed (char* ptr = value)
            {
                int idx;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (value.IsEmpty)
                {
                    idx = -1;
                    goto Return;
                }

                if (encoder != null)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                short* startingAddress = (short*)ptr;

                if (value.Length < 8)
                {
                    int backFillCount = 8 - value.Length;
                    int index = BackFillReadAndProcess(startingAddress - backFillCount, (short*)pTrailingAlignmentMask + (value.Length * 8));
                    // TrailingZeroCount is relatively expensive, avoid it if possible.
                    if (index != 0)
                    {
                        idx = (BitOperations.TrailingZeroCount(index) >> 1) - backFillCount;
                        goto Return;
                    }
                }
                else
                {
                    idx = 0;
                    while (value.Length - 8 >= idx)
                    {
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = CreateEscapingMask(sourceValue);

                        int index = Sse2.MoveMask(mask.AsByte());
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += BitOperations.TrailingZeroCount(index) >> 1;
                            goto Return;
                        }
                        idx += 8;
                        startingAddress += 8;
                    }

                    int remainder = value.Length - idx;
                    if (remainder > 0)
                    {
                        int backFillCount = 8 - remainder;
                        int index = BackFillReadAndProcess(startingAddress - backFillCount, (short*)pTrailingAlignmentMask + (remainder * 8));
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += (BitOperations.TrailingZeroCount(index) >> 1) - backFillCount;
                            goto Return;
                        }
                    }
                }

                idx = -1; // all characters allowed

            Return:
                return idx;
            }
        }

        public static int NeedsEscaping_New_Fixed(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (char* ptr = value)
            {
                int idx = 0;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (encoder != null && !value.IsEmpty)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                if (Sse2.IsSupported)
                {
                    short* startingAddress = (short*)ptr;
                    while (value.Length - 8 >= idx)
                    {
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = CreateEscapingMask(sourceValue);

                        int index = Sse2.MoveMask(mask.AsByte());
                        // TrailingZeroCount is relatively expensive, avoid it if possible.
                        if (index != 0)
                        {
                            idx += BitOperations.TrailingZeroCount(index) >> 1;
                            goto Return;
                        }
                        idx += 8;
                        startingAddress += 8;
                    }
                }

                for (; idx < value.Length; idx++)
                {
                    if (NeedsEscaping(*(ptr + idx)))
                    {
                        goto Return;
                    }
                }

                idx = -1; // all characters allowed

            Return:
                return idx;
            }
        }

        public static int NeedsEscaping_New_Fixed_LoopUnrolled(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            fixed (char* ptr = value)
            {
                int idx = 0;

                // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
                // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
                if (encoder != null && !value.IsEmpty)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                    goto Return;
                }

                short* startingAddress = (short*)ptr;
                while (value.Length - 8 >= idx)
                {
                    Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                    Vector128<short> mask = CreateEscapingMask(sourceValue);

                    int index = Sse2.MoveMask(mask.AsByte());
                    // TrailingZeroCount is relatively expensive, avoid it if possible.
                    if (index != 0)
                    {
                        idx += BitOperations.TrailingZeroCount(index) >> 1;
                        goto Return;
                    }
                    idx += 8;
                    startingAddress += 8;
                }

                if (value.Length - 4 >= idx)
                {
                    char* currentIndex = ptr + idx;
                    ulong candidateUInt64 = Unsafe.ReadUnaligned<ulong>(currentIndex);
                    if (AllCharsInUInt64AreAscii(candidateUInt64))
                    {
                        if (NeedsEscaping_NoBoundsCheck(*(currentIndex)) || NeedsEscaping_NoBoundsCheck(*(ptr + ++idx)) || NeedsEscaping_NoBoundsCheck(*(ptr + ++idx)) || NeedsEscaping_NoBoundsCheck(*(ptr + ++idx)))
                        {
                            goto Return;
                        }
                        idx++;
                    }
                    else
                    {
                        if (*(currentIndex) > 0x7F)
                        {
                            goto Return;
                        }
                        idx++;
                        if (*(ptr + idx) > 0x7F)
                        {
                            goto Return;
                        }
                        idx++;
                        if (*(ptr + idx) > 0x7F)
                        {
                            goto Return;
                        }
                        idx++;
                        goto Return;
                    }
                }

                if (value.Length - 2 >= idx)
                {
                    char* currentIndex = ptr + idx;
                    uint candidateUInt32 = Unsafe.ReadUnaligned<uint>(currentIndex);
                    if (AllCharsInUInt32AreAscii(candidateUInt32))
                    {
                        if (NeedsEscaping_NoBoundsCheck(*(currentIndex)) || NeedsEscaping_NoBoundsCheck(*(ptr + ++idx)))
                        {
                            goto Return;
                        }
                        idx++;
                    }
                    else
                    {
                        if (*(currentIndex) > 0x7F)
                        {
                            goto Return;
                        }
                        idx++;
                        goto Return;
                    }
                }

                if (value.Length > idx)
                {
                    if (NeedsEscaping(*(ptr + idx)))
                    {
                        goto Return;
                    }
                }

                idx = -1; // all characters allowed

            Return:
                return idx;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> iff all chars in <paramref name="value"/> are ASCII.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllCharsInUInt64AreAscii(ulong value)
        {
            return (value & ~0x007F007F_007F007Ful) == 0;
        }

        /// <summary>
        /// Returns <see langword="true"/> iff all chars in <paramref name="value"/> are ASCII.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllCharsInUInt32AreAscii(uint value)
        {
            return (value & ~0x007F007Fu) == 0;
        }

        public static int NeedsEscaping_New_LoopUnrolled(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            int idx = 0;

            // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
            // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
            if (encoder != null && !value.IsEmpty)
            {
                fixed (char* ptr = value)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                }
                goto Return;
            }

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value);
            while (value.Length - 8 >= idx)
            {
                Vector128<short> sourceValue = MemoryMarshal.Read<Vector128<short>>(bytes.Slice(idx << 1));

                Vector128<short> mask = CreateEscapingMask(sourceValue);

                int index = Sse2.MoveMask(Vector128.AsByte(mask));
                // TrailingZeroCount is relatively expensive, avoid it if possible.
                if (index != 0)
                {
                    idx += BitOperations.TrailingZeroCount(index) >> 1;
                    goto Return;
                }
                idx += 8;
            }

            if (value.Length - 4 >= idx)
            {
                if (NeedsEscaping(value[idx++]) || NeedsEscaping(value[idx++]) || NeedsEscaping(value[idx++]) || NeedsEscaping(value[idx++]))
                {
                    idx--;
                    goto Return;
                }
            }

            if (value.Length - 2 >= idx)
            {
                if (NeedsEscaping(value[idx++]) || NeedsEscaping(value[idx++]))
                {
                    idx--;
                    goto Return;
                }
            }

            if (value.Length > idx)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public static int NeedsEscaping_New_DoWhile(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            int idx = 0;

            // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
            // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
            if (encoder != null && !value.IsEmpty)
            {
                fixed (char* ptr = value)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                }
                goto Return;
            }

            if (value.Length - 8 >= idx)
            {
                ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value);
                do
                {
                    Vector128<short> sourceValue = MemoryMarshal.Read<Vector128<short>>(bytes.Slice(idx << 1));

                    Vector128<short> mask = CreateEscapingMask(sourceValue);

                    int index = Sse2.MoveMask(mask.AsByte());
                    // TrailingZeroCount is relatively expensive, avoid it if possible.
                    if (index != 0)
                    {
                        idx += BitOperations.TrailingZeroCount(index) >> 1;
                        goto Return;
                    }
                    idx += 8;
                } while (value.Length - 8 >= idx);
            }

            if (value.Length - 4 >= idx)
            {
                if (NeedsEscaping(value[idx]) || NeedsEscaping(value[idx + 1]) || NeedsEscaping(value[idx + 2]) || NeedsEscaping(value[idx + 3]))
                {
                    goto Return;
                }
                idx += 4;
            }

            if (value.Length - 2 >= idx)
            {
                if (NeedsEscaping(value[idx]) || NeedsEscaping(value[idx + 1]))
                {
                    goto Return;
                }
                idx += 2;
            }

            if (value.Length > idx)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public static int NeedsEscaping_New(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
        {
            int idx = 0;

            // Some implementations of JavascriptEncoder.FindFirstCharacterToEncode may not accept
            // null pointers and gaurd against that. Hence, check up-front and fall down to return -1.
            if (encoder != null && !value.IsEmpty)
            {
                fixed (char* ptr = value)
                {
                    idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
                }
                goto Return;
            }

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value);

            while (value.Length - 8 >= idx)
            {
                Vector128<short> sourceValue = MemoryMarshal.Read<Vector128<short>>(bytes.Slice(idx << 1));

                Vector128<short> mask = Sse2.CompareLessThan(sourceValue, Mask_UInt16_0x20); // Control characters

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x22)); // Quotation Mark "
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x26)); // Ampersand &
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x27)); // Apostrophe '
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x2B)); // Plus sign +

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x3C)); // Less Than Sign <
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x3E)); // Greater Than Sign >
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x5C)); // Reverse Solidus \
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask_UInt16_0x60)); // Grave Access `

                mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, Mask_UInt16_0x7E)); // Tilde ~, anything above the ASCII range

                int index = Sse2.MoveMask(Vector128.AsByte(mask));
                // TrailingZeroCount is relatively expensive, avoid it if possible.
                if (index != 0)
                {
                    idx += (BitOperations.TrailingZeroCount(index | 0xFFFF0000)) >> 1;
                    goto Return;
                }
                idx += 8;
            }

            for (; idx < value.Length; idx++)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public static int NeedsEscaping_Encoding(ReadOnlySpan<byte> value, JavaScriptEncoder encoder)
        {
            return encoder.FindFirstCharacterToEncodeUtf8(value);
        }

        public static int NeedsEscaping_New(ReadOnlySpan<byte> value, JavaScriptEncoder encoder)
        {
            int idx = 0;

            if (encoder != null)
            {
                idx = encoder.FindFirstCharacterToEncodeUtf8(value);
                goto Return;
            }

            while (value.Length - 16 >= idx)
            {
                Vector128<sbyte> sourceValue = MemoryMarshal.Read<Vector128<sbyte>>(value.Slice(idx));

                Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, Mask0x20); // Control characters, and anything above 0x7E since sbyte.MaxValue is 0x7E

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x22)); // Quotation Mark "
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x26)); // Ampersand &
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x27)); // Apostrophe '
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x2B)); // Plus sign +

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3C)); // Less Than Sign <
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3E)); // Greater Than Sign >
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x5C)); // Reverse Solidus \
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x60)); // Grave Access `

                int index = Sse2.MoveMask(mask);
                // TrailingZeroCount is relatively expensive, avoid it if possible.
                if (index != 0)
                {
                    idx += BitOperations.TrailingZeroCount(index | 0xFFFF0000);
                    goto Return;
                }
                idx += 16;
            }

            for (; idx < value.Length; idx++)
            {
                if (NeedsEscaping(value[idx]))
                {
                    goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        private static readonly ulong mapLower = 17726150386525405184; // From BitConverter.ToUInt64(new byte[8] {0, 0, 0, 0, 220, 239, 255, 245}, 0);
        private static readonly ulong mapUpper = 18374685929781592063; // From BitConverter.ToUInt64(new byte[8] {255, 255, 255, 247, 127, 255, 255, 254}, 0);

        public static int NeedsEscaping_New_2(ReadOnlySpan<byte> value)
        {
            int idx = 0;
            for (; idx < value.Length; idx++)
            {
                byte val = value[idx];
                int whichFlag = val / 64;
                switch (whichFlag)
                {
                    case 0:
                        if ((mapLower & (ulong)(1 << val)) == 0)
                            goto Return;
                        break;
                    case 1:
                        if ((mapUpper & (ulong)(1 << (val - 64))) == 0)
                            goto Return;
                        break;
                    default:
                        goto Return;
                }
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        private static readonly Vector128<sbyte> Mask0x20 = Vector128.Create((sbyte)0x20);

        private static readonly Vector128<sbyte> Mask0x22 = Vector128.Create((sbyte)0x22);
        private static readonly Vector128<sbyte> Mask0x26 = Vector128.Create((sbyte)0x26);
        private static readonly Vector128<sbyte> Mask0x27 = Vector128.Create((sbyte)0x27);
        private static readonly Vector128<sbyte> Mask0x2B = Vector128.Create((sbyte)0x2B);
        private static readonly Vector128<sbyte> Mask0x3C = Vector128.Create((sbyte)0x3C);
        private static readonly Vector128<sbyte> Mask0x3E = Vector128.Create((sbyte)0x3E);
        private static readonly Vector128<sbyte> Mask0x5C = Vector128.Create((sbyte)0x5C);
        private static readonly Vector128<sbyte> Mask0x60 = Vector128.Create((sbyte)0x60);

        private static readonly Vector128<sbyte> Mask0x7E = Vector128.Create((sbyte)0x7E);

        private static readonly Vector128<short> Mask_UInt16_0x20 = Vector128.Create((short)0x20);

        private static readonly Vector128<short> Mask_UInt16_0x22 = Vector128.Create((short)0x22);
        private static readonly Vector128<short> Mask_UInt16_0x26 = Vector128.Create((short)0x26);
        private static readonly Vector128<short> Mask_UInt16_0x27 = Vector128.Create((short)0x27);
        private static readonly Vector128<short> Mask_UInt16_0x2B = Vector128.Create((short)0x2B);
        private static readonly Vector128<short> Mask_UInt16_0x3C = Vector128.Create((short)0x3C);
        private static readonly Vector128<short> Mask_UInt16_0x3E = Vector128.Create((short)0x3E);
        private static readonly Vector128<short> Mask_UInt16_0x5C = Vector128.Create((short)0x5C);
        private static readonly Vector128<short> Mask_UInt16_0x60 = Vector128.Create((short)0x60);

        private static readonly Vector128<short> Mask_UInt16_0x7E = Vector128.Create((short)0x7E);
    }
}
