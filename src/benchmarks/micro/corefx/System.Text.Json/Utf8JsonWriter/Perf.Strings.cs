// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Tests;
using System.Linq;
using System.Memory;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MicroBenchmarks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PerfLabTests;
using static System.Text.Json.Tests.Test_EscapingUnsafe;

namespace System.Text.Json.Tests
{
    public static class Extensions
    {
        public static string SortProperties(this JsonElement jsonElement)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    jsonElement.SortPropertiesCore(writer);
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static string SortPropertiesIBW(this JsonElement jsonElement)
        {
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(output))
            {
                jsonElement.SortPropertiesCore(writer);
            }
            return Encoding.UTF8.GetString(output.WrittenSpan);
        }

        public static string SortPropertiesIBW(this JsonElement jsonElement, ArrayBufferWriter<byte> output)
        {
            using (var writer = new Utf8JsonWriter(output))
            {
                jsonElement.SortPropertiesCore(writer);
            }
            return Encoding.UTF8.GetString(output.WrittenSpan);
        }

        private static void SortPropertiesCore(this JsonElement jsonElement, Utf8JsonWriter writer)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Undefined:
                    throw new InvalidOperationException();
                case JsonValueKind.Object:
                    jsonElement.SortObjectProperties(writer);
                    break;
                case JsonValueKind.Array:
                    jsonElement.SortArrayProperties(writer);
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    jsonElement.WriteTo(writer);
                    break;
            };
        }

        private static void SortObjectProperties(this JsonElement jObject, Utf8JsonWriter writer)
        {
            Debug.Assert(jObject.ValueKind == JsonValueKind.Object);

            writer.WriteStartObject();
            foreach (JsonProperty prop in jObject.EnumerateObject().OrderBy(p => p.Name))
            {
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteElementHelper(writer);
            }
            writer.WriteEndObject();
        }

        private static void SortArrayProperties(this JsonElement jArray, Utf8JsonWriter writer)
        {
            Debug.Assert(jArray.ValueKind == JsonValueKind.Array);

            writer.WriteStartArray();
            foreach (JsonElement item in jArray.EnumerateArray())
            {
                item.WriteElementHelper(writer);
            }
            writer.WriteEndArray();
        }

        private static void WriteElementHelper(this JsonElement item, Utf8JsonWriter writer)
        {
            Debug.Assert(item.ValueKind != JsonValueKind.Undefined);

            if (item.ValueKind == JsonValueKind.Object)
            {
                item.SortObjectProperties(writer);
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                item.SortArrayProperties(writer);
            }
            else
            {
                item.WriteTo(writer);
            }
        }

        public static string SortPropertiesIBW_Custom(this JsonElement jsonElement, ArrayBufferWriter<byte> output)
        {
            using (var writer = new Utf8JsonWriter(output))
            {
                jsonElement.SortPropertiesCore_Custom(writer);
            }
            return Encoding.UTF8.GetString(output.WrittenSpan);
        }

        private static void SortPropertiesCore_Custom(this JsonElement jsonElement, Utf8JsonWriter writer)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Undefined:
                    throw new InvalidOperationException();
                case JsonValueKind.Object:
                    jsonElement.SortObjectProperties_Custom(writer);
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    jsonElement.SortArrayProperties_Custom(writer);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    jsonElement.WriteTo(writer);
                    break;
            };
        }

        private static void SortObjectProperties_Custom(this JsonElement jObject, Utf8JsonWriter writer)
        {
            Debug.Assert(jObject.ValueKind == JsonValueKind.Object);

            var propertyNames = new List<string>();
            foreach (JsonProperty prop in jObject.EnumerateObject())
            {
                propertyNames.Add(prop.Name);
            }
            propertyNames.Sort();

            writer.WriteStartObject();
            foreach (string name in propertyNames)
            {
                writer.WritePropertyName(name);
                jObject.GetProperty(name).WriteElementHelper_Custom(writer);
            }
            writer.WriteEndObject();
        }

        private static void SortArrayProperties_Custom(this JsonElement jArray, Utf8JsonWriter writer, int i = 0)
        {
            Debug.Assert(jArray.ValueKind == JsonValueKind.Array);
            Console.WriteLine(i);
            i++;
            //writer.WriteStartArray();
            foreach (JsonElement item in jArray.EnumerateArray())
            {
                item.WriteElementHelper_Custom(writer, i);
            }
            //writer.WriteEndArray();
        }

        private static void WriteElementHelper_Custom(this JsonElement item, Utf8JsonWriter writer, int i = 0)
        {
            Debug.Assert(item.ValueKind != JsonValueKind.Undefined);

            if (item.ValueKind == JsonValueKind.Object)
            {
                item.SortObjectProperties_Custom(writer);
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                item.SortArrayProperties_Custom(writer, i);
            }
            else
            {
                item.WriteTo(writer);
            }
        }

        public static JsonElement SortProperties_old(this JsonElement jObject)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    foreach (JsonProperty prop in jObject.EnumerateObject().OrderBy(p => p.Name))
                    {
                        writer.WritePropertyName(prop.Name);
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            prop.Value.SortProperties_old().WriteTo(writer);
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            prop.Value.SortArrayProperties_old().WriteTo(writer);
                        }
                        else
                        {
                            prop.Value.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                    writer.Flush();
                    return JsonDocument.Parse(stream.ToArray()).RootElement;
                }
            }
        }

        public static JsonElement SortArrayProperties_old(this JsonElement jArray)
        {
            if (jArray.GetArrayLength() == 0)
            {
                return jArray;
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartArray();
                    foreach (JsonElement item in jArray.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            item.SortProperties_old().WriteTo(writer);
                        }
                        else if (item.ValueKind == JsonValueKind.Array)
                        {
                            item.SortArrayProperties_old().WriteTo(writer);
                        }
                    }
                    writer.WriteEndArray();
                    writer.Flush();
                    return JsonDocument.Parse(stream.ToArray()).RootElement;
                }
            }
        }
    }

    public class NormalizeTests
    {
        public abstract class AtomicType : DataType
        {
        }

        /// <summary>
        /// Represents a numeric type.
        /// </summary>
        public abstract class NumericType : AtomicType
        {
        }

        /// <summary>
        /// Represents an integral type.
        /// </summary>
        public abstract class IntegralType : NumericType
        {
        }


        public abstract class DataType
        { }
            
            /// <summary>
            /// Represents a fractional type.
            /// </summary>
            public abstract class FractionalType : NumericType
        {
        }

        /// <summary>
        /// Represents a null type.
        /// </summary>
        public sealed class NullType : DataType
        {
        }

        /// <summary>
        /// Represents a string type.
        /// </summary>
        public sealed class StringType : AtomicType
        {
        }

        /// <summary>
        /// Represents a binary (byte array) type.
        /// </summary>
        public sealed class BinaryType : AtomicType
        {
        }

        /// <summary>
        /// Represents a boolean type.
        /// </summary>
        public sealed class BooleanType : AtomicType
        {
        }

        /// <summary>
        /// Represents a date type.
        /// </summary>
        public sealed class DateType : AtomicType
        {
        }

        /// <summary>
        /// Represents a timestamp type.
        /// </summary>
        public sealed class TimestampType : AtomicType
        {
        }

        /// <summary>
        /// Represents a double type.
        /// </summary>
        public sealed class DoubleType : FractionalType
        {
        }

        /// <summary>
        /// Represents a float type.
        /// </summary>
        public sealed class FloatType : FractionalType
        {
        }

        /// <summary>
        /// Represents a byte type.
        /// </summary>
        public sealed class ByteType : IntegralType
        {
        }

        /// <summary>
        /// Represents an int type.
        /// </summary>
        public sealed class IntegerType : IntegralType
        {
        }

        /// <summary>
        /// Represents a long type.
        /// </summary>
        public sealed class LongType : IntegralType
        {
        }

        /// <summary>
        /// Represents a short type.
        /// </summary>
        public sealed class ShortType : IntegralType
        {
        }

        /// <summary>
        /// Represents a decimal type.
        /// </summary>
        public sealed class DecimalType : FractionalType
        {
            private readonly int _precision;
            private readonly int _scale;

            /// <summary>
            /// Initializes the <see cref="DecimalType"/> instance.
            /// </summary>
            /// <remarks>
            /// Default values of precision and scale are from Scala:
            /// sql/catalyst/src/main/scala/org/apache/spark/sql/types/DecimalType.scala.
            /// </remarks>
            /// <param name="precision">Number of digits in a number</param>
            /// <param name="scale">
            /// Number of digits to the right of the decimal point in a number
            /// </param>
            public DecimalType(int precision = 10, int scale = 0)
            {
                _precision = precision;
                _scale = scale;
            }
        }

        private static readonly Type[] s_simpleTypes = new[] {
            typeof(NullType),
            typeof(StringType),
            typeof(BinaryType),
            typeof(BooleanType),
            typeof(DateType),
            typeof(TimestampType),
            typeof(DoubleType),
            typeof(FloatType),
            typeof(ByteType),
            typeof(IntegerType),
            typeof(LongType),
            typeof(ShortType),
            typeof(DecimalType) };

        private static string[] s_simpleTypeMapping = null;

        private static int SimpleTypeIndex(string typeName)
        {
            if (s_simpleTypeMapping == null)
            {
                BuildNormalizedStringMapping();
            }
            return Array.IndexOf(s_simpleTypeMapping, typeName);
        }

        private static int SimpleTypeIndexSpan(string typeName)
        {
            if (s_simpleTypeMapping == null)
            {
                BuildNormalizedStringMapping();
            }
            return s_simpleTypeMapping.AsSpan().IndexOf(typeName);
        }

        private static void BuildNormalizedStringMapping()
        {
            s_simpleTypeMapping = new string[s_simpleTypes.Length];

            for (int i = 0; i < s_simpleTypes.Length; i++)
            {
                s_simpleTypeMapping[i] = NormalizeTypeName(s_simpleTypes[i].Name);
            }
        }

        private static string NormalizeTypeName_old(string typeName) =>
            typeName.Substring(0, typeName.Length - 4).ToLower();

        private static string NormalizeTypeName(string typeName)
        {
            return string.Create(typeName.Length - 4, typeName, (span, typeName) =>
            {
                typeName.AsSpan(0, typeName.Length - 4).ToLower(span, CultureInfo.CurrentCulture);
            });
        }

        private static string NormalizeTypeName_new(string typeName)
        {
            Debug.Assert(typeName.Length <= 256);

            Span<char> destination = stackalloc char[typeName.Length - 4];

            int written = typeName.AsSpan(0, typeName.Length - 4).ToLower(destination, CultureInfo.CurrentCulture);

            Debug.Assert(written == typeName.Length - 4);

            return destination.ToString();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public string NormalizeTypeName_old()
        {
            return NormalizeTypeName(s_simpleTypes[5].Name);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string NormalizeTypeName()
        {
            return NormalizeTypeName(s_simpleTypes[5]);
        }

        private static readonly Lazy<string[]> s_simpleTypeNormalizedNames =
            new Lazy<string[]>(
                () => s_simpleTypes.Select(t => NormalizeTypeName(t.Name)).ToArray());

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public DataType ParseSimpleType_old()
        {
            return ParseSimpleType_old(_token);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public DataType ParseSimpleType_new()
        {
            return ParseSimpleType_lazy(_token);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public DataType ParseDataType_old()
        //{
        //    return ParseDataType_old(_token);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public DataType ParseDataType_new()
        //{
        //    return ParseDataType_new(_token);
        //}

        internal static DataType ParseDataType_old(JToken json)
        {
            if (json.Type == JTokenType.Object)
            {
                var typeJObject = (JObject)json;
                if (typeJObject.TryGetValue("type", out JToken type))
                {
                    Type complexType = s_complexTypes.FirstOrDefault(
                        (t) => NormalizeTypeName_old(t.Name) == type.ToString());

                    if (complexType != default)
                    {
                        return (DataType)Activator.CreateInstance(
                            complexType,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                            null,
                            new object[] { typeJObject },
                            null);
                    }
                    else if (type.ToString() == "udt")
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new ArgumentException($"Could not parse data type: {type}");
            }
            else
            {
                throw new NotImplementedException();
            }

        }
        public sealed class ArrayType : DataType
        {
            internal ArrayType(JObject json)
            {

            }
        }

        public sealed class MapType : DataType
        {
            internal MapType(JObject json)
            {

            }
        }

        public sealed class StructType : DataType
        {
            internal StructType(JObject json)
            {

            }
        }

        private static readonly Type[] s_complexTypes = new[] {
        typeof(ArrayType),
        typeof(MapType),
        typeof(StructType) };

        private static readonly Lazy<string[]> s_complexTypeNormalizedNames =
            new Lazy<string[]>(
                () => s_complexTypes.Select(t => NormalizeTypeName(t.Name)).ToArray());

        internal static DataType ParseDataType_new(JToken json)
        {
            if (json.Type == JTokenType.Object)
            {
                var typeJObject = (JObject)json;
                if (typeJObject.TryGetValue("type", out JToken type))
                {
                    string typeName = type.ToString();

                    int typeIndex = Array.IndexOf(s_complexTypeNormalizedNames.Value, typeName);

                    if (typeIndex != -1)
                    {
                        return (DataType)Activator.CreateInstance(
                            s_complexTypes[typeIndex],
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                            null,
                            new object[] { typeJObject },
                            null);
                    }
                    else if (typeName == "udt")
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new ArgumentException($"Could not parse data type: {type}");
            }
            else
            {
                throw new NotImplementedException();
            }

        }

        private static DataType ParseSimpleType_old(JToken json)
        {
            string typeName = json.ToString();
            Type simpleType = s_simpleTypes.FirstOrDefault(
                (t) => NormalizeTypeName_old(t.Name) == typeName);

            if (simpleType != default)
            {
                return (DataType)Activator.CreateInstance(simpleType);
            }

            throw new ArgumentException($"Could not parse data type: {json}");
        }

        private static string NormalizeTypeName(Type type)
        {
            if (s_simpleTypeMapping == null)
            {
                BuildNormalizedStringMapping();
            }

            for (int i = 0; i < s_simpleTypes.Length; i++)
            {
                if (s_simpleTypes[i] == type)
                {
                    return s_simpleTypeMapping[i];
                }
            }

            return NormalizeTypeName(type.Name);
        }

        JToken _token;

        [GlobalSetup]
        public void Setup()
        {
            _token = JToken.Parse("\"timestamp\"");
            //_token = JToken.Parse("{\"type\":\"array\"}");
        }


        private static DataType ParseSimpleType_lazy(JToken json)
        {
            string typeName = json.ToString();

            int typeIndex = SimpleTypeIndex2(typeName);

            if (typeIndex != -1)
            {
                return (DataType)Activator.CreateInstance(s_simpleTypes[typeIndex]);
            }

            throw new ArgumentException($"Could not parse data type: {json}");
        }

        private static int SimpleTypeIndex2(string typeName)
        {
            return Array.IndexOf(s_simpleTypeNormalizedNames.Value, typeName);
        }


        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public int ArrayIndexOf()
        {
            return SimpleTypeIndex("timestamp");
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int SpanIndexOf()
        {
            return SimpleTypeIndexSpan("timestamp");
        }
    }

    public class TestSortingProperties
    {
        private JsonDocument _doc;
        private ArrayBufferWriter<byte> _output;

        [GlobalSetup]
        public void Setup()
        {
            var obj = new
            {
                objC = new
                {
                    propC = "valueC",
                    propB = "valueB",
                    propA = "valueA"
                },
                objB = new
                {
                    propC = "valueC",
                    propB = "valueB",
                    propA = "valueA"
                },
                objA = new
                {
                    propC = "valueC",
                    propB = "valueB",
                    propA = "valueA"
                },
                arrayC = new[] {
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    }
                },
                arrayB = new[] {
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    }
                },
                arrayA = new[] {
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    },
                    new {
                        propC = "valueC",
                        propB = "valueB",
                        propA = "valueA"
                    }
                }
            };
            string json = JsonSerializer.Serialize(obj);

            //string json = "{\"propertyA\": \"first\", \"propertyC\": 3, \"propertyB\": \"second\"}";


            var builder = new StringBuilder();
            for (int i = 0; i < 10_001; i++)
                builder.Append("[");

            for (int i = 0; i < 10_001; i++)
                builder.Append("]");
            _doc = JsonDocument.Parse(builder.ToString(), new JsonDocumentOptions { MaxDepth = 10_001 });
            _output = new ArrayBufferWriter<byte>();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _doc.Dispose();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public string SortOld()
        {
            return JsonSerializer.Serialize(_doc.RootElement.SortProperties_old());
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SortOldWithoutSerialize()
        {
            return _doc.RootElement.SortProperties_old().ToString();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SortNew()
        {
            return _doc.RootElement.SortProperties();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SortNewIBW()
        {
            return _doc.RootElement.SortPropertiesIBW();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SortNewIBWCached()
        {
            string str = _doc.RootElement.SortPropertiesIBW(_output);
            _output.Clear();
            return str;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SortNewIBWCached_Custom()
        {
            string str = _doc.RootElement.SortPropertiesIBW_Custom(_output);
            _output.Clear();
            return str;
        }
    }

    public class TestPartOfSpeech
    {
        private JsonElement partOfSpeech;

        [GlobalSetup]
        public void Setup()
        {
            partOfSpeech = JsonDocument.Parse("[\"NOUN\", \"ADJ\"]").RootElement;
        }

        public enum PartOfSpeech
        {
            Adjective,
            Adverb,
            Conjunction,
            Other,
            Verb,
            Pronoun,
            Preposition,
            Noun,
            Modal,
            Determiner
        }

        private static readonly byte[] s_adj = Encoding.UTF8.GetBytes("ADJ");
        private static readonly byte[] s_adv = Encoding.UTF8.GetBytes("ADV");
        private static readonly byte[] s_conj = Encoding.UTF8.GetBytes("CONJ");
        private static readonly byte[] s_det = Encoding.UTF8.GetBytes("DET");
        private static readonly byte[] s_modal = Encoding.UTF8.GetBytes("MODAL");
        private static readonly byte[] s_noun = Encoding.UTF8.GetBytes("NOUN");

        private static readonly byte[] s_prep = Encoding.UTF8.GetBytes("PREP");
        private static readonly byte[] s_pron = Encoding.UTF8.GetBytes("PRON");
        private static readonly byte[] s_verb = Encoding.UTF8.GetBytes("VERB");
        private static readonly byte[] s_other = Encoding.UTF8.GetBytes("OTHER");


        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public PartOfSpeech Old()
        {
            return (PartOfSpeech)((int)Helper(partOfSpeech[0]) | (int)Helper(partOfSpeech[1]));
        }

        private PartOfSpeech Helper(JsonElement partOfSpeech)
        {
            if (partOfSpeech.ValueEquals(s_adj)) return PartOfSpeech.Adjective;
            if (partOfSpeech.ValueEquals(s_adv)) return PartOfSpeech.Adverb;
            if (partOfSpeech.ValueEquals(s_conj)) return PartOfSpeech.Conjunction;
            if (partOfSpeech.ValueEquals(s_det)) return PartOfSpeech.Determiner;
            if (partOfSpeech.ValueEquals(s_modal)) return PartOfSpeech.Modal;
            if (partOfSpeech.ValueEquals(s_noun)) return PartOfSpeech.Noun;

            if (partOfSpeech.ValueEquals(s_prep)) return PartOfSpeech.Preposition;
            if (partOfSpeech.ValueEquals(s_pron)) return PartOfSpeech.Pronoun;
            if (partOfSpeech.ValueEquals(s_verb)) return PartOfSpeech.Verb;
            if (partOfSpeech.ValueEquals(s_other)) return PartOfSpeech.Other;

            Debug.Fail("unknown part of speech : " + partOfSpeech.GetString());

            return PartOfSpeech.Other;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public PartOfSpeech New()
        {
            return (PartOfSpeech)((int)ParsePartOfSpeech(partOfSpeech[0]) | (int)ParsePartOfSpeech(partOfSpeech[1]));
        }

        internal static PartOfSpeech ParsePartOfSpeech(JsonElement partOfSpeech)
        {
            string speech = partOfSpeech.GetString();
            PartOfSpeech speechPart = PartOfSpeech.Other;
            switch (speech)
            {
                case "ADJ":
                    speechPart = PartOfSpeech.Adjective;
                    break;
                case "ADV":
                    speechPart = PartOfSpeech.Adverb;
                    break;
                case "CONJ":
                    speechPart = PartOfSpeech.Conjunction;
                    break;
                case "DET":
                    speechPart = PartOfSpeech.Determiner;
                    break;
                case "MODAL":
                    speechPart = PartOfSpeech.Modal;
                    break;
                case "NOUN":
                    speechPart = PartOfSpeech.Noun;
                    break;
                case "PREP":
                    speechPart = PartOfSpeech.Pronoun;
                    break;
                case "PRON":
                    speechPart = PartOfSpeech.Verb;
                    break;
                case "VERB":
                    speechPart = PartOfSpeech.Other;
                    break;
                default:
                    Debug.Fail("unknown part of speech : " + partOfSpeech.GetString());
                    break;
            };

            return speechPart;
        }
    }

    public unsafe class Test_Escaping
    {
        private JavaScriptEncoder _encoder;
        private byte[] _utf8String;

        [ParamsSource(nameof(JsonStringData))]
        public (int Length, int EscapeCharIndex, string JsonString) TestStringData { get; set; }

        private static readonly int[] dataLengths = new int[] { 100 };

        private static readonly int[] subsetToAddEscapedCharacters = new int[] { 1, 7, 8 };

        public static IEnumerable<(int, int, string)> JsonStringData()
        {
            var random = new Random(42);

            for (int j = 0; j < dataLengths.Length; j++)
            {
                int dataLength = dataLengths[j];
                var array = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    array[i] = (char)random.Next(97, 123);
                }
                yield return (dataLength, -1, new string(array)); // No character requires escaping

                if (subsetToAddEscapedCharacters.Contains(dataLength))
                {
                    for (int i = 0; i < dataLength; i++)
                    {
                        char currentChar = array[i];
                        array[i] = '<';
                        yield return (dataLength, i, new string(array)); // One character requires escaping at index i
                        array[i] = currentChar;
                    }
                }

            }
        }

        //[ParamsSource(nameof(Utf8JsonStringData))]
        //public (int Length, int EscapeCharIndex, byte[] Utf8JsonString) TestStringData { get; set; }

        //public static IEnumerable<(int, int, byte[])> Utf8JsonStringData()
        //{
        //    var random = new Random(42);

        //    for (int j = 0; j < dataLengths.Length; j++)
        //    {
        //        int dataLength = dataLengths[j];
        //        var array = new char[dataLength];
        //        for (int i = 0; i < dataLength; i++)
        //        {
        //            array[i] = (char)random.Next(97, 123);
        //        }
        //        yield return (dataLength, -1, Encoding.UTF8.GetBytes(new string(array))); // No character requires escaping

        //        if (subsetToAddEscapedCharacters.Contains(dataLength))
        //        {
        //            for (int i = 0; i < dataLength; i++)
        //            {
        //                char currentChar = array[i];
        //                array[i] = '<';
        //                yield return (dataLength, i, Encoding.UTF8.GetBytes(new string(array))); // One character requires escaping at index i
        //                array[i] = currentChar;
        //            }
        //        }

        //    }
        //}

        [GlobalSetup]
        public void Setup()
        {
            _encoder = JavaScriptEncoder.Default;
            _utf8String = Encoding.UTF8.GetBytes(TestStringData.JsonString);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public int NeedsEscapingCurrent()
        {
            return NeedsEscaping(_utf8String, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingIntrinsics()
        {
            return NeedsEscapingIntrinsics(TestStringData.JsonString, _encoder);
        }

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

        public static int NeedsEscapingIntrinsics(ReadOnlySpan<byte> value, JavaScriptEncoder encoder)
        {
            fixed (byte* ptr = value)
            {
                int idx = 0;

                if (encoder != null)
                {
                    idx = encoder.FindFirstCharacterToEncodeUtf8(value);
                    goto Return;
                }

                sbyte* startingAddress = (sbyte*)ptr;
                while (value.Length - 16 >= idx)
                {
                    Vector128<sbyte> sourceValue = Sse2.LoadVector128(startingAddress);

                    Vector128<sbyte> mask = CreateEscapingMask(sourceValue);

                    int index = Sse2.MoveMask(mask.AsByte());
                    // TrailingZeroCount is relatively expensive, avoid it if possible.
                    if (index != 0)
                    {
                        idx += BitOperations.TrailingZeroCount(index | 0xFFFF0000);
                        goto Return;
                    }
                    idx += 16;
                    startingAddress += 16;
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

        public static int NeedsEscapingIntrinsics(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
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

        public const int LastAsciiCharacter = 0x7F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsEscaping(char value) => value > LastAsciiCharacter || AllowList[value] == 0;

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

        private static readonly Vector128<sbyte> Mask0x20 = Vector128.Create((sbyte)0x20);

        private static readonly Vector128<sbyte> Mask0x22 = Vector128.Create((sbyte)0x22);
        private static readonly Vector128<sbyte> Mask0x26 = Vector128.Create((sbyte)0x26);
        private static readonly Vector128<sbyte> Mask0x27 = Vector128.Create((sbyte)0x27);
        private static readonly Vector128<sbyte> Mask0x2B = Vector128.Create((sbyte)0x2B);
        private static readonly Vector128<sbyte> Mask0x3C = Vector128.Create((sbyte)0x3C);
        private static readonly Vector128<sbyte> Mask0x3E = Vector128.Create((sbyte)0x3E);
        private static readonly Vector128<sbyte> Mask0x5C = Vector128.Create((sbyte)0x5C);
        private static readonly Vector128<sbyte> Mask0x60 = Vector128.Create((sbyte)0x60);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<sbyte> CreateEscapingMask(Vector128<sbyte> sourceValue)
        {
            Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, Mask0x20); // Control characters, and anything above 0x7E since sbyte.MaxValue is 0x7E

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x22)); // Quotation Mark "
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x26)); // Ampersand &
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x27)); // Apostrophe '
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x2B)); // Plus sign +

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3C)); // Less Than Sign <
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3E)); // Greater Than Sign >
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x5C)); // Reverse Solidus \
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x60)); // Grave Access `

            return mask;
        }
    }


    public unsafe class Test_Escaping_Old
    {
        private string _source;
        private byte[] _sourceUtf8;
        private byte[] _sourceNegativeUtf8;
        private JavaScriptEncoder _encoder;

        //[Params(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 16, 17, 31, 32, 33, 47, 63, 64, 65, 100, 1000)]
        //[Params(1, 2, 4, 10, 15, 16, 17, 31, 32, 33, 47, 64, 100, 1000)]
        //[Params(1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 100, 1000)]
        //[Params(1, 2, 4, 6, 7, 8, 9, 100)]
        //[Params(32)]
        [Params(1)]
        public int DataLength { get; set; }

        //[Params(-1)]
        [Params(-1, 0)]
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
            _encoder = null;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public int NeedsEscapingCurrent()
        {
            return NeedsEscaping(_source, _encoder);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int NeedsEscapingNewFixed()
        {
            return NeedsEscaping_New_Fixed(_source, _encoder);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewFixedLoopUnrolled()
        {
            return NeedsEscaping_New_Fixed_LoopUnrolled(_source, _encoder);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingNewLoopUnrolled()
        {
            return NeedsEscaping_New_LoopUnrolled(_source, _encoder);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingNew()
        //{
        //    return NeedsEscaping_New(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingNewFixed()
        //{
        //    return NeedsEscaping_New_Fixed(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingNewFixedLoopUnrolled()
        //{
        //    return NeedsEscaping_New_Fixed_LoopUnrolled(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingNewLoopUnrolled()
        //{
        //    return NeedsEscaping_New_LoopUnrolled(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingNewDoWhile()
        //{
        //    return NeedsEscaping_New_DoWhile(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        ////[Benchmark]
        //public int NeedsEscapingBackFill()
        //{
        //    return NeedsEscaping_BackFill(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public int NeedsEscapingJumpTable()
        //{
        //    return NeedsEscaping_JumpTable(_source, _encoder);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public int NeedsEscapingCurrent()
        //{
        //    return NeedsEscaping(_sourceUtf8);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public int NeedsEscapingNew()
        //{
        //    return NeedsEscaping_New(_sourceUtf8);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public int NeedsEscapingEncoding()
        //{
        //    return NeedsEscaping_Encoding(_sourceUtf8, JavaScriptEncoder.Default);
        //}

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

        public static int NeedsEscaping_New_Snapshot(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
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

        //public static int NeedsEscaping_New(ReadOnlySpan<byte> value)
        //{
        //    // Approach 1 using ulongs:

        //    //ulong myValLower;
        //    //ulong myValUpper;

        //    //byte val = value[idx];

        //    //int remainder = val % 8;
        //    //int dividend = val / 8;
        //    //int bitIndex = val - 64;
        //    //bool needsEscaping = myValUpper & (1 << bitIndex) == 0

        //    if (value.Length >= 16)
        //    {
        //        Vector128<sbyte> sourceValue = MemoryMarshal.Read<Vector128<sbyte>>(value);
        //        Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, Mask0x20);
        //        mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, Mask0x7E));

        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x22));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x26));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x27));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x2B));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3C));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3E));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x5C));
        //        mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x60));

        //        int index = Sse2.MoveMask(mask);
        //        int count = BitOperations.TrailingZeroCount(index);

        //        //bool needsEscaped = index != 0;
        //    }

        //    //int count = BitOperations.TrailingZeroCount(index);
        //    //for (int i = 0; i < count; i++)
        //    //{

        //    //}
        //}

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

        public static int NeedsEscaping_New_3(ReadOnlySpan<byte> value)
        {
            int idx = 0;

            while (value.Length - 16 >= idx)
            {
                Vector128<sbyte> vec = MemoryMarshal.Read<Vector128<sbyte>>(value.Slice(idx));
                int numBytesToSkip = GetNumAllowedBytes(vec);
                idx += numBytesToSkip;
                if (numBytesToSkip != 16)
                {
                    goto Return;
                }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNumAllowedBytes(Vector128<sbyte> sourceValue)
        {
            Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, Mask0x20);
            //mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, Mask0x7E));

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x22));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x26));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x27));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x2B));

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3C));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x3E));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x5C));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x60));
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, Mask0x7E));

            int index = Sse2.MoveMask(mask);
            int count = 16;
            if (index != 0)
            {
                count = BitOperations.TrailingZeroCount(index | 0xFFFF0000);
            }
            return count;
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

        public static int NeedsEscaping_New_old(ReadOnlySpan<byte> value)
        {
            int idx = 0;

            uint count = DivMod((uint)value.Length, 16, out uint modulo);

            while (count > 0)
            {
                Vector128<byte> vec = MemoryMarshal.Read<Vector128<byte>>(value.Slice(idx));
                int numBytesToSkip = GetNumberOfAllowedBytes(vec);
                idx += numBytesToSkip;
                if (numBytesToSkip != 16)
                {
                    goto Return;
                }
                count--;
            }

            Debug.Assert(modulo == value.Length - idx);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint DivMod(uint numerator, uint denominator, out uint modulo)
        {
            uint div = numerator / denominator;
            modulo = numerator - (div * denominator);
            return div;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int GetNumberOfAllowedBytes(Vector128<byte> sixteenBytesOfData)
        {
            Vector128<int> myVector1 = Ssse3.Shuffle(sixteenBytesOfData, ShufMask1).AsInt32();
            var gathered1 = Avx2.GatherVector128(Bitmap, myVector1, sizeof(int));
            int mask1 = Sse2.MoveMask(gathered1.AsByte());

            var myVector2 = Ssse3.Shuffle(sixteenBytesOfData, ShufMask2).AsInt32();
            var gathered2 = Avx2.GatherVector128(Bitmap, myVector2, sizeof(int));
            int mask2 = Sse2.MoveMask(gathered2.AsByte());

            var myVector3 = Ssse3.Shuffle(sixteenBytesOfData, ShufMask3).AsInt32();
            var gathered3 = Avx2.GatherVector128(Bitmap, myVector3, sizeof(int));
            int mask3 = Sse2.MoveMask(gathered3.AsByte());

            var myVector4 = Ssse3.Shuffle(sixteenBytesOfData, ShufMask4).AsInt32();
            var gathered4 = Avx2.GatherVector128(Bitmap, myVector4, sizeof(int));
            int mask4 = Sse2.MoveMask(gathered4.AsByte());

            int combinedMask = (mask4 << 3) | (mask3 << 2) | (mask2 << 1) | mask1;
            return BitOperations.TrailingZeroCount(combinedMask | 1 << 16);
        }

        private static readonly Vector128<byte> ShufMask1 = Vector128.Create(
            0x00, 0xFF, 0xFF, 0xFF,
            0x01, 0xFF, 0xFF, 0xFF,
            0x02, 0xFF, 0xFF, 0xFF,
            0x03, 0xFF, 0xFF, 0xFF);

        private static readonly Vector128<byte> ShufMask2 = Vector128.Create(
            0x04, 0xFF, 0xFF, 0xFF,
            0x05, 0xFF, 0xFF, 0xFF,
            0x06, 0xFF, 0xFF, 0xFF,
            0x07, 0xFF, 0xFF, 0xFF);

        private static readonly Vector128<byte> ShufMask3 = Vector128.Create(
            0x08, 0xFF, 0xFF, 0xFF,
            0x09, 0xFF, 0xFF, 0xFF,
            0x0A, 0xFF, 0xFF, 0xFF,
            0x0B, 0xFF, 0xFF, 0xFF);

        private static readonly Vector128<byte> ShufMask4 = Vector128.Create(
            0x0C, 0xFF, 0xFF, 0xFF,
            0x0D, 0xFF, 0xFF, 0xFF,
            0x0E, 0xFF, 0xFF, 0xFF,
            0x0F, 0xFF, 0xFF, 0xFF);

        private static int* Bitmap => (int*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(AllowList));
    }

    [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
    public class Perf_Strings
    {
        private const int DataSize = 1_000;

        private string[] _stringArrayValues;
        private byte[] _destination;

        [Params(2, 8, 16, 32, 64, 100, 256)]
        public int TextSize;

        [GlobalSetup]
        public void Setup()
        {
            _stringArrayValues = new string[DataSize];

            var random = new Random(42);

            for (int i = 0; i < DataSize; i++)
            {
                _stringArrayValues[i] = GetString(random, TextSize);
            }
            _destination = new byte[10_000];
        }

        private static string GetString(Random random, int length)
        {
            var array = new char[length];

            for (int i = 0; i < length; i++)
            {
                array[i] = (char)random.Next(97, 123);
            }

            return new string(array);
        }

        //[Benchmark]
        //public void TransodeNewOperationStatus()
        //{
        //    for (int i = 0; i < DataSize; i++)
        //    {
        //        Utf8.FromUtf16(_stringArrayValues[i].AsSpan(), _destination.AsSpan(), out _, out _);
        //    }
        //}

        [Benchmark]
        public void TransodeBuiltIn()
        {
            for (int i = 0; i < DataSize; i++)
            {
                Encoding.UTF8.GetBytes(_stringArrayValues[i].AsSpan(), _destination.AsSpan());
            }
        }

        [Benchmark(Baseline = true)]
        public void TransodeCustom()
        {
            for (int i = 0; i < DataSize; i++)
            {
                ToUtf8(MemoryMarshal.AsBytes(_stringArrayValues[i].AsSpan()), _destination.AsSpan(), out _, out _);
            }
        }

        // TODO: Replace this with publicly shipping implementation: https://github.com/dotnet/corefx/issues/34094
        /// <summary>
        /// Converts a span containing a sequence of UTF-16 bytes into UTF-8 bytes.
        ///
        /// This method will consume as many of the input bytes as possible.
        ///
        /// On successful exit, the entire input was consumed and encoded successfully. In this case, <paramref name="bytesConsumed"/> will be
        /// equal to the length of the <paramref name="utf16Source"/> and <paramref name="bytesWritten"/> will equal the total number of bytes written to
        /// the <paramref name="utf8Destination"/>.
        /// </summary>
        /// <param name="utf16Source">A span containing a sequence of UTF-16 bytes.</param>
        /// <param name="utf8Destination">A span to write the UTF-8 bytes into.</param>
        /// <param name="bytesConsumed">On exit, contains the number of bytes that were consumed from the <paramref name="utf16Source"/>.</param>
        /// <param name="bytesWritten">On exit, contains the number of bytes written to <paramref name="utf8Destination"/></param>
        /// <returns>A <see cref="OperationStatus"/> value representing the state of the conversion.</returns>
        private static unsafe OperationStatus ToUtf8(ReadOnlySpan<byte> utf16Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten)
        {
            //
            //
            // KEEP THIS IMPLEMENTATION IN SYNC WITH https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Text/UTF8Encoding.cs#L841
            //
            //
            fixed (byte* chars = &MemoryMarshal.GetReference(utf16Source))
            fixed (byte* bytes = &MemoryMarshal.GetReference(utf8Destination))
            {
                char* pSrc = (char*)chars;
                byte* pTarget = bytes;

                char* pEnd = (char*)(chars + utf16Source.Length);
                byte* pAllocatedBufferEnd = pTarget + utf8Destination.Length;

                // assume that JIT will enregister pSrc, pTarget and ch

                // Entering the fast encoding loop incurs some overhead that does not get amortized for small
                // number of characters, and the slow encoding loop typically ends up running for the last few
                // characters anyway since the fast encoding loop needs 5 characters on input at least.
                // Thus don't use the fast decoding loop at all if we don't have enough characters. The threashold
                // was choosen based on performance testing.
                // Note that if we don't have enough bytes, pStop will prevent us from entering the fast loop.
                while (pEnd - pSrc > 13)
                {
                    // we need at least 1 byte per character, but Convert might allow us to convert
                    // only part of the input, so try as much as we can.  Reduce charCount if necessary
                    int available = Math.Min(PtrDiff(pEnd, pSrc), PtrDiff(pAllocatedBufferEnd, pTarget));

                    // FASTLOOP:
                    // - optimistic range checks
                    // - fallbacks to the slow loop for all special cases, exception throwing, etc.

                    // To compute the upper bound, assume that all characters are ASCII characters at this point,
                    //  the boundary will be decreased for every non-ASCII character we encounter
                    // Also, we need 5 chars reserve for the unrolled ansi decoding loop and for decoding of surrogates
                    // If there aren't enough bytes for the output, then pStop will be <= pSrc and will bypass the loop.
                    char* pStop = pSrc + available - 5;
                    if (pSrc >= pStop)
                        break;

                    do
                    {
                        int ch = *pSrc;
                        pSrc++;

                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                        *pTarget = (byte)ch;
                        pTarget++;

                        // get pSrc aligned
                        if ((unchecked((int)pSrc) & 0x2) != 0)
                        {
                            ch = *pSrc;
                            pSrc++;
                            if (ch > 0x7F)
                            {
                                goto LongCode;
                            }
                            *pTarget = (byte)ch;
                            pTarget++;
                        }

                        // Run 4 characters at a time!
                        while (pSrc < pStop)
                        {
                            ch = *(int*)pSrc;
                            int chc = *(int*)(pSrc + 2);
                            if (((ch | chc) & unchecked((int)0xFF80FF80)) != 0)
                            {
                                goto LongCodeWithMask;
                            }

                            // Unfortunately, this is endianess sensitive
#if BIGENDIAN
                            *pTarget = (byte)(ch >> 16);
                            *(pTarget + 1) = (byte)ch;
                            pSrc += 4;
                            *(pTarget + 2) = (byte)(chc >> 16);
                            *(pTarget + 3) = (byte)chc;
                            pTarget += 4;
#else // BIGENDIAN
                            *pTarget = (byte)ch;
                            *(pTarget + 1) = (byte)(ch >> 16);
                            pSrc += 4;
                            *(pTarget + 2) = (byte)chc;
                            *(pTarget + 3) = (byte)(chc >> 16);
                            pTarget += 4;
#endif // BIGENDIAN
                        }
                        continue;

                    LongCodeWithMask:
#if BIGENDIAN
                        // be careful about the sign extension
                        ch = (int)(((uint)ch) >> 16);
#else // BIGENDIAN
                        ch = (char)ch;
#endif // BIGENDIAN
                        pSrc++;

                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                        *pTarget = (byte)ch;
                        pTarget++;
                        continue;

                    LongCode:
                        // use separate helper variables for slow and fast loop so that the jit optimizations
                        // won't get confused about the variable lifetimes
                        int chd;
                        if (ch <= 0x7FF)
                        {
                            // 2 byte encoding
                            chd = unchecked((sbyte)0xC0) | (ch >> 6);
                        }
                        else
                        {
                            // if (!IsLowSurrogate(ch) && !IsHighSurrogate(ch))
                            if (!IsInRangeInclusive(ch, JsonConstants.HighSurrogateStart, JsonConstants.LowSurrogateEnd))
                            {
                                // 3 byte encoding
                                chd = unchecked((sbyte)0xE0) | (ch >> 12);
                            }
                            else
                            {
                                // 4 byte encoding - high surrogate + low surrogate
                                // if (!IsHighSurrogate(ch))
                                if (ch > JsonConstants.HighSurrogateEnd)
                                {
                                    // low without high -> bad
                                    goto InvalidData;
                                }

                                chd = *pSrc;

                                // if (!IsLowSurrogate(chd)) {
                                if (!IsInRangeInclusive(chd, JsonConstants.LowSurrogateStart, JsonConstants.LowSurrogateEnd))
                                {
                                    // high not followed by low -> bad
                                    goto InvalidData;
                                }

                                pSrc++;

                                ch = chd + (ch << 10) +
                                    (0x10000
                                    - JsonConstants.LowSurrogateStart
                                    - (JsonConstants.HighSurrogateStart << 10));

                                *pTarget = (byte)(unchecked((sbyte)0xF0) | (ch >> 18));
                                // pStop - this byte is compensated by the second surrogate character
                                // 2 input chars require 4 output bytes.  2 have been anticipated already
                                // and 2 more will be accounted for by the 2 pStop-- calls below.
                                pTarget++;

                                chd = unchecked((sbyte)0x80) | (ch >> 12) & 0x3F;
                            }
                            *pTarget = (byte)chd;
                            pStop--;                    // 3 byte sequence for 1 char, so need pStop-- and the one below too.
                            pTarget++;

                            chd = unchecked((sbyte)0x80) | (ch >> 6) & 0x3F;
                        }
                        *pTarget = (byte)chd;
                        pStop--;                        // 2 byte sequence for 1 char so need pStop--.

                        *(pTarget + 1) = (byte)(unchecked((sbyte)0x80) | ch & 0x3F);
                        // pStop - this byte is already included

                        pTarget += 2;
                    }
                    while (pSrc < pStop);

                    Debug.Assert(pTarget <= pAllocatedBufferEnd, "[UTF8Encoding.GetBytes]pTarget <= pAllocatedBufferEnd");
                }

                while (pSrc < pEnd)
                {
                    // SLOWLOOP: does all range checks, handles all special cases, but it is slow

                    // read next char. The JIT optimization seems to be getting confused when
                    // compiling "ch = *pSrc++;", so rather use "ch = *pSrc; pSrc++;" instead
                    int ch = *pSrc;
                    pSrc++;

                    if (ch <= 0x7F)
                    {
                        if (pAllocatedBufferEnd - pTarget <= 0)
                            goto DestinationFull;

                        *pTarget = (byte)ch;
                        pTarget++;
                        continue;
                    }

                    int chd;
                    if (ch <= 0x7FF)
                    {
                        if (pAllocatedBufferEnd - pTarget <= 1)
                            goto DestinationFull;

                        // 2 byte encoding
                        chd = unchecked((sbyte)0xC0) | (ch >> 6);
                    }
                    else
                    {
                        // if (!IsLowSurrogate(ch) && !IsHighSurrogate(ch))
                        if (!IsInRangeInclusive(ch, JsonConstants.HighSurrogateStart, JsonConstants.LowSurrogateEnd))
                        {
                            if (pAllocatedBufferEnd - pTarget <= 2)
                                goto DestinationFull;

                            // 3 byte encoding
                            chd = unchecked((sbyte)0xE0) | (ch >> 12);
                        }
                        else
                        {
                            if (pAllocatedBufferEnd - pTarget <= 3)
                                goto DestinationFull;

                            // 4 byte encoding - high surrogate + low surrogate
                            // if (!IsHighSurrogate(ch))
                            if (ch > JsonConstants.HighSurrogateEnd)
                            {
                                // low without high -> bad
                                goto InvalidData;
                            }

                            if (pSrc >= pEnd)
                                goto NeedMoreData;

                            chd = *pSrc;

                            // if (!IsLowSurrogate(chd)) {
                            if (!IsInRangeInclusive(chd, JsonConstants.LowSurrogateStart, JsonConstants.LowSurrogateEnd))
                            {
                                // high not followed by low -> bad
                                goto InvalidData;
                            }

                            pSrc++;

                            ch = chd + (ch << 10) +
                                (0x10000
                                - JsonConstants.LowSurrogateStart
                                - (JsonConstants.HighSurrogateStart << 10));

                            *pTarget = (byte)(unchecked((sbyte)0xF0) | (ch >> 18));
                            pTarget++;

                            chd = unchecked((sbyte)0x80) | (ch >> 12) & 0x3F;
                        }
                        *pTarget = (byte)chd;
                        pTarget++;

                        chd = unchecked((sbyte)0x80) | (ch >> 6) & 0x3F;
                    }

                    *pTarget = (byte)chd;
                    *(pTarget + 1) = (byte)(unchecked((sbyte)0x80) | ch & 0x3F);

                    pTarget += 2;
                }

                bytesConsumed = (int)((byte*)pSrc - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.Done;

            InvalidData:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.InvalidData;

            DestinationFull:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.DestinationTooSmall;

            NeedMoreData:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.NeedMoreData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int PtrDiff(char* a, char* b)
        {
            return (int)(((uint)((byte*)a - (byte*)b)) >> 1);
        }

        // byte* flavor just for parity
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int PtrDiff(byte* a, byte* b)
        {
            return (int)(a - b);
        }

        internal static class JsonConstants
        {
            // Encoding Helpers
            public const char HighSurrogateStart = '\ud800';
            public const char HighSurrogateEnd = '\udbff';
            public const char LowSurrogateStart = '\udc00';
            public const char LowSurrogateEnd = '\udfff';
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="value"/> is between
        /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
            => (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);
    }

    public unsafe class TestEscapingWriter
    {
        //[Params(E.Default, E.Relaxed, E.Custom, E.Null)]
        [Params(E.Custom)]
        public E Encoder { get; set; }

        [Params(7)]
        public int DataLength { get; set; }

        public enum E
        {
            Default,
            Relaxed,
            Custom,
            Null
        }

        private string _source;
        private byte[] _sourceUtf8;
        private JavaScriptEncoder _encoder;

        private Utf8JsonWriter _writer;
        private ArrayBufferWriter<byte> _output;

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

            _encoder = null;
            switch (Encoder)
            {
                case E.Default:
                    _encoder = JavaScriptEncoder.Default;
                    break;
                case E.Relaxed:
                    _encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    break;
                case E.Custom:
                    _encoder = JavaScriptEncoder.Create(new Encodings.Web.TextEncoderSettings(UnicodeRanges.All));
                    break;
                case E.Null:
                    break;
            }

            _output = new ArrayBufferWriter<byte>();
            _writer = new Utf8JsonWriter(_output, new JsonWriterOptions { SkipValidation = true, Encoder = _encoder });
        }

        //[Params(-1)]
        //public int NegativeIndex { get; set; }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public void NeedsEscapingUtf16()
        {
            _output.Clear();
            for (int i = 0; i < 1_000; i++)
                _writer.WriteStringValue(_source);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingUtf8()
        {
            _output.Clear();
            for (int i = 0; i < 1_000; i++)
                _writer.WriteStringValue(_sourceUtf8);
        }
    }

    public unsafe class TestEscapingWriter_Reorder
    {
        private string[] _sources;
        private Utf8JsonWriter _writer;
        private ArrayBufferWriter<byte> _output;

        [GlobalSetup]
        public void Setup()
        {
            _sources = new string[1_000];
            var random = new Random(42);
            for (int j = 0; j < 1_000; j++)
            {
                int DataLength = random.Next(1, 16);
                var array = new char[DataLength];
                for (int i = 0; i < DataLength; i++)
                {
                    array[i] = (char)random.Next(97, 123);
                }
                _sources[j] = new string(array);
            }

            _output = new ArrayBufferWriter<byte>();
            _writer = new Utf8JsonWriter(_output, new JsonWriterOptions { SkipValidation = true });
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingCurrent()
        {
            _output.Clear();
            for (int i = 0; i < _sources.Length; i++)
                _writer.WriteStringValue(_sources[i]);
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 3)]
    public unsafe class Test_EscapingComparison_Reorder
    {
        private string[] _sources;
        private JavaScriptEncoder _encoder;

        [GlobalSetup]
        public void Setup()
        {
            _sources = new string[1_000];
            var random = new Random(42);

            for (int j = 0; j < 1_000; j++)
            {
                int DataLength = random.Next(1, 16);

                var array = new char[DataLength];
                for (int i = 0; i < DataLength; i++)
                {
                    array[i] = (char)random.Next(97, 123);
                }
                _sources[j] = new string(array);
            }
            _encoder = null;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public int NeedsEscapingNewFixed()
        {
            int result = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                result ^= NeedsEscaping_New_Fixed(_sources[i], _encoder);
            }
            return result;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int NeedsEscapingNewFixedReorder()
        {
            int result = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                result ^= NeedsEscaping_New_Fixed_Reorder(_sources[i], _encoder);
            }
            return result;
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

        public const int LastAsciiCharacter = 0x7F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsEscaping(char value) => value > LastAsciiCharacter || AllowList[value] == 0;

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

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 3)]
    public unsafe class Test_EscapingUnsafe
    {
        private string[] _sources;
        AllowedCharactersBitmap _allowed;

        [GlobalSetup]
        public void Setup()
        {
            _sources = new string[1_000];
            var random = new Random(42);

            for (int j = 0; j < 1_000; j++)
            {
                int DataLength = random.Next(1, 16);

                var array = new char[DataLength];
                for (int i = 0; i < DataLength; i++)
                {
                    array[i] = (char)random.Next(97, 123);
                }
                _sources[j] = new string(array);
            }

            var filter = new TextEncoderSettings(UnicodeRanges.All);

            _allowed = filter.GetAllowedCharacters();
            _allowed.ForbidUndefinedCharacters();
            _allowed.ForbidCharacter('\"');
            _allowed.ForbidCharacter('\\');
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public unsafe int Current()
        {
            int result = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                string s = _sources[i];
                fixed (char* ptr = s)
                {
                    result ^= _allowed.FindFirstCharacterToEncode(ptr, s.Length);
                }
            }
            return result;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public unsafe int New()
        {
            int result = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                string s = _sources[i];
                fixed (char* ptr = s)
                {
                    result ^= _allowed.FindFirstCharacterToEncode2(ptr, s.Length);
                }
            }
            return result;
        }

        public class TextEncoderSettings
        {
            private readonly AllowedCharactersBitmap _allowedCharactersBitmap;

            /// <summary>
            /// Instantiates an empty filter (allows no code points through by default).
            /// </summary>
            public TextEncoderSettings()
            {
                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
            }

            /// <summary>
            /// Instantiates the filter by cloning the allow list of another <see cref="TextEncoderSettings"/>.
            /// </summary>
            public TextEncoderSettings(TextEncoderSettings other)
            {
                if (other == null)
                {
                    throw new ArgumentNullException(nameof(other));
                }

                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
                AllowCodePoints(other.GetAllowedCodePoints());
            }

            /// <summary>
            /// Instantiates the filter where only the character ranges specified by <paramref name="allowedRanges"/>
            /// are allowed by the filter.
            /// </summary>
            public TextEncoderSettings(params UnicodeRange[] allowedRanges)
            {
                if (allowedRanges == null)
                {
                    throw new ArgumentNullException(nameof(allowedRanges));
                }
                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
                AllowRanges(allowedRanges);
            }

            /// <summary>
            /// Allows the character specified by <paramref name="character"/> through the filter.
            /// </summary>
            public virtual void AllowCharacter(char character)
            {
                _allowedCharactersBitmap.AllowCharacter(character);
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="characters"/> through the filter.
            /// </summary>
            public virtual void AllowCharacters(params char[] characters)
            {
                if (characters == null)
                {
                    throw new ArgumentNullException(nameof(characters));
                }

                for (int i = 0; i < characters.Length; i++)
                {
                    _allowedCharactersBitmap.AllowCharacter(characters[i]);
                }
            }

            /// <summary>
            /// Allows all code points specified by <paramref name="codePoints"/>.
            /// </summary>
            public virtual void AllowCodePoints(IEnumerable<int> codePoints)
            {
                if (codePoints == null)
                {
                    throw new ArgumentNullException(nameof(codePoints));
                }

                foreach (var allowedCodePoint in codePoints)
                {
                    // If the code point can't be represented as a BMP character, skip it.
                    char codePointAsChar = (char)allowedCodePoint;
                    if (allowedCodePoint == codePointAsChar)
                    {
                        _allowedCharactersBitmap.AllowCharacter(codePointAsChar);
                    }
                }
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="range"/> through the filter.
            /// </summary>
            public virtual void AllowRange(UnicodeRange range)
            {
                if (range == null)
                {
                    throw new ArgumentNullException(nameof(range));
                }

                int firstCodePoint = range.FirstCodePoint;
                int rangeSize = range.Length;
                for (int i = 0; i < rangeSize; i++)
                {
                    _allowedCharactersBitmap.AllowCharacter((char)(firstCodePoint + i));
                }
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="ranges"/> through the filter.
            /// </summary>
            public virtual void AllowRanges(params UnicodeRange[] ranges)
            {
                if (ranges == null)
                {
                    throw new ArgumentNullException(nameof(ranges));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    AllowRange(ranges[i]);
                }
            }

            /// <summary>
            /// Resets this settings object by disallowing all characters.
            /// </summary>
            public virtual void Clear()
            {
                _allowedCharactersBitmap.Clear();
            }

            /// <summary>
            /// Disallows the character <paramref name="character"/> through the filter.
            /// </summary>
            public virtual void ForbidCharacter(char character)
            {
                _allowedCharactersBitmap.ForbidCharacter(character);
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="characters"/> through the filter.
            /// </summary>
            public virtual void ForbidCharacters(params char[] characters)
            {
                if (characters == null)
                {
                    throw new ArgumentNullException(nameof(characters));
                }

                for (int i = 0; i < characters.Length; i++)
                {
                    _allowedCharactersBitmap.ForbidCharacter(characters[i]);
                }
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="range"/> through the filter.
            /// </summary>
            public virtual void ForbidRange(UnicodeRange range)
            {
                if (range == null)
                {
                    throw new ArgumentNullException(nameof(range));
                }

                int firstCodePoint = range.FirstCodePoint;
                int rangeSize = range.Length;
                for (int i = 0; i < rangeSize; i++)
                {
                    _allowedCharactersBitmap.ForbidCharacter((char)(firstCodePoint + i));
                }
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="ranges"/> through the filter.
            /// </summary>
            public virtual void ForbidRanges(params UnicodeRange[] ranges)
            {
                if (ranges == null)
                {
                    throw new ArgumentNullException(nameof(ranges));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    ForbidRange(ranges[i]);
                }
            }

            /// <summary>
            /// Retrieves the bitmap of allowed characters from this settings object.
            /// The returned bitmap is a clone of the original bitmap to avoid unintentional modification.
            /// </summary>
            internal AllowedCharactersBitmap GetAllowedCharacters()
            {
                return _allowedCharactersBitmap.Clone();
            }

            /// <summary>
            /// Gets an enumeration of all allowed code points.
            /// </summary>
            public virtual IEnumerable<int> GetAllowedCodePoints()
            {
                for (int i = 0; i < 0x10000; i++)
                {
                    if (_allowedCharactersBitmap.IsCharacterAllowed((char)i))
                    {
                        yield return i;
                    }
                }
            }
        }

        internal readonly struct AllowedCharactersBitmap
        {
            private const int ALLOWED_CHARS_BITMAP_LENGTH = 0x10000 / (8 * sizeof(uint));
            private readonly uint[] _allowedCharacters;

            // should be called in place of the default ctor
            public static AllowedCharactersBitmap CreateNew()
            {
                return new AllowedCharactersBitmap(new uint[ALLOWED_CHARS_BITMAP_LENGTH]);
            }

            private AllowedCharactersBitmap(uint[] allowedCharacters)
            {
                if (allowedCharacters == null)
                {
                    throw new ArgumentNullException(nameof(allowedCharacters));
                }
                _allowedCharacters = allowedCharacters;
            }

            // Marks a character as allowed (can be returned unencoded)
            public void AllowCharacter(char character)
            {
                int codePoint = character;
                int index = codePoint >> 5;
                int offset = codePoint & 0x1F;
                _allowedCharacters[index] |= 0x1U << offset;
            }

            // Marks a character as forbidden (must be returned encoded)
            public void ForbidCharacter(char character)
            {
                int codePoint = character;
                int index = codePoint >> 5;
                int offset = codePoint & 0x1F;
                _allowedCharacters[index] &= ~(0x1U << offset);
            }

            // Forbid codepoints which aren't mapped to characters or which are otherwise always disallowed
            // (includes categories Cc, Cs, Co, Cn, Zs [except U+0020 SPACE], Zl, Zp)
            public void ForbidUndefinedCharacters()
            {
                ReadOnlySpan<uint> definedCharactersBitmap = GetDefinedCharacterBitmap();
                Debug.Assert(definedCharactersBitmap.Length == _allowedCharacters.Length);
                for (int i = 0; i < _allowedCharacters.Length; i++)
                {
                    _allowedCharacters[i] &= definedCharactersBitmap[i];
                }
            }

            // This field is only used on big-endian architectures. We don't
            // bother computing it on little-endian architectures.
            private static readonly uint[] _definedCharacterBitmapBigEndian = (BitConverter.IsLittleEndian) ? null : CreateDefinedCharacterBitmapMachineEndian();

            private static uint[] CreateDefinedCharacterBitmapMachineEndian()
            {
                Debug.Assert(!BitConverter.IsLittleEndian);

                // We need to convert little-endian to machine-endian.

                ReadOnlySpan<byte> remainingBitmap = DefinedCharsBitmapSpan;
                uint[] bigEndianData = new uint[remainingBitmap.Length / sizeof(uint)];

                for (int i = 0; i < bigEndianData.Length; i++)
                {
                    bigEndianData[i] = BinaryPrimitives.ReadUInt32LittleEndian(remainingBitmap);
                    remainingBitmap = remainingBitmap.Slice(sizeof(uint));
                }

                return bigEndianData;
            }

            /// <summary>
            /// Returns a bitmap of all characters which are defined per the checked-in version
            /// of the Unicode specification.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ReadOnlySpan<uint> GetDefinedCharacterBitmap()
            {
                if (BitConverter.IsLittleEndian)
                {
                    // Underlying data is a series of 32-bit little-endian values and is guaranteed
                    // properly aligned by the compiler, so we know this is a valid cast byte -> uint.

                    return MemoryMarshal.Cast<byte, uint>(DefinedCharsBitmapSpan);
                }
                else
                {
                    // Static compiled data was little-endian; we had to create a big-endian
                    // representation at runtime.

                    return _definedCharacterBitmapBigEndian;
                }
            }

            private static ReadOnlySpan<byte> DefinedCharsBitmapSpan => new byte[0x2000]
        {
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, // U+0000..U+007F
            0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0080..U+00FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0100..U+017F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0180..U+01FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0200..U+027F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0280..U+02FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFC, // U+0300..U+037F
            0xF0, 0xD7, 0xFF, 0xFF, 0xFB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0380..U+03FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0400..U+047F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0480..U+04FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0x7F, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, // U+0500..U+057F
            0xFF, 0xE7, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0x87, 0x1F, 0x00, // U+0580..U+05FF
            0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0600..U+067F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0680..U+06FF
            0xFF, 0xBF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0700..U+077F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, // U+0780..U+07FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x4F, 0xFF, 0x07, 0x00, 0x00, // U+0800..U+087F
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xDF, 0x3F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0880..U+08FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0900..U+097F
            0xEF, 0x9F, 0xF9, 0xFF, 0xFF, 0xFD, 0xC5, 0xF3, 0x9F, 0x79, 0x80, 0xB0, 0xCF, 0xFF, 0xFF, 0x7F, // U+0980..U+09FF
            0xEE, 0x87, 0xF9, 0xFF, 0xFF, 0xFD, 0x6D, 0xD3, 0x87, 0x39, 0x02, 0x5E, 0xC0, 0xFF, 0x7F, 0x00, // U+0A00..U+0A7F
            0xEE, 0xBF, 0xFB, 0xFF, 0xFF, 0xFD, 0xED, 0xF3, 0xBF, 0x3B, 0x01, 0x00, 0xCF, 0xFF, 0x03, 0xFE, // U+0A80..U+0AFF
            0xEE, 0x9F, 0xF9, 0xFF, 0xFF, 0xFD, 0xED, 0xF3, 0x9F, 0x39, 0xC0, 0xB0, 0xCF, 0xFF, 0xFF, 0x00, // U+0B00..U+0B7F
            0xEC, 0xC7, 0x3D, 0xD6, 0x18, 0xC7, 0xFF, 0xC3, 0xC7, 0x3D, 0x81, 0x00, 0xC0, 0xFF, 0xFF, 0x07, // U+0B80..U+0BFF
            0xFF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFD, 0xFF, 0xE3, 0xDF, 0x3D, 0x60, 0x07, 0xCF, 0xFF, 0x80, 0xFF, // U+0C00..U+0C7F
            0xFF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFD, 0xEF, 0xF3, 0xDF, 0x3D, 0x60, 0x40, 0xCF, 0xFF, 0x06, 0x00, // U+0C80..U+0CFF
            0xEF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xDF, 0xFD, 0xF0, 0xFF, 0xCF, 0xFF, 0xFF, 0xFF, // U+0D00..U+0D7F
            0xEC, 0xFF, 0x7F, 0xFC, 0xFF, 0xFF, 0xFB, 0x2F, 0x7F, 0x84, 0x5F, 0xFF, 0xC0, 0xFF, 0x1C, 0x00, // U+0D80..U+0DFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x87, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0x00, 0x00, 0x00, // U+0E00..U+0E7F
            0xD6, 0xF7, 0xFF, 0xFF, 0xAF, 0xFF, 0xFF, 0x3F, 0x5F, 0x3F, 0xFF, 0xF3, 0x00, 0x00, 0x00, 0x00, // U+0E80..U+0EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0x1F, 0xFE, 0xFF, // U+0F00..U+0F7F
            0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0x07, 0x00, 0x00, 0x00, 0x00, // U+0F80..U+0FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1000..U+107F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1080..U+10FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1100..U+117F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1180..U+11FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3D, 0x7F, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, // U+1200..U+127F
            0xFF, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0x3D, 0x7F, 0x3D, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1280..U+12FF
            0xFF, 0xFF, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0xFF, 0xFF, 0x1F, // U+1300..U+137F
            0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x3F, // U+1380..U+13FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1400..U+147F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1480..U+14FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1500..U+157F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1580..U+15FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1600..U+167F
            0xFE, 0xFF, 0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, // U+1680..U+16FF
            0xFF, 0xDF, 0x1F, 0x00, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xDF, 0x0D, 0x00, // U+1700..U+177F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x03, 0xFF, 0x03, // U+1780..U+17FF
            0xFF, 0x7F, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, // U+1800..U+187F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, // U+1880..U+18FF
            0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0x0F, 0xFF, 0x0F, 0xF1, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x1F, 0x00, // U+1900..U+197F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xC7, 0xFF, 0xFF, 0xFF, 0xFF, // U+1980..U+19FF
            0xFF, 0xFF, 0xFF, 0xCF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x9F, // U+1A00..U+1A7F
            0xFF, 0x03, 0xFF, 0x03, 0xFF, 0x3F, 0xFF, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+1A80..U+1AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F, // U+1B00..U+1B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xF0, // U+1B80..U+1BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xF8, 0xFF, 0xE3, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1C00..U+1C7F
            0xFF, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, // U+1C80..U+1CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1D00..U+1D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFB, // U+1D80..U+1DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1E00..U+1E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1E80..U+1EFF
            0xFF, 0xFF, 0x3F, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x3F, 0xFF, 0xAA, 0xFF, 0xFF, 0xFF, 0x3F, // U+1F00..U+1F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xCF, 0xEF, 0xFF, 0xFF, 0xDC, 0x7F, // U+1F80..U+1FFF
            0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0x7C, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xDF, 0xFF, 0xF3, 0xFF, // U+2000..U+207F
            0xFF, 0x7F, 0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x00, // U+2080..U+20FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2100..U+217F
            0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2180..U+21FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2200..U+227F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2280..U+22FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2300..U+237F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2380..U+23FF
            0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0x00, 0x00, 0xFF, 0x07, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, // U+2400..U+247F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2480..U+24FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2500..U+257F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2580..U+25FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2600..U+267F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2680..U+26FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2700..U+277F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2780..U+27FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2800..U+287F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2880..U+28FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2900..U+297F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2980..U+29FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2A00..U+2A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2A80..U+2AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xFF, // U+2B00..U+2B7F
            0xFF, 0xFF, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2B80..U+2BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, // U+2C00..U+2C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFE, // U+2C80..U+2CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x80, 0x01, 0x80, // U+2D00..U+2D7F
            0xFF, 0xFF, 0x7F, 0x00, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, // U+2D80..U+2DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+2E00..U+2E7F
            0xFF, 0xFF, 0xFF, 0xFB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, // U+2E80..U+2EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2F00..U+2F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, 0x00, 0x00, 0xFF, 0x0F, // U+2F80..U+2FFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3000..U+307F
            0xFF, 0xFF, 0x7F, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3080..U+30FF
            0xE0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3100..U+317F
            0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xFF, // U+3180..U+31FF
            0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3200..U+327F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3280..U+32FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3300..U+337F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3380..U+33FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3400..U+347F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3480..U+34FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3500..U+357F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3580..U+35FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3600..U+367F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3680..U+36FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3700..U+377F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3780..U+37FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3800..U+387F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3880..U+38FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3900..U+397F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3980..U+39FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3A00..U+3A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3A80..U+3AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3B00..U+3B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3B80..U+3BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3C00..U+3C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3C80..U+3CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3D00..U+3D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3D80..U+3DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3E00..U+3E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3E80..U+3EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3F00..U+3F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3F80..U+3FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4000..U+407F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4080..U+40FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4100..U+417F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4180..U+41FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4200..U+427F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4280..U+42FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4300..U+437F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4380..U+43FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4400..U+447F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4480..U+44FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4500..U+457F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4580..U+45FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4600..U+467F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4680..U+46FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4700..U+477F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4780..U+47FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4800..U+487F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4880..U+48FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4900..U+497F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4980..U+49FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4A00..U+4A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4A80..U+4AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4B00..U+4B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4B80..U+4BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4C00..U+4C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4C80..U+4CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4D00..U+4D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4D80..U+4DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4E00..U+4E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4E80..U+4EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4F00..U+4F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4F80..U+4FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5000..U+507F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5080..U+50FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5100..U+517F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5180..U+51FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5200..U+527F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5280..U+52FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5300..U+537F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5380..U+53FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5400..U+547F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5480..U+54FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5500..U+557F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5580..U+55FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5600..U+567F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5680..U+56FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5700..U+577F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5780..U+57FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5800..U+587F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5880..U+58FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5900..U+597F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5980..U+59FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5A00..U+5A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5A80..U+5AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5B00..U+5B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5B80..U+5BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5C00..U+5C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5C80..U+5CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5D00..U+5D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5D80..U+5DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5E00..U+5E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5E80..U+5EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5F00..U+5F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5F80..U+5FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6000..U+607F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6080..U+60FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6100..U+617F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6180..U+61FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6200..U+627F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6280..U+62FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6300..U+637F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6380..U+63FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6400..U+647F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6480..U+64FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6500..U+657F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6580..U+65FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6600..U+667F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6680..U+66FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6700..U+677F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6780..U+67FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6800..U+687F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6880..U+68FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6900..U+697F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6980..U+69FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6A00..U+6A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6A80..U+6AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6B00..U+6B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6B80..U+6BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6C00..U+6C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6C80..U+6CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6D00..U+6D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6D80..U+6DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6E00..U+6E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6E80..U+6EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6F00..U+6F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6F80..U+6FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7000..U+707F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7080..U+70FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7100..U+717F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7180..U+71FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7200..U+727F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7280..U+72FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7300..U+737F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7380..U+73FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7400..U+747F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7480..U+74FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7500..U+757F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7580..U+75FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7600..U+767F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7680..U+76FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7700..U+777F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7780..U+77FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7800..U+787F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7880..U+78FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7900..U+797F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7980..U+79FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7A00..U+7A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7A80..U+7AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7B00..U+7B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7B80..U+7BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7C00..U+7C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7C80..U+7CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7D00..U+7D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7D80..U+7DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7E00..U+7E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7E80..U+7EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7F00..U+7F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7F80..U+7FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8000..U+807F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8080..U+80FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8100..U+817F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8180..U+81FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8200..U+827F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8280..U+82FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8300..U+837F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8380..U+83FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8400..U+847F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8480..U+84FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8500..U+857F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8580..U+85FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8600..U+867F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8680..U+86FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8700..U+877F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8780..U+87FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8800..U+887F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8880..U+88FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8900..U+897F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8980..U+89FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8A00..U+8A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8A80..U+8AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8B00..U+8B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8B80..U+8BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8C00..U+8C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8C80..U+8CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8D00..U+8D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8D80..U+8DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8E00..U+8E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8E80..U+8EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8F00..U+8F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8F80..U+8FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9000..U+907F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9080..U+90FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9100..U+917F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9180..U+91FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9200..U+927F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9280..U+92FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9300..U+937F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9380..U+93FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9400..U+947F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9480..U+94FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9500..U+957F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9580..U+95FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9600..U+967F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9680..U+96FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9700..U+977F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9780..U+97FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9800..U+987F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9880..U+98FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9900..U+997F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9980..U+99FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9A00..U+9A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9A80..U+9AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9B00..U+9B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9B80..U+9BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9C00..U+9C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9C80..U+9CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9D00..U+9D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9D80..U+9DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9E00..U+9E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9E80..U+9EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9F00..U+9F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, // U+9F80..U+9FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A000..U+A07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A080..U+A0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A100..U+A17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A180..U+A1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A200..U+A27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A280..U+A2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A300..U+A37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A380..U+A3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A400..U+A47F
            0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A480..U+A4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A500..U+A57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A580..U+A5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A600..U+A67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, // U+A680..U+A6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A700..U+A77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0xFF, // U+A780..U+A7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, // U+A800..U+A87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xC0, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, // U+A880..U+A8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x80, 0xFF, 0xFF, 0xFF, 0x1F, // U+A900..U+A97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0xFF, 0xC3, 0xFF, 0xFF, 0xFF, 0x7F, // U+A980..U+A9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0x3F, 0xFF, 0xF3, 0xFF, 0xFF, 0xFF, 0xFF, // U+AA00..U+AA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x7F, 0x00, // U+AA80..U+AAFF
            0x7E, 0x7E, 0x7E, 0x00, 0x7F, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0xFF, // U+AB00..U+AB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x03, // U+AB80..U+ABFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AC00..U+AC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AC80..U+ACFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AD00..U+AD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AD80..U+ADFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AE00..U+AE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AE80..U+AEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AF00..U+AF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AF80..U+AFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B000..U+B07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B080..U+B0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B100..U+B17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B180..U+B1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B200..U+B27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B280..U+B2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B300..U+B37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B380..U+B3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B400..U+B47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B480..U+B4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B500..U+B57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B580..U+B5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B600..U+B67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B680..U+B6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B700..U+B77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B780..U+B7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B800..U+B87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B880..U+B8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B900..U+B97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B980..U+B9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BA00..U+BA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BA80..U+BAFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BB00..U+BB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BB80..U+BBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BC00..U+BC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BC80..U+BCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BD00..U+BD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BD80..U+BDFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BE00..U+BE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BE80..U+BEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BF00..U+BF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BF80..U+BFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C000..U+C07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C080..U+C0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C100..U+C17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C180..U+C1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C200..U+C27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C280..U+C2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C300..U+C37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C380..U+C3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C400..U+C47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C480..U+C4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C500..U+C57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C580..U+C5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C600..U+C67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C680..U+C6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C700..U+C77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C780..U+C7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C800..U+C87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C880..U+C8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C900..U+C97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C980..U+C9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CA00..U+CA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CA80..U+CAFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CB00..U+CB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CB80..U+CBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CC00..U+CC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CC80..U+CCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CD00..U+CD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CD80..U+CDFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CE00..U+CE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CE80..U+CEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CF00..U+CF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CF80..U+CFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D000..U+D07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D080..U+D0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D100..U+D17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D180..U+D1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D200..U+D27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D280..U+D2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D300..U+D37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D380..U+D3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D400..U+D47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D480..U+D4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D500..U+D57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D580..U+D5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D600..U+D67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D680..U+D6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D700..U+D77F
            0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xFF, 0x7F, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, // U+D780..U+D7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D800..U+D87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D880..U+D8FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D900..U+D97F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D980..U+D9FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DA00..U+DA7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DA80..U+DAFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DB00..U+DB7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DB80..U+DBFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DC00..U+DC7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DC80..U+DCFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DD00..U+DD7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DD80..U+DDFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DE00..U+DE7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DE80..U+DEFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DF00..U+DF7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DF80..U+DFFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E000..U+E07F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E080..U+E0FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E100..U+E17F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E180..U+E1FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E200..U+E27F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E280..U+E2FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E300..U+E37F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E380..U+E3FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E400..U+E47F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E480..U+E4FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E500..U+E57F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E580..U+E5FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E600..U+E67F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E680..U+E6FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E700..U+E77F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E780..U+E7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E800..U+E87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E880..U+E8FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E900..U+E97F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E980..U+E9FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EA00..U+EA7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EA80..U+EAFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EB00..U+EB7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EB80..U+EBFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EC00..U+EC7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EC80..U+ECFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+ED00..U+ED7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+ED80..U+EDFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EE00..U+EE7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EE80..U+EEFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EF00..U+EF7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EF80..U+EFFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F000..U+F07F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F080..U+F0FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F100..U+F17F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F180..U+F1FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F200..U+F27F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F280..U+F2FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F300..U+F37F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F380..U+F3FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F400..U+F47F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F480..U+F4FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F500..U+F57F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F580..U+F5FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F600..U+F67F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F680..U+F6FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F700..U+F77F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F780..U+F7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F800..U+F87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F880..U+F8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+F900..U+F97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+F980..U+F9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0xFF, // U+FA00..U+FA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0x00, 0x00, 0x00, // U+FA80..U+FAFF
            0x7F, 0x00, 0xF8, 0xE0, 0xFF, 0xFF, 0x7F, 0x5F, 0xDB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FB00..U+FB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FB80..U+FBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FC00..U+FC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FC80..U+FCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FD00..U+FD7F
            0xFF, 0xFF, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x3F, // U+FD80..U+FDFF
            0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xF7, 0xFF, 0x7F, 0x0F, 0xDF, 0xFF, // U+FE00..U+FE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F, // U+FE80..U+FEFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FF00..U+FF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFC, 0xFC, 0xFC, 0x1C, 0x7F, 0x7F, 0x00, 0x3E, // U+FF80..U+FFFF
        };

            // Marks all characters as forbidden (must be returned encoded)
            public void Clear()
            {
                Array.Clear(_allowedCharacters, 0, _allowedCharacters.Length);
            }

            // Creates a deep copy of this bitmap
            public AllowedCharactersBitmap Clone()
            {
                return new AllowedCharactersBitmap((uint[])_allowedCharacters.Clone());
            }

            // Determines whether the given character can be returned unencoded.
            public bool IsCharacterAllowed(char character)
            {
                return IsUnicodeScalarAllowed(character);
            }

            // Determines whether the given character can be returned unencoded.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsUnicodeScalarAllowed(int unicodeScalar)
            {
                Debug.Assert(unicodeScalar < 0x10000);
                int index = unicodeScalar >> 5;
                int offset = unicodeScalar & 0x1F;
                return (_allowedCharacters[index] & (0x1U << offset)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int FindFirstCharacterToEncode(char* text, int textLength)
            {
                int i = 0;

                while (i <= textLength - 8)
                {
                    if (!IsCharacterAllowed(text[i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i]))
                    {
                        goto Return;
                    }
                    i++;
                }

                while (i <= textLength - 4)
                {
                    if (!IsCharacterAllowed(text[i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i])
                        || !IsCharacterAllowed(text[++i]))
                    {
                        goto Return;
                    }
                    i++;
                }

                while (i < textLength)
                {
                    if (!IsCharacterAllowed(text[i]))
                    {
                        goto Return;
                    }
                    i++;
                }

                i = -1;

            Return:
                return i;
            }

            private static readonly Vector128<short> s_mask_UInt16_0x00 = Vector128.Create((short)0x00); // Null
            private static readonly Vector128<short> s_mask_UInt16_0xFF = Vector128.Create((short)0xFF); // LATIN SMALL LETTER Y WITH DIAERESIS 'ÿ'

            private static readonly Vector128<short> s_mask_UInt16_0x20 = Vector128.Create((short)0x20); // Space ' '

            private static readonly Vector128<short> s_mask_UInt16_0x22 = Vector128.Create((short)0x22); // Quotation Mark '"'
            private static readonly Vector128<short> s_mask_UInt16_0x26 = Vector128.Create((short)0x26); // Ampersand '&'
            private static readonly Vector128<short> s_mask_UInt16_0x27 = Vector128.Create((short)0x27); // Apostrophe '''
            private static readonly Vector128<short> s_mask_UInt16_0x2B = Vector128.Create((short)0x2B); // Plus sign '+'
            private static readonly Vector128<short> s_mask_UInt16_0x3C = Vector128.Create((short)0x3C); // Less Than Sign '<'
            private static readonly Vector128<short> s_mask_UInt16_0x3E = Vector128.Create((short)0x3E); // Greater Than Sign '>'
            private static readonly Vector128<short> s_mask_UInt16_0x5C = Vector128.Create((short)0x5C); // Reverse Solidus '\'
            private static readonly Vector128<short> s_mask_UInt16_0x60 = Vector128.Create((short)0x60); // Grave Access '`'

            private static readonly Vector128<short> s_mask_UInt16_0x7E = Vector128.Create((short)0x7E); // Tilde '~'

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector128<short> CreateEscapingMask(Vector128<short> sourceValue)
            {
                Debug.Assert(Sse2.IsSupported);

                Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x20); // Space ' ', anything in the control characters range

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x22)); // Quotation Mark '"'
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x5C)); // Reverse Solidus '\'

                return mask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindFirstCharacterToEncode2(char* ptr, int textLength)
            {
                int idx = 0;

                if (Sse2.IsSupported)
                {
                    short* startingAddress = (short*)ptr;
                    while (textLength - 8 >= idx)
                    {
                        Debug.Assert(startingAddress >= ptr && startingAddress <= (ptr + textLength - 8));

                        // Load the next 8 characters.
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x00); // Null anything above short.MaxValue but less than char.MaxValue
                        mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, s_mask_UInt16_0x7E)); // Tilde '~', anything above the ASCII range
                        int index = Sse2.MoveMask(mask.AsByte());
                        if (index != 0)
                        {
                            int processNextEight = idx + 8;
                            for (; idx < processNextEight; idx++)
                            {
                                Debug.Assert((ptr + idx) <= (ptr + textLength));
                                if (!IsCharacterAllowed(*(ptr + idx)))
                                {
                                    goto Return;
                                }
                            }
                        }
                        else
                        {
                            // Check if any of the 8 characters need to be escaped.
                            mask = CreateEscapingMask(sourceValue);

                            index = Sse2.MoveMask(mask.AsByte());
                            // If index == 0, that means none of the 8 characters needed to be escaped.
                            // TrailingZeroCount is relatively expensive, avoid it if possible.
                            if (index != 0)
                            {
                                // Found at least one character that needs to be escaped, figure out the index of
                                // the first one found that needed to be escaped within the 8 characters.
                                idx += BitOperations.TrailingZeroCount(index) >> 1;
                                goto Return;
                            }
                            idx += 8;
                            startingAddress += 8;
                        }
                    }

                    // Process the remaining characters.
                    Debug.Assert(textLength - idx < 8);
                }

                for (; idx < textLength; idx++)
                {
                    Debug.Assert((ptr + idx) <= (ptr + textLength));
                    if (!IsCharacterAllowed(*(ptr + idx)))
                    {
                        goto Return;
                    }
                }

                idx = -1; // All characters are allowed.

            Return:
                return idx;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool NeedsEscaping(char value) => AllowList[value] == 0;

            private static ReadOnlySpan<byte> AllowList => new byte[byte.MaxValue + 1]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0000..U+000F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0010..U+001F
                1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0020..U+002F   "
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0030..U+003F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0040..U+004F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, // U+0050..U+005F   \
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0060..U+006F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, // U+0070..U+007F

                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0080..U+008F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0090..U+009F
                0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00A0..U+00AF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00B0..U+00BF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00C0..U+00CF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00D0..U+00DF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00E0..U+00EF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00F0..U+00FF
            };
        }
    }

    //[DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 3)]
    public unsafe class Test_EscapingUnsafe_Single
    {
        //private string _source;
        AllowedCharactersBitmap _allowed;

        //[Params(1, 2, 3, 4, 5, 6, 7, 8, 15, 16, 17)]
        //public int DataLength { get; set; }

        [ParamsSource(nameof(JsonStringData))]
        public (int Length, int EscapeCharIndex, byte[] JsonString) TestStringData { get; set; }

        private static readonly int[] dataLengths = new int[] { 16 };

        private static readonly int[] subsetToAddEscapedCharacters = new int[] { 16 };

        public static IEnumerable<(int, int, byte[])> JsonStringData()
        {
            var random = new Random(42);

            for (int j = 0; j < dataLengths.Length; j++)
            {
                int dataLength = dataLengths[j];
                var array = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    array[i] = (char)random.Next(97, 123);
                    //array[i] = (char)0xD0;
                }
                yield return (dataLength, -1, Encoding.UTF8.GetBytes(new string(array))); // No character requires escaping

                if (subsetToAddEscapedCharacters.Contains(dataLength))
                {
                    for (int i = 0; i < dataLength; i++)
                    {
                        char currentChar = array[i];
                        array[i] = '\\';
                        yield return (dataLength, i, Encoding.UTF8.GetBytes(new string(array))); // One character requires escaping at index i
                        array[i] = currentChar;
                    }
                }

            }
        }

        [GlobalSetup]
        public void Setup()
        {
            //var random = new Random(42);
            //var array = new char[DataLength];
            //for (int i = 0; i < DataLength; i++)
            //{
            //    array[i] = (char)random.Next(97, 123);
            //}
            //_source = new string(array);

            var filter = new TextEncoderSettings(UnicodeRanges.All);

            _allowed = filter.GetAllowedCharacters();
            _allowed.ForbidUndefinedCharacters();
            _allowed.ForbidCharacter('\"');
            _allowed.ForbidCharacter('\\');
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public unsafe int Current()
        {
            //int result = 0;
            //fixed (char* ptr = TestStringData.JsonString)
            //{
            //    result ^= _allowed.FindFirstCharacterToEncode(ptr, TestStringData.JsonString.Length);
            //}
            //return result;
            return _allowed.FindFirstCharacterToEncodeUtf8(TestStringData.JsonString);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public unsafe int New()
        {
            //int result = 0;
            //fixed (char* ptr = TestStringData.JsonString)
            //{
            //    result ^= _allowed.FindFirstCharacterToEncode2(ptr, TestStringData.JsonString.Length);
            //}
            //return result;
            return _allowed.FindFirstCharacterToEncodeUtf82(TestStringData.JsonString);
        }

        public class TextEncoderSettings
        {
            private readonly AllowedCharactersBitmap _allowedCharactersBitmap;

            /// <summary>
            /// Instantiates an empty filter (allows no code points through by default).
            /// </summary>
            public TextEncoderSettings()
            {
                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
            }

            /// <summary>
            /// Instantiates the filter by cloning the allow list of another <see cref="TextEncoderSettings"/>.
            /// </summary>
            public TextEncoderSettings(TextEncoderSettings other)
            {
                if (other == null)
                {
                    throw new ArgumentNullException(nameof(other));
                }

                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
                AllowCodePoints(other.GetAllowedCodePoints());
            }

            /// <summary>
            /// Instantiates the filter where only the character ranges specified by <paramref name="allowedRanges"/>
            /// are allowed by the filter.
            /// </summary>
            public TextEncoderSettings(params UnicodeRange[] allowedRanges)
            {
                if (allowedRanges == null)
                {
                    throw new ArgumentNullException(nameof(allowedRanges));
                }
                _allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
                AllowRanges(allowedRanges);
            }

            /// <summary>
            /// Allows the character specified by <paramref name="character"/> through the filter.
            /// </summary>
            public virtual void AllowCharacter(char character)
            {
                _allowedCharactersBitmap.AllowCharacter(character);
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="characters"/> through the filter.
            /// </summary>
            public virtual void AllowCharacters(params char[] characters)
            {
                if (characters == null)
                {
                    throw new ArgumentNullException(nameof(characters));
                }

                for (int i = 0; i < characters.Length; i++)
                {
                    _allowedCharactersBitmap.AllowCharacter(characters[i]);
                }
            }

            /// <summary>
            /// Allows all code points specified by <paramref name="codePoints"/>.
            /// </summary>
            public virtual void AllowCodePoints(IEnumerable<int> codePoints)
            {
                if (codePoints == null)
                {
                    throw new ArgumentNullException(nameof(codePoints));
                }

                foreach (var allowedCodePoint in codePoints)
                {
                    // If the code point can't be represented as a BMP character, skip it.
                    char codePointAsChar = (char)allowedCodePoint;
                    if (allowedCodePoint == codePointAsChar)
                    {
                        _allowedCharactersBitmap.AllowCharacter(codePointAsChar);
                    }
                }
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="range"/> through the filter.
            /// </summary>
            public virtual void AllowRange(UnicodeRange range)
            {
                if (range == null)
                {
                    throw new ArgumentNullException(nameof(range));
                }

                int firstCodePoint = range.FirstCodePoint;
                int rangeSize = range.Length;
                for (int i = 0; i < rangeSize; i++)
                {
                    _allowedCharactersBitmap.AllowCharacter((char)(firstCodePoint + i));
                }
            }

            /// <summary>
            /// Allows all characters specified by <paramref name="ranges"/> through the filter.
            /// </summary>
            public virtual void AllowRanges(params UnicodeRange[] ranges)
            {
                if (ranges == null)
                {
                    throw new ArgumentNullException(nameof(ranges));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    AllowRange(ranges[i]);
                }
            }

            /// <summary>
            /// Resets this settings object by disallowing all characters.
            /// </summary>
            public virtual void Clear()
            {
                _allowedCharactersBitmap.Clear();
            }

            /// <summary>
            /// Disallows the character <paramref name="character"/> through the filter.
            /// </summary>
            public virtual void ForbidCharacter(char character)
            {
                _allowedCharactersBitmap.ForbidCharacter(character);
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="characters"/> through the filter.
            /// </summary>
            public virtual void ForbidCharacters(params char[] characters)
            {
                if (characters == null)
                {
                    throw new ArgumentNullException(nameof(characters));
                }

                for (int i = 0; i < characters.Length; i++)
                {
                    _allowedCharactersBitmap.ForbidCharacter(characters[i]);
                }
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="range"/> through the filter.
            /// </summary>
            public virtual void ForbidRange(UnicodeRange range)
            {
                if (range == null)
                {
                    throw new ArgumentNullException(nameof(range));
                }

                int firstCodePoint = range.FirstCodePoint;
                int rangeSize = range.Length;
                for (int i = 0; i < rangeSize; i++)
                {
                    _allowedCharactersBitmap.ForbidCharacter((char)(firstCodePoint + i));
                }
            }

            /// <summary>
            /// Disallows all characters specified by <paramref name="ranges"/> through the filter.
            /// </summary>
            public virtual void ForbidRanges(params UnicodeRange[] ranges)
            {
                if (ranges == null)
                {
                    throw new ArgumentNullException(nameof(ranges));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    ForbidRange(ranges[i]);
                }
            }

            /// <summary>
            /// Retrieves the bitmap of allowed characters from this settings object.
            /// The returned bitmap is a clone of the original bitmap to avoid unintentional modification.
            /// </summary>
            internal AllowedCharactersBitmap GetAllowedCharacters()
            {
                return _allowedCharactersBitmap.Clone();
            }

            /// <summary>
            /// Gets an enumeration of all allowed code points.
            /// </summary>
            public virtual IEnumerable<int> GetAllowedCodePoints()
            {
                for (int i = 0; i < 0x10000; i++)
                {
                    if (_allowedCharactersBitmap.IsCharacterAllowed((char)i))
                    {
                        yield return i;
                    }
                }
            }
        }

        internal class AllowedCharactersBitmap
        {
            private const int ALLOWED_CHARS_BITMAP_LENGTH = 0x10000 / (8 * sizeof(uint));
            private readonly uint[] _allowedCharacters;

            // should be called in place of the default ctor
            public static AllowedCharactersBitmap CreateNew()
            {
                return new AllowedCharactersBitmap(new uint[ALLOWED_CHARS_BITMAP_LENGTH]);
            }

            private AllowedCharactersBitmap(uint[] allowedCharacters)
            {
                if (allowedCharacters == null)
                {
                    throw new ArgumentNullException(nameof(allowedCharacters));
                }
                _allowedCharacters = allowedCharacters;
            }

            // Marks a character as allowed (can be returned unencoded)
            public void AllowCharacter(char character)
            {
                int codePoint = character;
                int index = codePoint >> 5;
                int offset = codePoint & 0x1F;
                _allowedCharacters[index] |= 0x1U << offset;
            }

            // Marks a character as forbidden (must be returned encoded)
            public void ForbidCharacter(char character)
            {
                int codePoint = character;
                int index = codePoint >> 5;
                int offset = codePoint & 0x1F;
                _allowedCharacters[index] &= ~(0x1U << offset);
            }

            // Forbid codepoints which aren't mapped to characters or which are otherwise always disallowed
            // (includes categories Cc, Cs, Co, Cn, Zs [except U+0020 SPACE], Zl, Zp)
            public void ForbidUndefinedCharacters()
            {
                ReadOnlySpan<uint> definedCharactersBitmap = GetDefinedCharacterBitmap();
                Debug.Assert(definedCharactersBitmap.Length == _allowedCharacters.Length);
                for (int i = 0; i < _allowedCharacters.Length; i++)
                {
                    _allowedCharacters[i] &= definedCharactersBitmap[i];
                }
            }

            // This field is only used on big-endian architectures. We don't
            // bother computing it on little-endian architectures.
            private static readonly uint[] _definedCharacterBitmapBigEndian = (BitConverter.IsLittleEndian) ? null : CreateDefinedCharacterBitmapMachineEndian();

            private static uint[] CreateDefinedCharacterBitmapMachineEndian()
            {
                Debug.Assert(!BitConverter.IsLittleEndian);

                // We need to convert little-endian to machine-endian.

                ReadOnlySpan<byte> remainingBitmap = DefinedCharsBitmapSpan;
                uint[] bigEndianData = new uint[remainingBitmap.Length / sizeof(uint)];

                for (int i = 0; i < bigEndianData.Length; i++)
                {
                    bigEndianData[i] = BinaryPrimitives.ReadUInt32LittleEndian(remainingBitmap);
                    remainingBitmap = remainingBitmap.Slice(sizeof(uint));
                }

                return bigEndianData;
            }

            /// <summary>
            /// Returns a bitmap of all characters which are defined per the checked-in version
            /// of the Unicode specification.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ReadOnlySpan<uint> GetDefinedCharacterBitmap()
            {
                if (BitConverter.IsLittleEndian)
                {
                    // Underlying data is a series of 32-bit little-endian values and is guaranteed
                    // properly aligned by the compiler, so we know this is a valid cast byte -> uint.

                    return MemoryMarshal.Cast<byte, uint>(DefinedCharsBitmapSpan);
                }
                else
                {
                    // Static compiled data was little-endian; we had to create a big-endian
                    // representation at runtime.

                    return _definedCharacterBitmapBigEndian;
                }
            }

            private static ReadOnlySpan<byte> DefinedCharsBitmapSpan => new byte[0x2000]
        {
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, // U+0000..U+007F
            0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0080..U+00FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0100..U+017F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0180..U+01FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0200..U+027F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0280..U+02FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFC, // U+0300..U+037F
            0xF0, 0xD7, 0xFF, 0xFF, 0xFB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0380..U+03FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0400..U+047F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0480..U+04FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0x7F, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, // U+0500..U+057F
            0xFF, 0xE7, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0x87, 0x1F, 0x00, // U+0580..U+05FF
            0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0600..U+067F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0680..U+06FF
            0xFF, 0xBF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0700..U+077F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, // U+0780..U+07FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x4F, 0xFF, 0x07, 0x00, 0x00, // U+0800..U+087F
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xDF, 0x3F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0880..U+08FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+0900..U+097F
            0xEF, 0x9F, 0xF9, 0xFF, 0xFF, 0xFD, 0xC5, 0xF3, 0x9F, 0x79, 0x80, 0xB0, 0xCF, 0xFF, 0xFF, 0x7F, // U+0980..U+09FF
            0xEE, 0x87, 0xF9, 0xFF, 0xFF, 0xFD, 0x6D, 0xD3, 0x87, 0x39, 0x02, 0x5E, 0xC0, 0xFF, 0x7F, 0x00, // U+0A00..U+0A7F
            0xEE, 0xBF, 0xFB, 0xFF, 0xFF, 0xFD, 0xED, 0xF3, 0xBF, 0x3B, 0x01, 0x00, 0xCF, 0xFF, 0x03, 0xFE, // U+0A80..U+0AFF
            0xEE, 0x9F, 0xF9, 0xFF, 0xFF, 0xFD, 0xED, 0xF3, 0x9F, 0x39, 0xC0, 0xB0, 0xCF, 0xFF, 0xFF, 0x00, // U+0B00..U+0B7F
            0xEC, 0xC7, 0x3D, 0xD6, 0x18, 0xC7, 0xFF, 0xC3, 0xC7, 0x3D, 0x81, 0x00, 0xC0, 0xFF, 0xFF, 0x07, // U+0B80..U+0BFF
            0xFF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFD, 0xFF, 0xE3, 0xDF, 0x3D, 0x60, 0x07, 0xCF, 0xFF, 0x80, 0xFF, // U+0C00..U+0C7F
            0xFF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFD, 0xEF, 0xF3, 0xDF, 0x3D, 0x60, 0x40, 0xCF, 0xFF, 0x06, 0x00, // U+0C80..U+0CFF
            0xEF, 0xDF, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xDF, 0xFD, 0xF0, 0xFF, 0xCF, 0xFF, 0xFF, 0xFF, // U+0D00..U+0D7F
            0xEC, 0xFF, 0x7F, 0xFC, 0xFF, 0xFF, 0xFB, 0x2F, 0x7F, 0x84, 0x5F, 0xFF, 0xC0, 0xFF, 0x1C, 0x00, // U+0D80..U+0DFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x87, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0x00, 0x00, 0x00, // U+0E00..U+0E7F
            0xD6, 0xF7, 0xFF, 0xFF, 0xAF, 0xFF, 0xFF, 0x3F, 0x5F, 0x3F, 0xFF, 0xF3, 0x00, 0x00, 0x00, 0x00, // U+0E80..U+0EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0x1F, 0xFE, 0xFF, // U+0F00..U+0F7F
            0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0x07, 0x00, 0x00, 0x00, 0x00, // U+0F80..U+0FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1000..U+107F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1080..U+10FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1100..U+117F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1180..U+11FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3D, 0x7F, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, // U+1200..U+127F
            0xFF, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0x3D, 0x7F, 0x3D, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1280..U+12FF
            0xFF, 0xFF, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0xFF, 0xFF, 0x1F, // U+1300..U+137F
            0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x3F, // U+1380..U+13FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1400..U+147F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1480..U+14FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1500..U+157F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1580..U+15FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1600..U+167F
            0xFE, 0xFF, 0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, // U+1680..U+16FF
            0xFF, 0xDF, 0x1F, 0x00, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xDF, 0x0D, 0x00, // U+1700..U+177F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x03, 0xFF, 0x03, // U+1780..U+17FF
            0xFF, 0x7F, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, // U+1800..U+187F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, // U+1880..U+18FF
            0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0x0F, 0xFF, 0x0F, 0xF1, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x1F, 0x00, // U+1900..U+197F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xC7, 0xFF, 0xFF, 0xFF, 0xFF, // U+1980..U+19FF
            0xFF, 0xFF, 0xFF, 0xCF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x9F, // U+1A00..U+1A7F
            0xFF, 0x03, 0xFF, 0x03, 0xFF, 0x3F, 0xFF, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+1A80..U+1AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F, // U+1B00..U+1B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xF0, // U+1B80..U+1BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xF8, 0xFF, 0xE3, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1C00..U+1C7F
            0xFF, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE7, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, // U+1C80..U+1CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1D00..U+1D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFB, // U+1D80..U+1DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1E00..U+1E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+1E80..U+1EFF
            0xFF, 0xFF, 0x3F, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x3F, 0xFF, 0xAA, 0xFF, 0xFF, 0xFF, 0x3F, // U+1F00..U+1F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xCF, 0xEF, 0xFF, 0xFF, 0xDC, 0x7F, // U+1F80..U+1FFF
            0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0x7C, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xDF, 0xFF, 0xF3, 0xFF, // U+2000..U+207F
            0xFF, 0x7F, 0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x00, // U+2080..U+20FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2100..U+217F
            0xFF, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2180..U+21FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2200..U+227F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2280..U+22FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2300..U+237F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2380..U+23FF
            0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0x00, 0x00, 0xFF, 0x07, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, // U+2400..U+247F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2480..U+24FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2500..U+257F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2580..U+25FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2600..U+267F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2680..U+26FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2700..U+277F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2780..U+27FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2800..U+287F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2880..U+28FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2900..U+297F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2980..U+29FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2A00..U+2A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2A80..U+2AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xFF, // U+2B00..U+2B7F
            0xFF, 0xFF, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2B80..U+2BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, // U+2C00..U+2C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFE, // U+2C80..U+2CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x80, 0x01, 0x80, // U+2D00..U+2D7F
            0xFF, 0xFF, 0x7F, 0x00, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, // U+2D80..U+2DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+2E00..U+2E7F
            0xFF, 0xFF, 0xFF, 0xFB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, // U+2E80..U+2EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+2F00..U+2F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, 0x00, 0x00, 0xFF, 0x0F, // U+2F80..U+2FFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3000..U+307F
            0xFF, 0xFF, 0x7F, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3080..U+30FF
            0xE0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3100..U+317F
            0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xFF, // U+3180..U+31FF
            0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3200..U+327F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3280..U+32FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3300..U+337F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3380..U+33FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3400..U+347F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3480..U+34FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3500..U+357F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3580..U+35FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3600..U+367F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3680..U+36FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3700..U+377F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3780..U+37FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3800..U+387F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3880..U+38FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3900..U+397F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3980..U+39FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3A00..U+3A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3A80..U+3AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3B00..U+3B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3B80..U+3BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3C00..U+3C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3C80..U+3CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3D00..U+3D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3D80..U+3DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3E00..U+3E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3E80..U+3EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3F00..U+3F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+3F80..U+3FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4000..U+407F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4080..U+40FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4100..U+417F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4180..U+41FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4200..U+427F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4280..U+42FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4300..U+437F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4380..U+43FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4400..U+447F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4480..U+44FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4500..U+457F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4580..U+45FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4600..U+467F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4680..U+46FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4700..U+477F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4780..U+47FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4800..U+487F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4880..U+48FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4900..U+497F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4980..U+49FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4A00..U+4A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4A80..U+4AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4B00..U+4B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4B80..U+4BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4C00..U+4C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4C80..U+4CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4D00..U+4D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4D80..U+4DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4E00..U+4E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4E80..U+4EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4F00..U+4F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+4F80..U+4FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5000..U+507F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5080..U+50FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5100..U+517F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5180..U+51FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5200..U+527F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5280..U+52FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5300..U+537F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5380..U+53FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5400..U+547F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5480..U+54FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5500..U+557F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5580..U+55FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5600..U+567F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5680..U+56FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5700..U+577F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5780..U+57FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5800..U+587F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5880..U+58FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5900..U+597F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5980..U+59FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5A00..U+5A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5A80..U+5AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5B00..U+5B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5B80..U+5BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5C00..U+5C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5C80..U+5CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5D00..U+5D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5D80..U+5DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5E00..U+5E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5E80..U+5EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5F00..U+5F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+5F80..U+5FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6000..U+607F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6080..U+60FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6100..U+617F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6180..U+61FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6200..U+627F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6280..U+62FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6300..U+637F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6380..U+63FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6400..U+647F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6480..U+64FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6500..U+657F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6580..U+65FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6600..U+667F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6680..U+66FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6700..U+677F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6780..U+67FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6800..U+687F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6880..U+68FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6900..U+697F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6980..U+69FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6A00..U+6A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6A80..U+6AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6B00..U+6B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6B80..U+6BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6C00..U+6C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6C80..U+6CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6D00..U+6D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6D80..U+6DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6E00..U+6E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6E80..U+6EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6F00..U+6F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+6F80..U+6FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7000..U+707F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7080..U+70FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7100..U+717F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7180..U+71FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7200..U+727F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7280..U+72FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7300..U+737F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7380..U+73FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7400..U+747F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7480..U+74FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7500..U+757F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7580..U+75FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7600..U+767F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7680..U+76FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7700..U+777F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7780..U+77FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7800..U+787F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7880..U+78FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7900..U+797F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7980..U+79FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7A00..U+7A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7A80..U+7AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7B00..U+7B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7B80..U+7BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7C00..U+7C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7C80..U+7CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7D00..U+7D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7D80..U+7DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7E00..U+7E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7E80..U+7EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7F00..U+7F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+7F80..U+7FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8000..U+807F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8080..U+80FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8100..U+817F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8180..U+81FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8200..U+827F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8280..U+82FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8300..U+837F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8380..U+83FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8400..U+847F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8480..U+84FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8500..U+857F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8580..U+85FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8600..U+867F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8680..U+86FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8700..U+877F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8780..U+87FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8800..U+887F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8880..U+88FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8900..U+897F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8980..U+89FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8A00..U+8A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8A80..U+8AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8B00..U+8B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8B80..U+8BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8C00..U+8C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8C80..U+8CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8D00..U+8D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8D80..U+8DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8E00..U+8E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8E80..U+8EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8F00..U+8F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+8F80..U+8FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9000..U+907F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9080..U+90FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9100..U+917F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9180..U+91FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9200..U+927F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9280..U+92FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9300..U+937F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9380..U+93FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9400..U+947F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9480..U+94FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9500..U+957F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9580..U+95FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9600..U+967F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9680..U+96FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9700..U+977F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9780..U+97FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9800..U+987F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9880..U+98FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9900..U+997F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9980..U+99FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9A00..U+9A7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9A80..U+9AFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9B00..U+9B7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9B80..U+9BFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9C00..U+9C7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9C80..U+9CFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9D00..U+9D7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9D80..U+9DFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9E00..U+9E7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9E80..U+9EFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+9F00..U+9F7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, // U+9F80..U+9FFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A000..U+A07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A080..U+A0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A100..U+A17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A180..U+A1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A200..U+A27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A280..U+A2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A300..U+A37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A380..U+A3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A400..U+A47F
            0xFF, 0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A480..U+A4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A500..U+A57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A580..U+A5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A600..U+A67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, // U+A680..U+A6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+A700..U+A77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0xFF, // U+A780..U+A7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, // U+A800..U+A87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xC0, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, // U+A880..U+A8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x80, 0xFF, 0xFF, 0xFF, 0x1F, // U+A900..U+A97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xBF, 0xFF, 0xC3, 0xFF, 0xFF, 0xFF, 0x7F, // U+A980..U+A9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0xFF, 0x3F, 0xFF, 0xF3, 0xFF, 0xFF, 0xFF, 0xFF, // U+AA00..U+AA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x7F, 0x00, // U+AA80..U+AAFF
            0x7E, 0x7E, 0x7E, 0x00, 0x7F, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0xFF, // U+AB00..U+AB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0x03, // U+AB80..U+ABFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AC00..U+AC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AC80..U+ACFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AD00..U+AD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AD80..U+ADFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AE00..U+AE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AE80..U+AEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AF00..U+AF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+AF80..U+AFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B000..U+B07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B080..U+B0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B100..U+B17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B180..U+B1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B200..U+B27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B280..U+B2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B300..U+B37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B380..U+B3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B400..U+B47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B480..U+B4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B500..U+B57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B580..U+B5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B600..U+B67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B680..U+B6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B700..U+B77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B780..U+B7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B800..U+B87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B880..U+B8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B900..U+B97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+B980..U+B9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BA00..U+BA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BA80..U+BAFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BB00..U+BB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BB80..U+BBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BC00..U+BC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BC80..U+BCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BD00..U+BD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BD80..U+BDFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BE00..U+BE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BE80..U+BEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BF00..U+BF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+BF80..U+BFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C000..U+C07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C080..U+C0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C100..U+C17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C180..U+C1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C200..U+C27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C280..U+C2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C300..U+C37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C380..U+C3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C400..U+C47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C480..U+C4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C500..U+C57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C580..U+C5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C600..U+C67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C680..U+C6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C700..U+C77F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C780..U+C7FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C800..U+C87F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C880..U+C8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C900..U+C97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+C980..U+C9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CA00..U+CA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CA80..U+CAFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CB00..U+CB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CB80..U+CBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CC00..U+CC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CC80..U+CCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CD00..U+CD7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CD80..U+CDFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CE00..U+CE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CE80..U+CEFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CF00..U+CF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+CF80..U+CFFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D000..U+D07F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D080..U+D0FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D100..U+D17F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D180..U+D1FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D200..U+D27F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D280..U+D2FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D300..U+D37F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D380..U+D3FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D400..U+D47F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D480..U+D4FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D500..U+D57F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D580..U+D5FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D600..U+D67F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D680..U+D6FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+D700..U+D77F
            0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x00, 0xFF, 0xFF, 0x7F, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, // U+D780..U+D7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D800..U+D87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D880..U+D8FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D900..U+D97F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+D980..U+D9FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DA00..U+DA7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DA80..U+DAFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DB00..U+DB7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DB80..U+DBFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DC00..U+DC7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DC80..U+DCFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DD00..U+DD7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DD80..U+DDFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DE00..U+DE7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DE80..U+DEFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DF00..U+DF7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+DF80..U+DFFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E000..U+E07F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E080..U+E0FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E100..U+E17F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E180..U+E1FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E200..U+E27F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E280..U+E2FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E300..U+E37F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E380..U+E3FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E400..U+E47F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E480..U+E4FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E500..U+E57F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E580..U+E5FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E600..U+E67F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E680..U+E6FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E700..U+E77F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E780..U+E7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E800..U+E87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E880..U+E8FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E900..U+E97F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+E980..U+E9FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EA00..U+EA7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EA80..U+EAFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EB00..U+EB7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EB80..U+EBFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EC00..U+EC7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EC80..U+ECFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+ED00..U+ED7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+ED80..U+EDFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EE00..U+EE7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EE80..U+EEFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EF00..U+EF7F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+EF80..U+EFFF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F000..U+F07F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F080..U+F0FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F100..U+F17F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F180..U+F1FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F200..U+F27F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F280..U+F2FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F300..U+F37F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F380..U+F3FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F400..U+F47F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F480..U+F4FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F500..U+F57F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F580..U+F5FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F600..U+F67F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F680..U+F6FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F700..U+F77F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F780..U+F7FF
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F800..U+F87F
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // U+F880..U+F8FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+F900..U+F97F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+F980..U+F9FF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0xFF, 0xFF, // U+FA00..U+FA7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0x00, 0x00, 0x00, // U+FA80..U+FAFF
            0x7F, 0x00, 0xF8, 0xE0, 0xFF, 0xFF, 0x7F, 0x5F, 0xDB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FB00..U+FB7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FB80..U+FBFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FC00..U+FC7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FC80..U+FCFF
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FD00..U+FD7F
            0xFF, 0xFF, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x3F, // U+FD80..U+FDFF
            0xFF, 0xFF, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xF7, 0xFF, 0x7F, 0x0F, 0xDF, 0xFF, // U+FE00..U+FE7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F, // U+FE80..U+FEFF
            0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // U+FF00..U+FF7F
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0xFC, 0xFC, 0xFC, 0x1C, 0x7F, 0x7F, 0x00, 0x3E, // U+FF80..U+FFFF
        };

            // Marks all characters as forbidden (must be returned encoded)
            public void Clear()
            {
                Array.Clear(_allowedCharacters, 0, _allowedCharacters.Length);
            }

            // Creates a deep copy of this bitmap
            public AllowedCharactersBitmap Clone()
            {
                return new AllowedCharactersBitmap((uint[])_allowedCharacters.Clone());
            }

            // Determines whether the given character can be returned unencoded.
            public bool IsCharacterAllowed(char character)
            {
                int codePoint = character;
                int index = codePoint >> 5;
                int offset = codePoint & 0x1F;
                return ((_allowedCharacters[index] >> offset) & 0x1U) != 0;
            }

            // Determines whether the given character can be returned unencoded.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsUnicodeScalarAllowed(int unicodeScalar)
            {
                int index = unicodeScalar >> 5;
                int offset = unicodeScalar & 0x1F;
                return ((_allowedCharacters[index] >> offset) & 0x1U) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int FindFirstCharacterToEncode(char* text, int textLength)
            {
                for (int i = 0; i < textLength; i++)
                {
                    if (!IsCharacterAllowed(text[i]))
                    { return i; }
                }
                return -1;
            }

            private static readonly Vector128<short> s_mask_UInt16_0x00 = Vector128.Create((short)0x00); // Null
            private static readonly Vector128<short> s_mask_UInt16_0xFF = Vector128.Create((short)0xFF); // LATIN SMALL LETTER Y WITH DIAERESIS 'ÿ'

            private static readonly Vector128<short> s_mask_UInt16_0x20 = Vector128.Create((short)0x20); // Space ' '

            private static readonly Vector128<short> s_mask_UInt16_0x22 = Vector128.Create((short)0x22); // Quotation Mark '"'
            private static readonly Vector128<short> s_mask_UInt16_0x26 = Vector128.Create((short)0x26); // Ampersand '&'
            private static readonly Vector128<short> s_mask_UInt16_0x27 = Vector128.Create((short)0x27); // Apostrophe '''
            private static readonly Vector128<short> s_mask_UInt16_0x2B = Vector128.Create((short)0x2B); // Plus sign '+'
            private static readonly Vector128<short> s_mask_UInt16_0x3C = Vector128.Create((short)0x3C); // Less Than Sign '<'
            private static readonly Vector128<short> s_mask_UInt16_0x3E = Vector128.Create((short)0x3E); // Greater Than Sign '>'
            private static readonly Vector128<short> s_mask_UInt16_0x5C = Vector128.Create((short)0x5C); // Reverse Solidus '\'
            private static readonly Vector128<short> s_mask_UInt16_0x60 = Vector128.Create((short)0x60); // Grave Access '`'

            private static readonly Vector128<short> s_mask_UInt16_0x7E = Vector128.Create((short)0x7E); // Tilde '~'


            private static readonly Vector128<sbyte> s_mask_SByte_0x00 = Vector128.Create((sbyte)0x00); // Null

            private static readonly Vector128<sbyte> s_mask_SByte_0x20 = Vector128.Create((sbyte)0x20); // Space ' '

            private static readonly Vector128<sbyte> s_mask_SByte_0x22 = Vector128.Create((sbyte)0x22); // Quotation Mark '"'
            private static readonly Vector128<sbyte> s_mask_SByte_0x5C = Vector128.Create((sbyte)0x5C); // Reverse Solidus '\'

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector128<short> CreateEscapingMask(Vector128<short> sourceValue)
            {
                Debug.Assert(Sse2.IsSupported);

                Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x20); // Space ' ', anything in the control characters range

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x22)); // Quotation Mark '"'
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x5C)); // Reverse Solidus '\'

                return mask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector128<sbyte> CreateEscapingMask(Vector128<sbyte> sourceValue)
            {
                Debug.Assert(Sse2.IsSupported);

                Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x20); // Space ' ', anything in the control characters range

                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x22)); // Quotation Mark "
                mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x5C)); // Reverse Solidus \

                return mask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindFirstCharacterToEncode2(char* ptr, int textLength)
            {
                int idx = 0;

                if (Sse2.IsSupported)
                {
                    short* startingAddress = (short*)ptr;
                    while (textLength - 8 >= idx)
                    {
                        Debug.Assert(startingAddress >= ptr && startingAddress <= (ptr + textLength - 8));

                        // Load the next 8 characters.
                        Vector128<short> sourceValue = Sse2.LoadVector128(startingAddress);

                        Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x00); // Null, anything above short.MaxValue but less than or equal char.MaxValue
                        mask = Sse2.Or(mask, Sse2.CompareGreaterThan(sourceValue, s_mask_UInt16_0x7E)); // Tilde '~', anything above the ASCII range
                        int index = Sse2.MoveMask(mask.AsByte());
                        if (index != 0)
                        {
                            int processNextEight = idx + 8;
                            for (; idx < processNextEight; idx++)
                            {
                                Debug.Assert((ptr + idx) <= (ptr + textLength));
                                if (!IsCharacterAllowed(*(ptr + idx)))
                                {
                                    goto Return;
                                }
                            }
                        }
                        else
                        {
                            // Check if any of the 8 characters need to be escaped.
                            mask = CreateEscapingMask(sourceValue);

                            index = Sse2.MoveMask(mask.AsByte());
                            // If index == 0, that means none of the 8 characters needed to be escaped.
                            // TrailingZeroCount is relatively expensive, avoid it if possible.
                            if (index != 0)
                            {
                                // Found at least one character that needs to be escaped, figure out the index of
                                // the first one found that needed to be escaped within the 8 characters.
                                idx += BitOperations.TrailingZeroCount(index) >> 1;
                                goto Return;
                            }
                            idx += 8;
                            startingAddress += 8;
                        }
                    }

                    // Process the remaining characters.
                    Debug.Assert(textLength - idx < 8);
                }

                for (; idx < textLength; idx++)
                {
                    Debug.Assert((ptr + idx) <= (ptr + textLength));
                    if (!IsCharacterAllowed(*(ptr + idx)))
                    {
                        goto Return;
                    }
                }

                idx = -1; // All characters are allowed.

            Return:
                return idx;
            }

            /// <summary>
            /// Determines whether the given scalar value is in the supplementary plane and thus
            /// requires 2 characters to be represented in UTF-16 (as a surrogate pair).
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static bool IsSupplementaryCodePoint(int scalar)
            {
                return ((scalar & ~((int)char.MaxValue)) != 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool WillEncode(int unicodeScalar)
            {
                if (IsSupplementaryCodePoint(unicodeScalar))
                {
                    return true;
                }

                Debug.Assert(unicodeScalar >= char.MinValue && unicodeScalar <= char.MaxValue);

                return !IsUnicodeScalarAllowed(unicodeScalar);
            }

            // Fast cache for Ascii
            private readonly byte[][] _asciiEscape = new byte[0x80][];

            // Keep a reference to Array.Empty<byte> as this is used as a singleton for comparisons
            // and there is no guarantee that Array.Empty<byte>() will always be the same instance.
            private static readonly byte[] s_noEscape = Array.Empty<byte>();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte[] GetAsciiEncoding(byte value)
            {
                byte[] encoding = _asciiEscape[value];
                if (encoding == null)
                {
                    if (!WillEncode(value))
                    {
                        encoding = s_noEscape;
                        _asciiEscape[value] = encoding;
                    }
                }

                return encoding;
            }

            public int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
            {
                int originalUtf8TextLength = utf8Text.Length;

                // Loop through the input text, terminating when we see ill-formed UTF-8 or when we decode a scalar value
                // that must be encoded. If we see either of these things then we'll return its index in the original
                // input sequence. If we consume the entire text without seeing either of these, return -1 to indicate
                // that the text can be copied as-is without escaping.

                int i = 0;
                while (i < utf8Text.Length)
                {
                    byte value = utf8Text[i];
                    if (IsAsciiCodePoint(value))
                    {
                        if (!ReferenceEquals(GetAsciiEncoding(value), s_noEscape))
                        {
                            return originalUtf8TextLength - utf8Text.Length + i;
                        }

                        i++;
                    }
                    else
                    {
                        if (i > 0)
                        {
                            utf8Text = utf8Text.Slice(i);
                        }

                        if (DecodeScalarValueFromUtf8(utf8Text, out uint nextScalarValue, out int bytesConsumedThisIteration) != OperationStatus.Done
                          || WillEncode((int)nextScalarValue))
                        {
                            return originalUtf8TextLength - utf8Text.Length;
                        }

                        i = bytesConsumedThisIteration;
                    }
                }

                return -1; // no input data needs to be escaped
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int FindFirstCharacterToEncodeUtf82(ReadOnlySpan<byte> utf8Text)
            {
                fixed (byte* ptr = utf8Text)
                {
                    int idx = 0;

                    if (Sse2.IsSupported)
                    {
                        sbyte* startingAddress = (sbyte*)ptr;
                        while (utf8Text.Length - 16 >= idx)
                        {
                            Debug.Assert(startingAddress >= ptr && startingAddress <= (ptr + utf8Text.Length - 16));

                            // Load the next 16 bytes.
                            Vector128<sbyte> sourceValue = Sse2.LoadVector128(startingAddress);

                            // Null, anything above sbyte.MaxValue but less than or equal byte.MaxValue (i.e. anything above the ASCII range)
                            Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x00);
                            int index = Sse2.MoveMask(mask.AsByte());

                            if (index != 0)
                            {
                                // At least one of the following 16 bytes is non-ASCII.
                                int processNextSixteen = idx + 16;
                                Debug.Assert(processNextSixteen <= utf8Text.Length);
                                for (; idx < processNextSixteen; idx++)
                                {
                                    Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                                    OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                                    Debug.Assert(nextScalarValue <= int.MaxValue);
                                    if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                                    {
                                        goto Return;
                                    }

                                    Debug.Assert(opStatus == OperationStatus.Done);
                                    idx += utf8BytesConsumedForScalar;
                                }
                                startingAddress = (sbyte*)ptr + idx;
                            }
                            else
                            {
                                // Check if any of the 16 bytes need to be escaped.
                                mask = CreateEscapingMask(sourceValue);

                                index = Sse2.MoveMask(mask.AsByte());
                                // If index == 0, that means none of the 16 bytes needed to be escaped.
                                // TrailingZeroCount is relatively expensive, avoid it if possible.
                                if (index != 0)
                                {
                                    // Found at least one byte that needs to be escaped, figure out the index of
                                    // the first one found that needed to be escaped within the 16 bytes.
                                    idx += BitOperations.TrailingZeroCount(index | 0xFFFF0000);
                                    goto Return;
                                }
                                idx += 16;
                                startingAddress += 16;
                            }
                        }

                        // Process the remaining bytes.
                        Debug.Assert(utf8Text.Length - idx < 16);
                    }

                    for (; idx < utf8Text.Length;)
                    {
                        Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                        OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                        Debug.Assert(nextScalarValue <= int.MaxValue);
                        if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                        {
                            goto Return;
                        }

                        Debug.Assert(opStatus == OperationStatus.Done);
                        idx += utf8BytesConsumedForScalar;
                    }

                    idx = -1; // All bytes are allowed.

                Return:
                    return idx;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsAsciiCodePoint(uint value) => value <= 0x7Fu;

            /// <summary>
            /// Returns <see langword="true"/> iff <paramref name="value"/> is between
            /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound) => (value - lowerBound) <= (upperBound - lowerBound);

            /// <summary>
            /// A copy of the logic in Rune.DecodeFromUtf8.
            /// </summary>
            public static OperationStatus DecodeScalarValueFromUtf8(ReadOnlySpan<byte> source, out uint result, out int bytesConsumed)
            {
                const char ReplacementChar = '\uFFFD';

                // This method follows the Unicode Standard's recommendation for detecting
                // the maximal subpart of an ill-formed subsequence. See The Unicode Standard,
                // Ch. 3.9 for more details. In summary, when reporting an invalid subsequence,
                // it tries to consume as many code units as possible as long as those code
                // units constitute the beginning of a longer well-formed subsequence per Table 3-7.

                int index = 0;

                // Try reading input[0].

                if ((uint)index >= (uint)source.Length)
                {
                    goto NeedsMoreData;
                }

                uint tempValue = source[index];
                if (!IsAsciiCodePoint(tempValue))
                {
                    goto NotAscii;
                }

            Finish:

                bytesConsumed = index + 1;
                Debug.Assert(1 <= bytesConsumed && bytesConsumed <= 4); // Valid subsequences are always length [1..4]
                result = tempValue;
                return OperationStatus.Done;

            NotAscii:

                // Per Table 3-7, the beginning of a multibyte sequence must be a code unit in
                // the range [C2..F4]. If it's outside of that range, it's either a standalone
                // continuation byte, or it's an overlong two-byte sequence, or it's an out-of-range
                // four-byte sequence.

                if (!IsInRangeInclusive(tempValue, 0xC2, 0xF4))
                {
                    goto FirstByteInvalid;
                }

                tempValue = (tempValue - 0xC2) << 6;

                // Try reading input[1].

                index++;
                if ((uint)index >= (uint)source.Length)
                {
                    goto NeedsMoreData;
                }

                // Continuation bytes are of the form [10xxxxxx], which means that their two's
                // complement representation is in the range [-65..-128]. This allows us to
                // perform a single comparison to see if a byte is a continuation byte.

                int thisByteSignExtended = (sbyte)source[index];
                if (thisByteSignExtended >= -64)
                {
                    goto Invalid;
                }

                tempValue += (uint)thisByteSignExtended;
                tempValue += 0x80; // remove the continuation byte marker
                tempValue += (0xC2 - 0xC0) << 6; // remove the leading byte marker

                if (tempValue < 0x0800)
                {
                    Debug.Assert(IsInRangeInclusive(tempValue, 0x0080, 0x07FF));
                    goto Finish; // this is a valid 2-byte sequence
                }

                // This appears to be a 3- or 4-byte sequence. Since per Table 3-7 we now have
                // enough information (from just two code units) to detect overlong or surrogate
                // sequences, we need to perform these checks now.

                if (!IsInRangeInclusive(tempValue, ((0xE0 - 0xC0) << 6) + (0xA0 - 0x80), ((0xF4 - 0xC0) << 6) + (0x8F - 0x80)))
                {
                    // The first two bytes were not in the range [[E0 A0]..[F4 8F]].
                    // This is an overlong 3-byte sequence or an out-of-range 4-byte sequence.
                    goto Invalid;
                }

                if (IsInRangeInclusive(tempValue, ((0xED - 0xC0) << 6) + (0xA0 - 0x80), ((0xED - 0xC0) << 6) + (0xBF - 0x80)))
                {
                    // This is a UTF-16 surrogate code point, which is invalid in UTF-8.
                    goto Invalid;
                }

                if (IsInRangeInclusive(tempValue, ((0xF0 - 0xC0) << 6) + (0x80 - 0x80), ((0xF0 - 0xC0) << 6) + (0x8F - 0x80)))
                {
                    // This is an overlong 4-byte sequence.
                    goto Invalid;
                }

                // The first two bytes were just fine. We don't need to perform any other checks
                // on the remaining bytes other than to see that they're valid continuation bytes.

                // Try reading input[2].

                index++;
                if ((uint)index >= (uint)source.Length)
                {
                    goto NeedsMoreData;
                }

                thisByteSignExtended = (sbyte)source[index];
                if (thisByteSignExtended >= -64)
                {
                    goto Invalid; // this byte is not a UTF-8 continuation byte
                }

                tempValue <<= 6;
                tempValue += (uint)thisByteSignExtended;
                tempValue += 0x80; // remove the continuation byte marker
                tempValue -= (0xE0 - 0xC0) << 12; // remove the leading byte marker

                if (tempValue <= 0xFFFF)
                {
                    Debug.Assert(IsInRangeInclusive(tempValue, 0x0800, 0xFFFF));
                    goto Finish; // this is a valid 3-byte sequence
                }

                // Try reading input[3].

                index++;
                if ((uint)index >= (uint)source.Length)
                {
                    goto NeedsMoreData;
                }

                thisByteSignExtended = (sbyte)source[index];
                if (thisByteSignExtended >= -64)
                {
                    goto Invalid; // this byte is not a UTF-8 continuation byte
                }

                tempValue <<= 6;
                tempValue += (uint)thisByteSignExtended;
                tempValue += 0x80; // remove the continuation byte marker
                tempValue -= (0xF0 - 0xE0) << 18; // remove the leading byte marker

                goto Finish; // this is a valid 4-byte sequence

            FirstByteInvalid:

                index = 1; // Invalid subsequences are always at least length 1.

            Invalid:

                Debug.Assert(1 <= index && index <= 3); // Invalid subsequences are always length 1..3
                bytesConsumed = index;
                result = ReplacementChar;
                return OperationStatus.InvalidData;

            NeedsMoreData:

                Debug.Assert(0 <= index && index <= 3); // Incomplete subsequences are always length 0..3
                bytesConsumed = index;
                result = ReplacementChar;
                return OperationStatus.NeedMoreData;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool NeedsEscaping(char value) => AllowList[value] == 0;

            private static ReadOnlySpan<byte> AllowList => new byte[byte.MaxValue + 1]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0000..U+000F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0010..U+001F
                1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0020..U+002F   "
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0030..U+003F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0040..U+004F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, // U+0050..U+005F   \
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+0060..U+006F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, // U+0070..U+007F

                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0080..U+008F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // U+0090..U+009F
                0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00A0..U+00AF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00B0..U+00BF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00C0..U+00CF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00D0..U+00DF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00E0..U+00EF
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // U+00F0..U+00FF
            };
        }
    }


    public class Temp
    {

        private static readonly byte[] s_noEscape = Array.Empty<byte>();
        private readonly byte[][] _asciiEscape = new byte[0x80][];

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WillEncode(int unicodeScalar)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiCodePoint(uint value) => value <= 0x7Fu;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static OperationStatus DecodeScalarValueFromUtf8(ReadOnlySpan<byte> source, out uint result, out int bytesConsumed)
        {
            result = 'a';
            bytesConsumed = 1;
            return OperationStatus.Done;
        }

        public Temp(ReadOnlySpan<byte> utf8Text)
        {
            Test(utf8Text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] GetAsciiEncoding(byte value)
        {
            byte[] encoding = _asciiEscape[value];
            if (encoding == null)
            {
                if (!WillEncode(value))
                {
                    encoding = s_noEscape;
                    _asciiEscape[value] = encoding;
                }
            }

            return encoding;
        }

        public int Test(ReadOnlySpan<byte> utf8Text)
        {
            int originalUtf8TextLength = utf8Text.Length;

            // Loop through the input text, terminating when we see ill-formed UTF-8 or when we decode a scalar value
            // that must be encoded. If we see either of these things then we'll return its index in the original
            // input sequence. If we consume the entire text without seeing either of these, return -1 to indicate
            // that the text can be copied as-is without escaping.

            int i = 0;
            while (i < utf8Text.Length)
            {
                byte value = utf8Text[i];
                if (IsAsciiCodePoint(value))
                {
                    if (!ReferenceEquals(GetAsciiEncoding(value), s_noEscape))
                    {
                        return originalUtf8TextLength - utf8Text.Length + i;
                    }

                    i++;
                }
                else
                {
                    if (i > 0)
                    {
                        utf8Text = utf8Text.Slice(i);
                    }

                    if (DecodeScalarValueFromUtf8(utf8Text, out uint nextScalarValue, out int bytesConsumedThisIteration) != OperationStatus.Done
                      || WillEncode((int)nextScalarValue))
                    {
                        return originalUtf8TextLength - utf8Text.Length;
                    }

                    i = bytesConsumedThisIteration;
                }
            }

            return -1; // no input data needs to be escaped
        }
    }

    public unsafe class TestEscapingWriter_All
    {
        private string _source;
        private byte[] _sourceUtf8;
        private Utf8JsonWriter _writer;
        private Utf8JsonWriter _writerDefault;
        private ArrayBufferWriter<byte> _output;

        [Params(4)]
        public int DataLength { get; set; }

        [Params(-1)]
        public int NegativeIndex { get; set; }

        private JavaScriptEncoder _encoder;

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);
            var array = new char[DataLength];
            for (int i = 0; i < DataLength; i++)
            {
                array[i] = (char)random.Next(97, 123);
            }
            if (NegativeIndex != -1)
            {
                array[NegativeIndex] = '\\'; // '¢'
            }
            _source = new string(array);
            _sourceUtf8 = Encoding.UTF8.GetBytes(_source);

            _output = new ArrayBufferWriter<byte>();
            _writer = new Utf8JsonWriter(_output, new JsonWriterOptions { SkipValidation = true, Encoder = null });
            _writerDefault = new Utf8JsonWriter(_output, new JsonWriterOptions { SkipValidation = true, Encoder = JavaScriptEncoder.Default });
            _encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingUtf16()
        {
            _output.Clear();
            //for (int i = 0; i < 1_000; i++)
                _writer.WriteStringValue(_source);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingUtf8()
        {
            _output.Clear();
            //for (int i = 0; i < 1_000; i++)
                _writer.WriteStringValue(_sourceUtf8);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingUtf16_Default()
        {
            _output.Clear();
            //for (int i = 0; i < 1_000; i++)
            _writerDefault.WriteStringValue(_source);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void NeedsEscapingUtf8_Default()
        {
            _output.Clear();
            //for (int i = 0; i < 1_000; i++)
            _writerDefault.WriteStringValue(_sourceUtf8);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        public int NeedsEscapingNull()
        {
            return FindFirstCharacterToEncodeUtf8(_sourceUtf8);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingDefault()
        {
            return _encoder.FindFirstCharacterToEncodeUtf8(_sourceUtf8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
        {
            fixed (byte* ptr = utf8Text)
            {
                int idx = 0;

                if (Sse2.IsSupported)
                {
                    sbyte* startingAddress = (sbyte*)ptr;
                    while (utf8Text.Length - 16 >= idx)
                    {
                        Debug.Assert(startingAddress >= ptr && startingAddress <= (ptr + utf8Text.Length - 16));

                        // Load the next 16 bytes.
                        Vector128<sbyte> sourceValue = Sse2.LoadVector128(startingAddress);

                        // Null, anything above sbyte.MaxValue but less than or equal byte.MaxValue (i.e. anything above the ASCII range)
                        Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x00);
                        int index = Sse2.MoveMask(mask.AsByte());

                        if (index != 0)
                        {
                            // At least one of the following 16 bytes is non-ASCII.
                            int processNextSixteen = idx + 16;
                            Debug.Assert(processNextSixteen <= utf8Text.Length);
                            for (; idx < processNextSixteen; idx++)
                            {
                                Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                                OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                                Debug.Assert(nextScalarValue <= int.MaxValue);
                                if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                                {
                                    goto Return;
                                }

                                Debug.Assert(opStatus == OperationStatus.Done);
                                idx += utf8BytesConsumedForScalar;
                            }
                            startingAddress = (sbyte*)ptr + idx;
                        }
                        else
                        {
                            // Check if any of the 16 bytes need to be escaped.
                            mask = CreateEscapingMask(sourceValue);

                            index = Sse2.MoveMask(mask.AsByte());
                            // If index == 0, that means none of the 16 bytes needed to be escaped.
                            // TrailingZeroCount is relatively expensive, avoid it if possible.
                            if (index != 0)
                            {
                                // Found at least one byte that needs to be escaped, figure out the index of
                                // the first one found that needed to be escaped within the 16 bytes.
                                idx += BitOperations.TrailingZeroCount(index | 0xFFFF0000);
                                goto Return;
                            }
                            idx += 16;
                            startingAddress += 16;
                        }
                    }

                    // Process the remaining bytes.
                    Debug.Assert(utf8Text.Length - idx < 16);
                }

                for (; idx < utf8Text.Length;)
                {
                    Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                    OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                    Debug.Assert(nextScalarValue <= int.MaxValue);
                    if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                    {
                        goto Return;
                    }

                    Debug.Assert(opStatus == OperationStatus.Done);
                    idx += utf8BytesConsumedForScalar;
                }

                idx = -1; // All bytes are allowed.

            Return:
                return idx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnicodeScalarAllowed(int unicodeScalar)
        {
            int index = unicodeScalar >> 5;
            int offset = unicodeScalar & 0x1F;
            return ((index >> offset) & 0x1U) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> CreateEscapingMask(Vector128<short> sourceValue)
        {
            Debug.Assert(Sse2.IsSupported);

            Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x20); // Space ' ', anything in the control characters range

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x22)); // Quotation Mark '"'
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x5C)); // Reverse Solidus '\'

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<sbyte> CreateEscapingMask(Vector128<sbyte> sourceValue)
        {
            Debug.Assert(Sse2.IsSupported);

            Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x20); // Space ' ', anything in the control characters range

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x22)); // Quotation Mark "
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x5C)); // Reverse Solidus \

            return mask;
        }

        private static readonly Vector128<short> s_mask_UInt16_0x00 = Vector128.Create((short)0x00); // Null

        private static readonly Vector128<short> s_mask_UInt16_0x20 = Vector128.Create((short)0x20); // Space ' '

        private static readonly Vector128<short> s_mask_UInt16_0x22 = Vector128.Create((short)0x22); // Quotation Mark '"'
        private static readonly Vector128<short> s_mask_UInt16_0x5C = Vector128.Create((short)0x5C); // Reverse Solidus '\'

        private static readonly Vector128<short> s_mask_UInt16_0x7E = Vector128.Create((short)0x7E); // Tilde '~'

        private static readonly Vector128<sbyte> s_mask_SByte_0x00 = Vector128.Create((sbyte)0x00); // Null

        private static readonly Vector128<sbyte> s_mask_SByte_0x20 = Vector128.Create((sbyte)0x20); // Space ' '

        private static readonly Vector128<sbyte> s_mask_SByte_0x22 = Vector128.Create((sbyte)0x22); // Quotation Mark '"'
        private static readonly Vector128<sbyte> s_mask_SByte_0x5C = Vector128.Create((sbyte)0x5C); // Reverse Solidus '\'

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiCodePoint(uint value) => value <= 0x7Fu;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound) => (value - lowerBound) <= (upperBound - lowerBound);

        public static OperationStatus DecodeScalarValueFromUtf8(ReadOnlySpan<byte> source, out uint result, out int bytesConsumed)
        {
            const char ReplacementChar = '\uFFFD';

            // This method follows the Unicode Standard's recommendation for detecting
            // the maximal subpart of an ill-formed subsequence. See The Unicode Standard,
            // Ch. 3.9 for more details. In summary, when reporting an invalid subsequence,
            // it tries to consume as many code units as possible as long as those code
            // units constitute the beginning of a longer well-formed subsequence per Table 3-7.

            int index = 0;

            // Try reading input[0].

            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            uint tempValue = source[index];
            if (!IsAsciiCodePoint(tempValue))
            {
                goto NotAscii;
            }

        Finish:

            bytesConsumed = index + 1;
            Debug.Assert(1 <= bytesConsumed && bytesConsumed <= 4); // Valid subsequences are always length [1..4]
            result = tempValue;
            return OperationStatus.Done;

        NotAscii:

            // Per Table 3-7, the beginning of a multibyte sequence must be a code unit in
            // the range [C2..F4]. If it's outside of that range, it's either a standalone
            // continuation byte, or it's an overlong two-byte sequence, or it's an out-of-range
            // four-byte sequence.

            if (!IsInRangeInclusive(tempValue, 0xC2, 0xF4))
            {
                goto FirstByteInvalid;
            }

            tempValue = (tempValue - 0xC2) << 6;

            // Try reading input[1].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            // Continuation bytes are of the form [10xxxxxx], which means that their two's
            // complement representation is in the range [-65..-128]. This allows us to
            // perform a single comparison to see if a byte is a continuation byte.

            int thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid;
            }

            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue += (0xC2 - 0xC0) << 6; // remove the leading byte marker

            if (tempValue < 0x0800)
            {
                Debug.Assert(IsInRangeInclusive(tempValue, 0x0080, 0x07FF));
                goto Finish; // this is a valid 2-byte sequence
            }

            // This appears to be a 3- or 4-byte sequence. Since per Table 3-7 we now have
            // enough information (from just two code units) to detect overlong or surrogate
            // sequences, we need to perform these checks now.

            if (!IsInRangeInclusive(tempValue, ((0xE0 - 0xC0) << 6) + (0xA0 - 0x80), ((0xF4 - 0xC0) << 6) + (0x8F - 0x80)))
            {
                // The first two bytes were not in the range [[E0 A0]..[F4 8F]].
                // This is an overlong 3-byte sequence or an out-of-range 4-byte sequence.
                goto Invalid;
            }

            if (IsInRangeInclusive(tempValue, ((0xED - 0xC0) << 6) + (0xA0 - 0x80), ((0xED - 0xC0) << 6) + (0xBF - 0x80)))
            {
                // This is a UTF-16 surrogate code point, which is invalid in UTF-8.
                goto Invalid;
            }

            if (IsInRangeInclusive(tempValue, ((0xF0 - 0xC0) << 6) + (0x80 - 0x80), ((0xF0 - 0xC0) << 6) + (0x8F - 0x80)))
            {
                // This is an overlong 4-byte sequence.
                goto Invalid;
            }

            // The first two bytes were just fine. We don't need to perform any other checks
            // on the remaining bytes other than to see that they're valid continuation bytes.

            // Try reading input[2].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid; // this byte is not a UTF-8 continuation byte
            }

            tempValue <<= 6;
            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue -= (0xE0 - 0xC0) << 12; // remove the leading byte marker

            if (tempValue <= 0xFFFF)
            {
                Debug.Assert(IsInRangeInclusive(tempValue, 0x0800, 0xFFFF));
                goto Finish; // this is a valid 3-byte sequence
            }

            // Try reading input[3].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid; // this byte is not a UTF-8 continuation byte
            }

            tempValue <<= 6;
            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue -= (0xF0 - 0xE0) << 18; // remove the leading byte marker

            goto Finish; // this is a valid 4-byte sequence

        FirstByteInvalid:

            index = 1; // Invalid subsequences are always at least length 1.

        Invalid:

            Debug.Assert(1 <= index && index <= 3); // Invalid subsequences are always length 1..3
            bytesConsumed = index;
            result = ReplacementChar;
            return OperationStatus.InvalidData;

        NeedsMoreData:

            Debug.Assert(0 <= index && index <= 3); // Incomplete subsequences are always length 0..3
            bytesConsumed = index;
            result = ReplacementChar;
            return OperationStatus.NeedMoreData;
        }
    }

    internal sealed class MyCustomEncoder : JavaScriptEncoder
    {
        private readonly AllowedCharactersBitmap _allowedCharacters;

        public MyCustomEncoder(Test_EscapingUnsafe.TextEncoderSettings filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            _allowedCharacters = filter.GetAllowedCharacters();

            // Forbid codepoints which aren't mapped to characters or which are otherwise always disallowed
            // (includes categories Cc, Cs, Co, Cn, Zs [except U+0020 SPACE], Zl, Zp)
            _allowedCharacters.ForbidUndefinedCharacters();

            // Forbid characters that are special in HTML.
            // Even though this is a not HTML encoder,
            // it's unfortunately common for developers to
            // forget to HTML-encode a string once it has been JS-encoded,
            // so this offers extra protection.
            ForbidHtmlCharacters(_allowedCharacters);

            // '\' (U+005C REVERSE SOLIDUS) must always be escaped in Javascript / ECMAScript / JSON.
            // '/' (U+002F SOLIDUS) is not Javascript / ECMAScript / JSON-sensitive so doesn't need to be escaped.
            _allowedCharacters.ForbidCharacter('\\');

            // '`' (U+0060 GRAVE ACCENT) is ECMAScript-sensitive (see ECMA-262).
            _allowedCharacters.ForbidCharacter('`');
        }

        internal static void ForbidHtmlCharacters(AllowedCharactersBitmap allowedCharacters)
        {
            allowedCharacters.ForbidCharacter('<');
            allowedCharacters.ForbidCharacter('>');
            allowedCharacters.ForbidCharacter('&');
            allowedCharacters.ForbidCharacter('\''); // can be used to escape attributes
            allowedCharacters.ForbidCharacter('\"'); // can be used to escape attributes
            allowedCharacters.ForbidCharacter('+'); // technically not HTML-specific, but can be used to perform UTF7-based attacks
        }

        public MyCustomEncoder(params UnicodeRange[] allowedRanges) : this(new Test_EscapingUnsafe.TextEncoderSettings(allowedRanges))
        { }

        public override int MaxOutputCharactersPerInputCharacter => 12; // "\uFFFF\uFFFF" is the longest encoded form

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return _allowedCharacters.FindFirstCharacterToEncode(text, textLength);
        }

        public override bool WillEncode(int unicodeScalar)
        {
            if (IsSupplementaryCodePoint(unicodeScalar))
            {
                return true;
            }

            Debug.Assert(unicodeScalar >= char.MinValue && unicodeScalar <= char.MaxValue);

            return !_allowedCharacters.IsUnicodeScalarAllowed(unicodeScalar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsSupplementaryCodePoint(int scalar)
        {
            return ((scalar & ~((int)char.MaxValue)) != 0);
        }
    }

    //[DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public unsafe class NeedsEscapingTest
    {
        //[Params(E.Default, E.Relaxed, E.Create, E.Custom)]
        [Params(E.Default, E.Relaxed)]
        public E Encoder { get; set; }

        //[Params(1, 2, 4, 7, 8, 9, 15, 16, 17, 31, 32, 33, 100, 1000)]
        //[Params(1, 2, 4, 7, 8, 9, 15, 16, 17, 100, 1000)]
        [Params(160)]
        public int DataLength { get; set; }

        public enum E
        {
            Default,
            Relaxed,
            Create,
            Custom
        }

        private string _source;
        private byte[] _sourceUtf8;
        private JavaScriptEncoder _encoder;

        //[Params(true, false)]
        [Params(true)]
        public bool OnlyAscii { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);

            var array = new char[DataLength];
            for (int i = 0; i < DataLength; i++)
            {
                array[i] = (char)random.Next(97, 123);
                //array[i] = (char)random.Next(0xC0, 0xFF);
                if (!OnlyAscii && (i % 8 == 0))
                {
                    array[i] = (char)random.Next(0xC0, 0xFF);
                }
            }
            _source = new string(array);
            _sourceUtf8 = Encoding.UTF8.GetBytes(_source);

            _encoder = null;
            switch (Encoder)
            {
                case E.Default:
                    _encoder = JavaScriptEncoder.Default;
                    break;
                case E.Relaxed:
                    _encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    break;
                case E.Create:
                    _encoder = JavaScriptEncoder.Create(new Encodings.Web.TextEncoderSettings(UnicodeRanges.All));
                    break;
                case E.Custom:
                    _encoder = new MyCustomEncoder(new Test_EscapingUnsafe.TextEncoderSettings(UnicodeRanges.All));
                    break;
            }
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public int NeedsEscapingUtf16()
        {
            fixed (char* ptr = _source)
            {
                return _encoder.FindFirstCharacterToEncode(ptr, _source.Length);
            }
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int NeedsEscapingUtf8()
        {
            return _encoder.FindFirstCharacterToEncodeUtf8(_sourceUtf8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
        {
            fixed (byte* ptr = utf8Text)
            {
                int idx = 0;

                if (Sse2.IsSupported)
                {
                    sbyte* startingAddress = (sbyte*)ptr;
                    while (utf8Text.Length - 16 >= idx)
                    {
                        Debug.Assert(startingAddress >= ptr && startingAddress <= (ptr + utf8Text.Length - 16));

                        // Load the next 16 bytes.
                        Vector128<sbyte> sourceValue = Sse2.LoadVector128(startingAddress);

                        // Null, anything above sbyte.MaxValue but less than or equal byte.MaxValue (i.e. anything above the ASCII range)
                        Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x00);
                        int index = Sse2.MoveMask(mask.AsByte());

                        if (index != 0)
                        {
                            // At least one of the following 16 bytes is non-ASCII.
                            int processNextSixteen = idx + 16;
                            Debug.Assert(processNextSixteen <= utf8Text.Length);
                            for (; idx < processNextSixteen; idx++)
                            {
                                Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                                OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                                Debug.Assert(nextScalarValue <= int.MaxValue);
                                if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                                {
                                    goto Return;
                                }

                                Debug.Assert(opStatus == OperationStatus.Done);
                                idx += utf8BytesConsumedForScalar;
                            }
                            startingAddress = (sbyte*)ptr + idx;
                        }
                        else
                        {
                            // Check if any of the 16 bytes need to be escaped.
                            mask = CreateEscapingMask(sourceValue);

                            index = Sse2.MoveMask(mask.AsByte());
                            // If index == 0, that means none of the 16 bytes needed to be escaped.
                            // TrailingZeroCount is relatively expensive, avoid it if possible.
                            if (index != 0)
                            {
                                // Found at least one byte that needs to be escaped, figure out the index of
                                // the first one found that needed to be escaped within the 16 bytes.
                                idx += BitOperations.TrailingZeroCount(index | 0xFFFF0000);
                                goto Return;
                            }
                            idx += 16;
                            startingAddress += 16;
                        }
                    }

                    // Process the remaining bytes.
                    Debug.Assert(utf8Text.Length - idx < 16);
                }

                for (; idx < utf8Text.Length;)
                {
                    Debug.Assert((ptr + idx) <= (ptr + utf8Text.Length));

                    OperationStatus opStatus = DecodeScalarValueFromUtf8(utf8Text.Slice(idx), out uint nextScalarValue, out int utf8BytesConsumedForScalar);

                    Debug.Assert(nextScalarValue <= int.MaxValue);
                    if (!IsUnicodeScalarAllowed((int)nextScalarValue))
                    {
                        goto Return;
                    }

                    Debug.Assert(opStatus == OperationStatus.Done);
                    idx += utf8BytesConsumedForScalar;
                }

                idx = -1; // All bytes are allowed.

            Return:
                return idx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnicodeScalarAllowed(int unicodeScalar)
        {
            int index = unicodeScalar >> 5;
            int offset = unicodeScalar & 0x1F;
            return ((index >> offset) & 0x1U) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> CreateEscapingMask(Vector128<short> sourceValue)
        {
            Debug.Assert(Sse2.IsSupported);

            Vector128<short> mask = Sse2.CompareLessThan(sourceValue, s_mask_UInt16_0x20); // Space ' ', anything in the control characters range

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x22)); // Quotation Mark '"'
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_UInt16_0x5C)); // Reverse Solidus '\'

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<sbyte> CreateEscapingMask(Vector128<sbyte> sourceValue)
        {
            Debug.Assert(Sse2.IsSupported);

            Vector128<sbyte> mask = Sse2.CompareLessThan(sourceValue, s_mask_SByte_0x20); // Space ' ', anything in the control characters range

            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x22)); // Quotation Mark "
            mask = Sse2.Or(mask, Sse2.CompareEqual(sourceValue, s_mask_SByte_0x5C)); // Reverse Solidus \

            return mask;
        }

        private static readonly Vector128<short> s_mask_UInt16_0x00 = Vector128.Create((short)0x00); // Null

        private static readonly Vector128<short> s_mask_UInt16_0x20 = Vector128.Create((short)0x20); // Space ' '

        private static readonly Vector128<short> s_mask_UInt16_0x22 = Vector128.Create((short)0x22); // Quotation Mark '"'
        private static readonly Vector128<short> s_mask_UInt16_0x5C = Vector128.Create((short)0x5C); // Reverse Solidus '\'

        private static readonly Vector128<short> s_mask_UInt16_0x7E = Vector128.Create((short)0x7E); // Tilde '~'

        private static readonly Vector128<sbyte> s_mask_SByte_0x00 = Vector128.Create((sbyte)0x00); // Null

        private static readonly Vector128<sbyte> s_mask_SByte_0x20 = Vector128.Create((sbyte)0x20); // Space ' '

        private static readonly Vector128<sbyte> s_mask_SByte_0x22 = Vector128.Create((sbyte)0x22); // Quotation Mark '"'
        private static readonly Vector128<sbyte> s_mask_SByte_0x5C = Vector128.Create((sbyte)0x5C); // Reverse Solidus '\'

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiCodePoint(uint value) => value <= 0x7Fu;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound) => (value - lowerBound) <= (upperBound - lowerBound);

        public static OperationStatus DecodeScalarValueFromUtf8(ReadOnlySpan<byte> source, out uint result, out int bytesConsumed)
        {
            const char ReplacementChar = '\uFFFD';

            // This method follows the Unicode Standard's recommendation for detecting
            // the maximal subpart of an ill-formed subsequence. See The Unicode Standard,
            // Ch. 3.9 for more details. In summary, when reporting an invalid subsequence,
            // it tries to consume as many code units as possible as long as those code
            // units constitute the beginning of a longer well-formed subsequence per Table 3-7.

            int index = 0;

            // Try reading input[0].

            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            uint tempValue = source[index];
            if (!IsAsciiCodePoint(tempValue))
            {
                goto NotAscii;
            }

        Finish:

            bytesConsumed = index + 1;
            Debug.Assert(1 <= bytesConsumed && bytesConsumed <= 4); // Valid subsequences are always length [1..4]
            result = tempValue;
            return OperationStatus.Done;

        NotAscii:

            // Per Table 3-7, the beginning of a multibyte sequence must be a code unit in
            // the range [C2..F4]. If it's outside of that range, it's either a standalone
            // continuation byte, or it's an overlong two-byte sequence, or it's an out-of-range
            // four-byte sequence.

            if (!IsInRangeInclusive(tempValue, 0xC2, 0xF4))
            {
                goto FirstByteInvalid;
            }

            tempValue = (tempValue - 0xC2) << 6;

            // Try reading input[1].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            // Continuation bytes are of the form [10xxxxxx], which means that their two's
            // complement representation is in the range [-65..-128]. This allows us to
            // perform a single comparison to see if a byte is a continuation byte.

            int thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid;
            }

            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue += (0xC2 - 0xC0) << 6; // remove the leading byte marker

            if (tempValue < 0x0800)
            {
                Debug.Assert(IsInRangeInclusive(tempValue, 0x0080, 0x07FF));
                goto Finish; // this is a valid 2-byte sequence
            }

            // This appears to be a 3- or 4-byte sequence. Since per Table 3-7 we now have
            // enough information (from just two code units) to detect overlong or surrogate
            // sequences, we need to perform these checks now.

            if (!IsInRangeInclusive(tempValue, ((0xE0 - 0xC0) << 6) + (0xA0 - 0x80), ((0xF4 - 0xC0) << 6) + (0x8F - 0x80)))
            {
                // The first two bytes were not in the range [[E0 A0]..[F4 8F]].
                // This is an overlong 3-byte sequence or an out-of-range 4-byte sequence.
                goto Invalid;
            }

            if (IsInRangeInclusive(tempValue, ((0xED - 0xC0) << 6) + (0xA0 - 0x80), ((0xED - 0xC0) << 6) + (0xBF - 0x80)))
            {
                // This is a UTF-16 surrogate code point, which is invalid in UTF-8.
                goto Invalid;
            }

            if (IsInRangeInclusive(tempValue, ((0xF0 - 0xC0) << 6) + (0x80 - 0x80), ((0xF0 - 0xC0) << 6) + (0x8F - 0x80)))
            {
                // This is an overlong 4-byte sequence.
                goto Invalid;
            }

            // The first two bytes were just fine. We don't need to perform any other checks
            // on the remaining bytes other than to see that they're valid continuation bytes.

            // Try reading input[2].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid; // this byte is not a UTF-8 continuation byte
            }

            tempValue <<= 6;
            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue -= (0xE0 - 0xC0) << 12; // remove the leading byte marker

            if (tempValue <= 0xFFFF)
            {
                Debug.Assert(IsInRangeInclusive(tempValue, 0x0800, 0xFFFF));
                goto Finish; // this is a valid 3-byte sequence
            }

            // Try reading input[3].

            index++;
            if ((uint)index >= (uint)source.Length)
            {
                goto NeedsMoreData;
            }

            thisByteSignExtended = (sbyte)source[index];
            if (thisByteSignExtended >= -64)
            {
                goto Invalid; // this byte is not a UTF-8 continuation byte
            }

            tempValue <<= 6;
            tempValue += (uint)thisByteSignExtended;
            tempValue += 0x80; // remove the continuation byte marker
            tempValue -= (0xF0 - 0xE0) << 18; // remove the leading byte marker

            goto Finish; // this is a valid 4-byte sequence

        FirstByteInvalid:

            index = 1; // Invalid subsequences are always at least length 1.

        Invalid:

            Debug.Assert(1 <= index && index <= 3); // Invalid subsequences are always length 1..3
            bytesConsumed = index;
            result = ReplacementChar;
            return OperationStatus.InvalidData;

        NeedsMoreData:

            Debug.Assert(0 <= index && index <= 3); // Incomplete subsequences are always length 0..3
            bytesConsumed = index;
            result = ReplacementChar;
            return OperationStatus.NeedMoreData;
        }
    }

    public ref partial struct ValueStringBuilder
    {
        private char[] _arrayToReturnToPool;
        private Span<char> _chars;
        private int _pos;

        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _pos = 0;
        }

        public ValueStringBuilder(int initialCapacity)
        {
            _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
            _chars = _arrayToReturnToPool;
            _pos = 0;
        }

        public int Length
        {
            get => _pos;
            set
            {
                Debug.Assert(value >= 0);
                Debug.Assert(value <= _chars.Length);
                _pos = value;
            }
        }

        public int Capacity => _chars.Length;

        public void EnsureCapacity(int capacity)
        {
            if (capacity > _chars.Length)
                Grow(capacity - _pos);
        }

        /// <summary>
        /// Get a pinnable reference to the builder.
        /// Does not ensure there is a null char after <see cref="Length"/>
        /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
        /// the explicit method call, and write eg "fixed (char* c = builder)"
        /// </summary>
        public ref char GetPinnableReference()
        {
            return ref MemoryMarshal.GetReference(_chars);
        }

        /// <summary>
        /// Get a pinnable reference to the builder.
        /// </summary>
        /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
        public ref char GetPinnableReference(bool terminate)
        {
            if (terminate)
            {
                EnsureCapacity(Length + 1);
                _chars[Length] = '\0';
            }
            return ref MemoryMarshal.GetReference(_chars);
        }

        public ref char this[int index]
        {
            get
            {
                Debug.Assert(index < _pos);
                return ref _chars[index];
            }
        }

        public override string ToString()
        {
            string s = _chars.Slice(0, _pos).ToString();
            Dispose();
            return s;
        }

        /// <summary>Returns the underlying storage of the builder.</summary>
        public Span<char> RawChars => _chars;

        /// <summary>
        /// Returns a span around the contents of the builder.
        /// </summary>
        /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
        public ReadOnlySpan<char> AsSpan(bool terminate)
        {
            if (terminate)
            {
                EnsureCapacity(Length + 1);
                _chars[Length] = '\0';
            }
            return _chars.Slice(0, _pos);
        }

        public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _pos);
        public ReadOnlySpan<char> AsSpan(int start) => _chars.Slice(start, _pos - start);
        public ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

        public bool TryCopyTo(Span<char> destination, out int charsWritten)
        {
            if (_chars.Slice(0, _pos).TryCopyTo(destination))
            {
                charsWritten = _pos;
                Dispose();
                return true;
            }
            else
            {
                charsWritten = 0;
                Dispose();
                return false;
            }
        }

        public void Insert(int index, char value, int count)
        {
            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            int remaining = _pos - index;
            _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
            _chars.Slice(index, count).Fill(value);
            _pos += count;
        }

        public void Insert(int index, string s)
        {
            if (s == null)
            {
                return;
            }

            int count = s.Length;

            if (_pos > (_chars.Length - count))
            {
                Grow(count);
            }

            int remaining = _pos - index;
            _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
            s.AsSpan().CopyTo(_chars.Slice(index));
            _pos += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            int pos = _pos;
            if ((uint)pos < (uint)_chars.Length)
            {
                _chars[pos] = c;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string s)
        {
            if (s == null)
            {
                return;
            }

            int pos = _pos;
            if (s.Length == 1 && (uint)pos < (uint)_chars.Length) // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
            {
                _chars[pos] = s[0];
                _pos = pos + 1;
            }
            else
            {
                AppendSlow(s);
            }
        }

        private void AppendSlow(string s)
        {
            int pos = _pos;
            if (pos > _chars.Length - s.Length)
            {
                Grow(s.Length);
            }

            s.AsSpan().CopyTo(_chars.Slice(pos));
            _pos += s.Length;
        }

        public void Append(char c, int count)
        {
            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            Span<char> dst = _chars.Slice(_pos, count);
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = c;
            }
            _pos += count;
        }

        public unsafe void Append(char* value, int length)
        {
            int pos = _pos;
            if (pos > _chars.Length - length)
            {
                Grow(length);
            }

            Span<char> dst = _chars.Slice(_pos, length);
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = *value++;
            }
            _pos += length;
        }

        public void Append(ReadOnlySpan<char> value)
        {
            int pos = _pos;
            if (pos > _chars.Length - value.Length)
            {
                Grow(value.Length);
            }

            value.CopyTo(_chars.Slice(_pos));
            _pos += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<char> AppendSpan(int length)
        {
            int origPos = _pos;
            if (origPos > _chars.Length - length)
            {
                Grow(length);
            }

            _pos = origPos + length;
            return _chars.Slice(origPos, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char c)
        {
            Grow(1);
            Append(c);
        }

        /// <summary>
        /// Resize the internal buffer either by doubling current buffer size or
        /// by adding <paramref name="additionalCapacityBeyondPos"/> to
        /// <see cref="_pos"/> whichever is greater.
        /// </summary>
        /// <param name="additionalCapacityBeyondPos">
        /// Number of chars requested beyond current position.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            Debug.Assert(additionalCapacityBeyondPos > 0);
            Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

            char[] poolArray = ArrayPool<char>.Shared.Rent(Math.Max(_pos + additionalCapacityBeyondPos, _chars.Length * 2));

            _chars.Slice(0, _pos).CopyTo(poolArray);

            char[] toReturn = _arrayToReturnToPool;
            _chars = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            char[] toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public unsafe class ScartchPerf
    {
        JsonSnakeCaseNamingPolicy _policy;
        JsonSnakeCaseNamingPolicyNew _newPolicy;

        //[Params("ID", "url", "URL", "THIS IS SPARTA", "IsCIA", "iPhone", "IPhone", "xml2json", "already_snake_case", "Hi!! This is text. Time to test.", "IsJSONProperty", "ABCDEFGHIJKLMNOP")]
        [Params("Url", "already_snake_case", "ABCDEFGHIJKLMNOP")]
        public string InputString { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _policy = new JsonSnakeCaseNamingPolicy();
            _newPolicy = new JsonSnakeCaseNamingPolicyNew();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark (Baseline = true)]
        public string Current()
        {
            return _policy.ConvertName(InputString);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string New()
        {
            return _newPolicy.ConvertName(InputString);
        }

        internal sealed class JsonSnakeCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                StringBuilder builder = new StringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory? previousCategory = null;

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = null;
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1]))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLower(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if (previousCategory != null)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }

        internal sealed class JsonSnakeCaseNamingPolicyNew : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                var builder = new ValueStringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory previousCategory = (UnicodeCategory)(-1);

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = (UnicodeCategory)(-1);
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                (previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1])))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLowerInvariant(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if ((int)previousCategory != -1)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }

        internal sealed class JsonSnakeCaseNamingPolicyNew_VSB : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                var builder = new ValueStringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory? previousCategory = null;

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = null;
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1]))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLower(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if (previousCategory != null)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public class ConvertNamePerfComparison
    {
        JsonNamingPolicy _camelPolicy;
        JsonSnakeCaseNamingPolicy _snakePolicy;

        [Params("ID", "url", "URL", "THIS IS SPARTA", "IsCIA", "iPhone", "IPhone", "xml2json", "already_snake_case", "Hi!! This is text. Time to test.", "IsJSONProperty", "ABCDEFGHIJKLMNOP")]
        public string InputString { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _camelPolicy = JsonNamingPolicy.CamelCase;
            _snakePolicy = new JsonSnakeCaseNamingPolicy();
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark (Baseline =true)]
        public string Camel()
        {
            return _camelPolicy.ConvertName(InputString);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Snake()
        {
            return _snakePolicy.ConvertName(InputString);
        }

        internal sealed class JsonSnakeCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                StringBuilder builder = new StringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory? previousCategory = null;

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = null;
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1]))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLower(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if (previousCategory != null)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public unsafe class ScratchPerfSerialize
    {
        JsonSerializerOptions _camelOptions;
        JsonSerializerOptions _snakeOptions;

        TestClass _foo;

        [GlobalSetup]
        public void Setup()
        {
            _camelOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };
            _snakeOptions = new JsonSerializerOptions { PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy(), DictionaryKeyPolicy = new JsonSnakeCaseNamingPolicy() };

            _foo = new TestClass
            {
                ID = "A",
                url = "A",
                THIS_IS_SPARTA = "A",
                IsCIA = "A",
                iFhone = "A",
                IPhone = "A",
                xml2json = "A",
                already_snake_case = "A",
                IsJSONProperty = "A",
                ABCDEFGHIJKLMNOP = "A",
                Hi_This_is_text_Time_to_test = "A",
            };
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public string Default()
        {
            return JsonSerializer.Serialize(_foo);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Camel()
        {
            return JsonSerializer.Serialize(_foo, _camelOptions);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Snake()
        {
            return JsonSerializer.Serialize(_foo, _snakeOptions);
        }

        public class TestClass
        {
            public string ID { get; set; }
            public string url { get; set; }
            public string THIS_IS_SPARTA { get; set; }
            public string IsCIA { get; set; }
            public string iFhone { get; set; }
            public string IPhone { get; set; }
            public string xml2json { get; set; }
            public string already_snake_case { get; set; }
            public string IsJSONProperty { get; set; }
            public string ABCDEFGHIJKLMNOP { get; set; }
            public string Hi_This_is_text_Time_to_test { get; set; }
        }

        internal sealed class JsonSnakeCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                StringBuilder builder = new StringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory? previousCategory = null;

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = null;
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1]))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLower(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if (previousCategory != null)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public class SerializeDictionaryWithNamingPolicy
    {
        JsonSerializerOptions _camelOptions;
        JsonSerializerOptions _snakeOptions;

        Dictionary<string, string> _foo;

        [GlobalSetup]
        public void Setup()
        {
            _camelOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };
            _snakeOptions = new JsonSerializerOptions { PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy(), DictionaryKeyPolicy = new JsonSnakeCaseNamingPolicy() };

            _foo = new Dictionary<string, string>()
            {
                ["ID"] = "A",
                ["url"] = "A",
                ["URL"] = "A",
                ["THIS IS SPARTA"] = "A",
                ["IsCIA"] = "A",
                ["iPhone"] = "A",
                ["IPhone"] = "A",
                ["xml2json"] = "A",
                ["already_snake_case"] = "A",
                ["IsJSONProperty"] = "A",
                ["ABCDEFGHIJKLMNOP"] = "A",
                ["Hi!! This is text.Time to test."] = "A",
            };
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public string Default()
        {
            return JsonSerializer.Serialize(_foo);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Camel()
        {
            return JsonSerializer.Serialize(_foo, _camelOptions);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Snake()
        {
            return JsonSerializer.Serialize(_foo, _snakeOptions);
        }

        internal sealed class JsonSnakeCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Allocates a string builder with the guessed result length,
                // where 5 is the average word length in English, and
                // max(2, length / 5) is the number of underscores.
                StringBuilder builder = new StringBuilder(name.Length + Math.Max(2, name.Length / 5));
                UnicodeCategory? previousCategory = null;

                for (int currentIndex = 0; currentIndex < name.Length; currentIndex++)
                {
                    char currentChar = name[currentIndex];
                    if (currentChar == '_')
                    {
                        builder.Append('_');
                        previousCategory = null;
                        continue;
                    }

                    UnicodeCategory currentCategory = char.GetUnicodeCategory(currentChar);

                    switch (currentCategory)
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                            if (previousCategory == UnicodeCategory.SpaceSeparator ||
                                previousCategory == UnicodeCategory.LowercaseLetter ||
                                previousCategory != UnicodeCategory.DecimalDigitNumber &&
                                currentIndex > 0 &&
                                currentIndex + 1 < name.Length &&
                                char.IsLower(name[currentIndex + 1]))
                            {
                                builder.Append('_');
                            }

                            currentChar = char.ToLower(currentChar);
                            break;

                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                            if (previousCategory == UnicodeCategory.SpaceSeparator)
                                builder.Append('_');
                            break;

                        default:
                            if (previousCategory != null)
                                previousCategory = UnicodeCategory.SpaceSeparator;
                            continue;
                    }

                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                }

                return builder.ToString();
            }
        }
    }

    [DisassemblyDiagnoser(printPrologAndEpilog: true, recursiveDepth: 5)]
    public class SerializeDictionaryWithNamingPolicy_Newtonsoft
    {
        DefaultContractResolver _camelContractResolver;
        DefaultContractResolver _snakeContractResolver;

        Dictionary<string, string> _foo;

        [GlobalSetup]
        public void Setup()
        {
            _foo = new Dictionary<string, string>()
            {
                ["ID"] = "A",
                ["url"] = "A",
                ["URL"] = "A",
                ["THIS IS SPARTA"] = "A",
                ["IsCIA"] = "A",
                ["iPhone"] = "A",
                ["IPhone"] = "A",
                ["xml2json"] = "A",
                ["already_snake_case"] = "A",
                ["IsJSONProperty"] = "A",
                ["ABCDEFGHIJKLMNOP"] = "A",
                ["Hi!! This is text.Time to test."] = "A",
            };

            _snakeContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            };

            _camelContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public string Default()
        {
            return JsonConvert.SerializeObject(_foo);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Camel()
        {
            return JsonConvert.SerializeObject(_foo, new JsonSerializerSettings
            {
                ContractResolver = _camelContractResolver
            });
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string Snake()
        {
            return JsonConvert.SerializeObject(_foo, new JsonSerializerSettings
            {
                ContractResolver = _snakeContractResolver
            });
        }
    }

    public unsafe class EncodeStringsPerf
    {
        [Params(/*E.Default, E.Relaxed, E.Create,*/ E.Null)]
        public E Encoder { get; set; }

        //[Params(1, 2, 4, 7, 8, 9, 15, 16, 17, 31, 32, 33, 100, 1000)]
        //[Params(1, 2, 4, 7, 8, 9, 15, 16, 17, 100, 1000)]
        [Params(15)]
        public int DataLength { get; set; }

        public enum E
        {
            Default,
            Relaxed,
            Create,
            Null
        }

        private string _source;
        private byte[] _sourceUtf8;
        private byte[] _destination;
        private JavaScriptEncoder _encoder;
        private JavaScriptEncoder _encoderNew;
        private int _indexOfFirstByteToEscape;

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
            _source = "hello my name is \"Ahson\"";
            _sourceUtf8 = Encoding.UTF8.GetBytes(_source);

            _encoder = null;
            switch (Encoder)
            {
                case E.Default:
                    _encoder = JavaScriptEncoder.Default;
                    _encoderNew = JavaScriptEncoder.Default;
                    break;
                case E.Relaxed:
                    _encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    _encoderNew = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    break;
                case E.Create:
                    _encoder = JavaScriptEncoder.Create(new Encodings.Web.TextEncoderSettings(UnicodeRanges.All));
                    _encoderNew = JavaScriptEncoder.Create(new Encodings.Web.TextEncoderSettings(UnicodeRanges.All));
                    break;
                case E.Null:
                    _encoder = null;
                    _encoderNew = JavaScriptEncoder.Default;
                    break;
            }
            _destination = new byte[DataLength * 6];
            _indexOfFirstByteToEscape = 0;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark (Baseline = true)]
        public int Before()
        {
            EscapeString(_sourceUtf8, _destination, _indexOfFirstByteToEscape, _encoder, out int written);
            return written;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public int After()
        {
            EscapeStringAfter(_sourceUtf8, _destination, _indexOfFirstByteToEscape, _encoderNew, out int written);
            return written;
        }

        public static void EscapeStringAfter(ReadOnlySpan<byte> value, Span<byte> destination, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
        {
            Debug.Assert(indexOfFirstByteToEscape >= 0 && indexOfFirstByteToEscape < value.Length);

            value.Slice(0, indexOfFirstByteToEscape).CopyTo(destination);
            written = indexOfFirstByteToEscape;

            destination = destination.Slice(indexOfFirstByteToEscape);
            value = value.Slice(indexOfFirstByteToEscape);
            EscapeString(value, destination, encoder, ref written);
        }

        private static void EscapeString(ReadOnlySpan<byte> value, Span<byte> destination, JavaScriptEncoder encoder, ref int written)
        {
            Debug.Assert(encoder != null);

            OperationStatus result = encoder.EncodeUtf8(value, destination, out int encoderBytesConsumed, out int encoderBytesWritten);

            Debug.Assert(result != OperationStatus.DestinationTooSmall);
            Debug.Assert(result != OperationStatus.NeedMoreData);

            if (result != OperationStatus.Done)
            {
                throw new ArgumentException();
            }

            Debug.Assert(encoderBytesConsumed == value.Length);

            written += encoderBytesWritten;
        }

        public static void EscapeString(ReadOnlySpan<byte> value, Span<byte> destination, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
        {
            Debug.Assert(indexOfFirstByteToEscape >= 0 && indexOfFirstByteToEscape < value.Length);

            value.Slice(0, indexOfFirstByteToEscape).CopyTo(destination);
            written = indexOfFirstByteToEscape;

            if (encoder != null)
            {
                destination = destination.Slice(indexOfFirstByteToEscape);
                value = value.Slice(indexOfFirstByteToEscape);
                EscapeString(value, destination, encoder, ref written);
            }
            else
            {
                // For performance when no encoder is specified, perform escaping here for Ascii and on the
                // first occurrence of a non-Ascii character, then call into the default encoder.
                while (indexOfFirstByteToEscape < value.Length)
                {
                    byte val = value[indexOfFirstByteToEscape];
                    if (IsAsciiValue(val))
                    {
                        if (NeedsEscaping(val))
                        {
                            EscapeNextBytes(val, destination, ref written);
                            indexOfFirstByteToEscape++;
                        }
                        else
                        {
                            destination[written] = val;
                            written++;
                            indexOfFirstByteToEscape++;
                        }
                    }
                    else
                    {
                        // Fall back to default encoder.
                        destination = destination.Slice(written);
                        value = value.Slice(indexOfFirstByteToEscape);
                        EscapeString(value, destination, JavaScriptEncoder.Default, ref written);
                        break;
                    }
                }
            }
        }

        private static bool NeedsEscaping(byte value) => AllowList[value] == 0;

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

        private static bool IsAsciiValue(byte value) => value <= 0x7F;

        private static bool IsAsciiValue(char value) => value <= 0x7F;

        private static void EscapeNextBytes(byte value, Span<byte> destination, ref int written)
        {
            destination[written++] = (byte)'\\';
            switch (value)
            {
                case JsonConstants.Quote:
                    // Optimize for the common quote case.
                    destination[written++] = (byte)'u';
                    destination[written++] = (byte)'0';
                    destination[written++] = (byte)'0';
                    destination[written++] = (byte)'2';
                    destination[written++] = (byte)'2';
                    break;
                case JsonConstants.LineFeed:
                    destination[written++] = (byte)'n';
                    break;
                case JsonConstants.CarriageReturn:
                    destination[written++] = (byte)'r';
                    break;
                case JsonConstants.Tab:
                    destination[written++] = (byte)'t';
                    break;
                case JsonConstants.BackSlash:
                    destination[written++] = (byte)'\\';
                    break;
                case JsonConstants.BackSpace:
                    destination[written++] = (byte)'b';
                    break;
                case JsonConstants.FormFeed:
                    destination[written++] = (byte)'f';
                    break;
                default:
                    destination[written++] = (byte)'u';

                    bool result = Utf8Formatter.TryFormat(value, destination.Slice(written), out int bytesWritten, format: s_hexStandardFormat);
                    Debug.Assert(result);
                    Debug.Assert(bytesWritten == 4);
                    written += bytesWritten;
                    break;
            }
        }

        private static readonly StandardFormat s_hexStandardFormat = new StandardFormat('X', 4);

        internal static class JsonConstants
        {
            public const byte OpenBrace = (byte)'{';
            public const byte CloseBrace = (byte)'}';
            public const byte OpenBracket = (byte)'[';
            public const byte CloseBracket = (byte)']';
            public const byte Space = (byte)' ';
            public const byte CarriageReturn = (byte)'\r';
            public const byte LineFeed = (byte)'\n';
            public const byte Tab = (byte)'\t';
            public const byte ListSeparator = (byte)',';
            public const byte KeyValueSeperator = (byte)':';
            public const byte Quote = (byte)'"';
            public const byte BackSlash = (byte)'\\';
            public const byte Slash = (byte)'/';
            public const byte BackSpace = (byte)'\b';
            public const byte FormFeed = (byte)'\f';
            public const byte Asterisk = (byte)'*';
            public const byte Colon = (byte)':';
            public const byte Period = (byte)'.';
            public const byte Plus = (byte)'+';
            public const byte Hyphen = (byte)'-';
            public const byte UtcOffsetToken = (byte)'Z';
            public const byte TimePrefix = (byte)'T';

            // \u2028 and \u2029 are considered respectively line and paragraph separators
            // UTF-8 representation for them is E2, 80, A8/A9
            public const byte StartingByteOfNonStandardSeparator = 0xE2;

            public static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };
            public static ReadOnlySpan<byte> TrueValue => new byte[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };
            public static ReadOnlySpan<byte> FalseValue => new byte[] { (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e' };
            public static ReadOnlySpan<byte> NullValue => new byte[] { (byte)'n', (byte)'u', (byte)'l', (byte)'l' };

            // Used to search for the end of a number
            public static ReadOnlySpan<byte> Delimiters => new byte[] { ListSeparator, CloseBrace, CloseBracket, Space, LineFeed, CarriageReturn, Tab, Slash };

            // Explicitly skipping ReverseSolidus since that is handled separately
            public static ReadOnlySpan<byte> EscapableChars => new byte[] { Quote, (byte)'n', (byte)'r', (byte)'t', Slash, (byte)'u', (byte)'b', (byte)'f' };

            public const int SpacesPerIndent = 2;
            public const int MaxWriterDepth = 1_000;
            public const int RemoveFlagsBitMask = 0x7FFFFFFF;

            public const int StackallocThreshold = 256;

            // In the worst case, an ASCII character represented as a single utf-8 byte could expand 6x when escaped.
            // For example: '+' becomes '\u0043'
            // Escaping surrogate pairs (represented by 3 or 4 utf-8 bytes) would expand to 12 bytes (which is still <= 6x).
            // The same factor applies to utf-16 characters.
            public const int MaxExpansionFactorWhileEscaping = 6;

            // In the worst case, a single UTF-16 character could be expanded to 3 UTF-8 bytes.
            // Only surrogate pairs expand to 4 UTF-8 bytes but that is a transformation of 2 UTF-16 characters goign to 4 UTF-8 bytes (factor of 2).
            // All other UTF-16 characters can be represented by either 1 or 2 UTF-8 bytes.
            public const int MaxExpansionFactorWhileTranscoding = 3;

            public const int MaxEscapedTokenSize = 1_000_000_000;   // Max size for already escaped value.
            public const int MaxUnescapedTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping;  // 166_666_666 bytes
            public const int MaxBase64ValueTokenSize = (MaxEscapedTokenSize >> 2) * 3 / MaxExpansionFactorWhileEscaping;  // 125_000_000 bytes
            public const int MaxCharacterTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping; // 166_666_666 characters

            public const int MaximumFormatInt64Length = 20;   // 19 + sign (i.e. -9223372036854775808)
            public const int MaximumFormatUInt64Length = 20;  // i.e. 18446744073709551615
            public const int MaximumFormatDoubleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
            public const int MaximumFormatSingleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
            public const int MaximumFormatDecimalLength = 31; // default (i.e. 'G')
            public const int MaximumFormatGuidLength = 36;    // default (i.e. 'D'), 8 + 4 + 4 + 4 + 12 + 4 for the hyphens (e.g. 094ffa0a-0442-494d-b452-04003fa755cc)
            public const int MaximumEscapedGuidLength = MaxExpansionFactorWhileEscaping * MaximumFormatGuidLength;
            public const int MaximumFormatDateTimeLength = 27;    // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000
            public const int MaximumFormatDateTimeOffsetLength = 33;  // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000-07:00
            public const int MaxDateTimeUtcOffsetHours = 14; // The UTC offset portion of a TimeSpan or DateTime can be no more than 14 hours and no less than -14 hours.
            public const int DateTimeNumFractionDigits = 7;  // TimeSpan and DateTime formats allow exactly up to many digits for specifying the fraction after the seconds.
            public const int MaxDateTimeFraction = 9_999_999;  // The largest fraction expressible by TimeSpan and DateTime formats
            public const int DateTimeParseNumFractionDigits = 16; // The maximum number of fraction digits the Json DateTime parser allows
            public const int MaximumDateTimeOffsetParseLength = (MaximumFormatDateTimeOffsetLength +
                (DateTimeParseNumFractionDigits - DateTimeNumFractionDigits)); // Like StandardFormat 'O' for DateTimeOffset, but allowing 9 additional (up to 16) fraction digits.
            public const int MinimumDateTimeParseLength = 10; // YYYY-MM-DD
            public const int MaximumEscapedDateTimeOffsetParseLength = MaxExpansionFactorWhileEscaping * MaximumDateTimeOffsetParseLength;

            internal const char ScientificNotationFormat = 'e';

            // Encoding Helpers
            public const char HighSurrogateStart = '\ud800';
            public const char HighSurrogateEnd = '\udbff';
            public const char LowSurrogateStart = '\udc00';
            public const char LowSurrogateEnd = '\udfff';

            public const int UnicodePlane01StartValue = 0x10000;
            public const int HighSurrogateStartValue = 0xD800;
            public const int HighSurrogateEndValue = 0xDBFF;
            public const int LowSurrogateStartValue = 0xDC00;
            public const int LowSurrogateEndValue = 0xDFFF;
            public const int BitShiftBy10 = 0x400;
        }
    }
}
