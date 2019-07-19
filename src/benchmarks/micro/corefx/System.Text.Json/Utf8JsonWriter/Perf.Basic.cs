// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Linq;
using BenchmarkDotNet.Attributes;
using MicroBenchmarks;

namespace System.Text.Json.Tests
{

    internal class BufferSegment<T> : ReadOnlySequenceSegment<T>
    {
        public BufferSegment(System.ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        public BufferSegment<T> Append(System.ReadOnlyMemory<T> memory)
        {
            var segment = new BufferSegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
    public class Perf_Basic
    {
        private ReadOnlySequence<byte> _sequence;
        private ReadOnlySequence<byte> _multiSegmentSequence;
        private SequencePosition _start;
        private SequencePosition _end;
        private SequencePosition _ms_start;
        private SequencePosition _ms_end;

        private byte[] _jsonData;
        private JsonReaderOptions _options;
        private JsonReaderState _state;

        [GlobalSetup]
        public void Setup()
        {
            Memory<byte> memory = new Memory<byte>(Enumerable.Repeat((byte)1, 10000).ToArray());
            _sequence = new ReadOnlySequence<byte>(memory);
            _start = _sequence.GetPosition(10);
            _end = _sequence.GetPosition(9990);

            BufferSegment<byte> firstSegment = new BufferSegment<byte>(memory.Slice(0, memory.Length / 2));
            BufferSegment<byte> secondSegment = firstSegment.Append(memory.Slice(memory.Length / 2, memory.Length / 2));
            _multiSegmentSequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, firstSegment.Memory.Length);
            _ms_start = _multiSegmentSequence.GetPosition(10);
            _ms_end = _multiSegmentSequence.GetPosition(9990);

            _jsonData = new byte[1000];
            _options = new JsonReaderOptions
            {
                AllowTrailingCommas = true
            };
            _state = new JsonReaderState(_options);
        }

        [Benchmark]
        public Utf8JsonReader CtorBasic()
        {
            return new Utf8JsonReader(_sequence);
        }

        [Benchmark]
        public Utf8JsonReader CtorMultiBasic()
        {
            return new Utf8JsonReader(_multiSegmentSequence, _options);
        }

        [Benchmark]
        public Utf8JsonReader Ctor()
        {
            return new Utf8JsonReader(_sequence, _options);
        }

        [Benchmark]
        public Utf8JsonReader CtorWithState()
        {
            return new Utf8JsonReader(_sequence, isFinalBlock: true, state: _state);
        }

        [Benchmark]
        public Utf8JsonReader CtorMulti()
        {
            return new Utf8JsonReader(_multiSegmentSequence, _options);
        }

        [Benchmark]
        public Utf8JsonReader CtorWithStateMulti()
        {
            return new Utf8JsonReader(_multiSegmentSequence, isFinalBlock: true, state: _state);
        }
    }
}
