// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace System.Text.Json.Tests
{
    [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
    public class Perf_MultiThreaded
    {
        private string[] _strings;
        private byte[][] _utf8Strings;
        private int[] _largeData;
        private string[] _largeDataStrings;
        private byte[][] _largeDataUtf8Strings;

        private ArrayBufferWriter<byte>[] _arrayBufferWriters;
        private ArrayBufferWriter<byte> _arrayBufferWriter;

        private MemoryStream[] _memoryStreams;
        private MemoryStream _memoryStream;

        [Params(0, 10, 30)]
        public int TaskCount;

        [Params(10, 100_000)]
        public int DataSize;

        [Params(false)]
        public bool IsUtf8;

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);

            _strings = new string[DataSize];
            _largeData = new int[DataSize];
            _largeDataStrings = new string[DataSize];

            _utf8Strings = new byte[DataSize][];
            _largeDataUtf8Strings = new byte[DataSize][];

            for (int i = 0; i < DataSize; i++)
            {
                _largeData[i] = random.Next(-10000, 10000);
                _strings[i] = i.ToString();
                _largeDataStrings[i] = _largeData[i].ToString();
            }

            for (int i = 0; i < DataSize; i++)
            {
                _utf8Strings[i] = Encoding.UTF8.GetBytes(_strings[i]);
                _largeDataUtf8Strings[i] = Encoding.UTF8.GetBytes(_largeDataStrings[i]);
            }

            _arrayBufferWriters = new ArrayBufferWriter<byte>[TaskCount];
            for (int i = 0; i < TaskCount; i++)
            {
                _arrayBufferWriters[i] = new ArrayBufferWriter<byte>();
            }
            _arrayBufferWriter = new ArrayBufferWriter<byte>();

            _memoryStreams = new MemoryStream[TaskCount];
            for (int i = 0; i < TaskCount; i++)
            {
                _memoryStreams[i] = new MemoryStream();
            }
            _memoryStream = new MemoryStream();
        }

        /*[Benchmark]
        public void WriteToStream()
        {
            if (TaskCount < 1)
            {
                if (IsUtf8)
                {
                    WriteToStream_Utf8(_memoryStream, _largeData, _largeDataUtf8Strings, _utf8Strings);
                }
                else
                {
                    WriteToStream_Utf16(_memoryStream, _largeData, _largeDataStrings, _strings);
                }
            }
            else
            {
                var tasks = new Task[TaskCount];
                for (int i = 0; i < tasks.Length; i++)
                {
                    MemoryStream ms = _memoryStreams[i];

                    if (IsUtf8)
                    {
                        tasks[i] = new Task(() => WriteToStream_Utf8(ms, _largeData, _largeDataUtf8Strings, _utf8Strings));
                    }
                    else
                    {
                        tasks[i] = new Task(() => WriteToStream_Utf16(ms, _largeData, _largeDataStrings, _strings));
                    }
                }
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i].Start();
                }
                Task.WaitAll(tasks);
            }
        }

        private static void WriteToStream_Utf8(MemoryStream stream, int[] intData, byte[][] stringData, byte[][] names)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
        }

        private static void WriteToStream_Utf16(MemoryStream stream, int[] intData, string[] stringData, string[] names)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
        }*/

        [Benchmark]
        public void WriteToSteamOverIBW()
        {
            if (TaskCount < 1)
            {
                if (IsUtf8)
                {
                    WriteToStreamOverIBW_Utf8(_arrayBufferWriter, _memoryStream, _largeData, _largeDataUtf8Strings, _utf8Strings);
                }
                else
                {
                    WriteToStreamOverIBW_Utf16(_arrayBufferWriter, _memoryStream, _largeData, _largeDataStrings, _strings);
                }
            }
            else
            {
                var tasks = new Task[TaskCount];
                for (int i = 0; i < tasks.Length; i++)
                {
                    MemoryStream ms = _memoryStreams[i];
                    ArrayBufferWriter<byte> abw = _arrayBufferWriters[i];

                    if (IsUtf8)
                    {
                        tasks[i] = new Task(() => WriteToStreamOverIBW_Utf8(abw, ms, _largeData, _largeDataUtf8Strings, _utf8Strings));
                    }
                    else
                    {
                        tasks[i] = new Task(() => WriteToStreamOverIBW_Utf16(abw, ms, _largeData, _largeDataStrings, _strings));
                    }
                }
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i].Start();
                }
                Task.WaitAll(tasks);
            }
        }

        private static void WriteToStreamOverIBW_Utf8(ArrayBufferWriter<byte> output, MemoryStream stream, int[] intData, byte[][] stringData, byte[][] names)
        {
            output.Clear();
            var state = new JsonWriterState(options: new JsonWriterOptions { Indented = false, SkipValidation = true });
            var json = new Utf8JsonWriter(output, state);
            //var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
            stream.Write(output.WrittenMemory.Span);
        }

        private static void WriteToStreamOverIBW_Utf16(ArrayBufferWriter<byte> output, MemoryStream stream, int[] intData, string[] stringData, string[] names)
        {
            output.Clear();
            var state = new JsonWriterState(options: new JsonWriterOptions { Indented = false, SkipValidation = true });
            var json = new Utf8JsonWriter(output, state);
            //var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
            stream.Write(output.WrittenMemory.Span);
        }

        [Benchmark]
        public void WriteToIBW()
        {
            if (TaskCount < 1)
            {
                if (IsUtf8)
                {
                    WriteToIBW_Utf8(_arrayBufferWriter, _largeData, _largeDataUtf8Strings, _utf8Strings);
                }
                else
                {
                    WriteToIBW_Utf16(_arrayBufferWriter, _largeData, _largeDataStrings, _strings);
                }
            }
            else
            {
                var tasks = new Task[TaskCount];
                for (int i = 0; i < tasks.Length; i++)
                {
                    ArrayBufferWriter<byte> abw = _arrayBufferWriters[i];

                    if (IsUtf8)
                    {
                        tasks[i] = new Task(() => WriteToIBW_Utf8(abw, _largeData, _largeDataUtf8Strings, _utf8Strings));
                    }
                    else
                    {
                        tasks[i] = new Task(() => WriteToIBW_Utf16(abw, _largeData, _largeDataStrings, _strings));
                    }
                }
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i].Start();
                }
                Task.WaitAll(tasks);
            }
        }

        private static void WriteToIBW_Utf8(ArrayBufferWriter<byte> output, int[] intData, byte[][] stringData, byte[][] names)
        {
            output.Clear();
            var state = new JsonWriterState(options: new JsonWriterOptions { Indented = false, SkipValidation = true });
            var json = new Utf8JsonWriter(output, state);
            //var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
        }

        private static void WriteToIBW_Utf16(ArrayBufferWriter<byte> output, int[] intData, string[] stringData, string[] names)
        {
            output.Clear();
            var state = new JsonWriterState(options: new JsonWriterOptions { Indented = false, SkipValidation = true });
            var json = new Utf8JsonWriter(output, state);
            //var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = true });

            json.WriteStartObject();
            for (int i = 0; i < intData.Length; i++)
            {
                json.WriteStartArray(names[i]);
                if (intData[i] > 0)
                    json.WriteStringValue(stringData[i]);
                else
                    json.WriteNumberValue(intData[i]);
                json.WriteEndArray();
            }
            json.WriteEndObject();
            json.Flush();
        }

        [Benchmark(Baseline = true)]
        public void WriteToStream_Newtonsoft()
        {
            if (TaskCount < 1)
            {
                if (IsUtf8)
                {
                    WriteToStream_Newtonsoft_Utf16(_memoryStream, _largeData, _largeDataStrings, _strings);
                }
                else
                {
                    WriteToStream_Newtonsoft_Utf16(_memoryStream, _largeData, _largeDataStrings, _strings);
                }
            }
            else
            {
                Task[] tasks = new Task[TaskCount];
                for (int i = 0; i < tasks.Length; i++)
                {
                    MemoryStream ms = _memoryStreams[i];

                    if (IsUtf8)
                    {
                        tasks[i] = new Task(() => WriteToStream_Newtonsoft_Utf16(ms, _largeData, _largeDataStrings, _strings));
                    }
                    else
                    {
                        tasks[i] = new Task(() => WriteToStream_Newtonsoft_Utf16(ms, _largeData, _largeDataStrings, _strings));
                    }
                }
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i].Start();
                }
                Task.WaitAll(tasks);
            }
        }

        private static void WriteToStream_Newtonsoft_Utf16(MemoryStream stream, int[] intData, string[] stringData, string[] names)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var json = new JsonTextWriter(new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)))
            {
                json.WriteStartObject();
                for (int i = 0; i < intData.Length; i++)
                {
                    json.WritePropertyName(names[i]);
                    json.WriteStartArray();
                    if (intData[i] > 0)
                        json.WriteValue(stringData[i]);
                    else
                        json.WriteValue(intData[i]);
                    json.WriteEndArray();
                }
                json.WriteEndObject();
                json.Flush();
            }
        }
    }
}
