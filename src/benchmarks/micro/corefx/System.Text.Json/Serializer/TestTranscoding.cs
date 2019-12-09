// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Diagnostics;
using System.Memory;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Unicode;

namespace System.Text.Json.Serialization.Tests
{
    public class TestTranscoding
    {
        private byte[] _destination;
        private string _source;

        [Params(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 32, 64, 100, 1000)]
        public int DataLength { get; set; }

        [Params(true, false)]
        public bool IsAsciiOnly { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < DataLength; i++)
            {
                if (IsAsciiOnly)
                {
                    sb.Append('a');
                }
                else
                {
                    sb.Append('汉');
                }
            }
            _source = sb.ToString();

            _destination = new byte[DataLength * 3];
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public OperationStatus TranscodingToUtf8FromUtf16()
        {
            return Utf8.FromUtf16(_source, _destination, out _, out _);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int TranscodingToUtf8FromUtf16_()
        {
            return Encoding.UTF8.GetBytes(_source, _destination);
        }
    }

    public class TestBase64
    {
        private string _source;

        [Params(4, 8, 16, 32, 64, 100, 1000)]
        public int DataLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            byte[] bytes = new byte[DataLength];
            var rand = new Random(42);
            rand.NextBytes(bytes);
            _source = Convert.ToBase64String(bytes);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public byte[] CurrentImpl()
        {
            TryGetBytesFromBase64(_source, out byte[] value);
            return value;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public byte[] CustomImpl()
        {
            TryGetBytesFromBase64New(_source, out byte[] value);
            return value;
        }

        internal bool TryGetBytesFromBase64(string _value, out byte[] value)
        {
            if (_value.Length < 4)
            {
                value = default;
                return false;
            }

            // we decode string -> byte, so the resulting length will
            // be /4 * 3 - padding. To be on the safe side, keep padding in slice later
            int bufferSize = _value.Length / 4 * 3;

            byte[] arrayToReturnToPool = null;
            try
            {
                Span<byte> buffer = bufferSize <= 256
                    ? stackalloc byte[bufferSize]
                    : arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(bufferSize);

                if (Convert.TryFromBase64String(_value, buffer, out int bytesWritten))
                {
                    value = buffer.Slice(0, bytesWritten).ToArray();
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
            finally
            {
                if (arrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }
        }

        internal bool TryGetBytesFromBase64New_Old(string _value, out byte[] value)
        {
            if (_value.Length < 4)
            {
                value = default;
                return false;
            }

            byte[] utf8Value = Encoding.UTF8.GetBytes(_value);
            int bufferSize = Base64.GetMaxDecodedFromUtf8Length(utf8Value.Length);

            byte[] arrayToReturnToPool = null;
            try
            {
                Span<byte> buffer = bufferSize <= 256
                    ? stackalloc byte[bufferSize]
                    : arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(bufferSize);

                OperationStatus status = Base64.DecodeFromUtf8InPlace(utf8Value, out int bytesWritten);
                if (status == OperationStatus.Done)
                {
                    value = buffer.Slice(0, bytesWritten).ToArray();
                    return true;
                }
                else
                {
                    Debug.Assert(status == OperationStatus.InvalidData);
                    throw new FormatException("Invalid Base64");
                }
            }
            finally
            {
                if (arrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }
        }

        internal bool TryGetBytesFromBase64New(string _value, out byte[] value)
        {
            if (_value.Length < 4)
            {
                goto False;
            }

            // There might be some concerns about what the maximum value of _value can be and if this can integer overflow
            // This would need to be validated first before going down this approach.
            int bufferSize = _value.Length * 3;
            bufferSize = Base64.GetMaxDecodedFromUtf8Length(bufferSize);

            byte[] arrayToReturnToPool = null;
            try
            {
                Span<byte> buffer = bufferSize <= 256
                    ? stackalloc byte[bufferSize]
                    : arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(bufferSize);

                OperationStatus status = Utf8.FromUtf16(_value.AsSpan(), buffer, out int charsRead, out int bytesWritten);
                // Can't have invalid UTF-8 data and since we define the destination size as max, can't have needs more data/destination too small
                Debug.Assert(status == OperationStatus.Done);
                Debug.Assert(charsRead == _value.Length);

                status = Base64.DecodeFromUtf8InPlace(buffer.Slice(0, bytesWritten), out bytesWritten);
                if (status == OperationStatus.Done)
                {
                    value = buffer.Slice(0, bytesWritten).ToArray();
                    return true;
                }
                goto False;
            }
            finally
            {
                if (arrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }

        False:
            value = default;
            return false;
        }
    }
}
