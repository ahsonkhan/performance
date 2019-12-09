// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Encodings.Web;
using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using MicroBenchmarks.Serializers;

namespace System.Text.Json.Tests
{
    public class TestEscaping
    {
        private string _source;
        private byte[] _sourceUtf8;
        private byte[] _sourceNegativeUtf8;

        //[Params(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 16, 17, 31, 32, 33, 47, 63, 64, 65, 100, 1000)]
        [Params(1, 2, 4, 10, 15, 16, 17, 31, 32, 33, 47, 64, 100, 1000)]
        //[Params(32)]
        public int DataLength { get; set; }

        [Params(-1)]
        //[Params(-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31)]
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
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public int NeedsEscapingCurrent()
        {
            return NeedsEscaping(_sourceUtf8);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int NeedsEscapingNew()
        {
            return NeedsEscaping_New(_sourceUtf8);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public int NeedsEscaping_Negative_Current()
        //{
        //    return NeedsEscaping(_sourceNegativeUtf8);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public int NeedsEscaping_Negative_New()
        //{
        //    return NeedsEscaping_New(_sourceNegativeUtf8);
        //}

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

        public static int NeedsEscaping(ReadOnlySpan<byte> value)
        {
            int idx;

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

        public static int NeedsEscaping_New(ReadOnlySpan<byte> value)
        {
            int idx = 0;

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

        private static readonly Vector128<sbyte> Mask0x20 = Vector128.Create((sbyte)0x20);

        private static readonly Vector128<sbyte> Mask0x22 = Vector128.Create((sbyte)0x22);
        private static readonly Vector128<sbyte> Mask0x26 = Vector128.Create((sbyte)0x26);
        private static readonly Vector128<sbyte> Mask0x27 = Vector128.Create((sbyte)0x27);
        private static readonly Vector128<sbyte> Mask0x2B = Vector128.Create((sbyte)0x2B);
        private static readonly Vector128<sbyte> Mask0x3C = Vector128.Create((sbyte)0x3C);
        private static readonly Vector128<sbyte> Mask0x3E = Vector128.Create((sbyte)0x3E);
        private static readonly Vector128<sbyte> Mask0x5C = Vector128.Create((sbyte)0x5C);
        private static readonly Vector128<sbyte> Mask0x60 = Vector128.Create((sbyte)0x60);
    }

    public class TestEscapingSerialize
    {
        private LoginViewModel _value;
        private JsonSerializerOptions _optionsDefault;
        private JsonSerializerOptions _optionsUnsafe;

        [GlobalSetup]
        public void Setup()
        {
            _value = DataGenerator.Generate<LoginViewModel>();
            _optionsDefault = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Default
            };
            _optionsUnsafe = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public byte[] NeedsEscapingDefault()
        {
            return JsonSerializer.SerializeToUtf8Bytes(_value, _optionsDefault);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public byte[] NeedsEscapingRelaxed()
        {
            return JsonSerializer.SerializeToUtf8Bytes(_value, _optionsUnsafe);
        }
    }
}
