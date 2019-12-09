// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using Newtonsoft.Json;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization.Testing
{
    public class Test_SerializingNuget
    {
        private string _serialized;
        private SearchResults _original;
        private JsonSerializerOptions _options;
        private JsonSerializerOptions _optionsRelaxed;

        [GlobalSetup]
        public void Setup()
        {
            _serialized = File.ReadAllText(@"E:\GitHub\Fork\Benchmarks\TestData\JsonParsingBenchmark\dotnet-core.json");
            _original = JsonSerializer.Deserialize<SearchResults>(_serialized);
            _options = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Default
            };
            _optionsRelaxed = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark (Baseline = true)]
        public string SerializeToString_NullEncoder()
        {
            return JsonSerializer.Serialize<SearchResults>(_original);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string SerializeToString_Default()
        {
            return JsonSerializer.Serialize<SearchResults>(_original, _options);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public string SerializeToString_Relaxed()
        {
            return JsonSerializer.Serialize<SearchResults>(_original, _optionsRelaxed);
        }

        public class SearchResult
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("version")]
            public string Version { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("versions")]
            public List<SearchResultVersion> Versions { get; set; }

            [JsonPropertyName("authors")]
            public List<string> Authors { get; set; }

            [JsonPropertyName("iconUrl")]
            public string IconUrl { get; set; }

            [JsonPropertyName("licenseUrl")]
            public string LicenseUrl { get; set; }

            [JsonPropertyName("owners")]
            public List<string> Owners { get; set; }

            [JsonPropertyName("projectUrl")]
            public string ProjectUrl { get; set; }

            [JsonPropertyName("registration")]
            public string Registration { get; set; }

            [JsonPropertyName("summary")]
            public string Summary { get; set; }

            [JsonPropertyName("tags")]
            public List<string> Tags { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("totalDownloads")]
            public int TotalDownloads { get; set; }

            [JsonPropertyName("verified")]
            public bool Verified { get; set; }
        }

        public class SearchResults
        {
            [JsonPropertyName("totalHits")]
            public int TotalHits { get; set; }

            [JsonPropertyName("data")]
            public List<SearchResult> Data { get; set; }
        }

        public class SearchResultVersion
        {
            [JsonPropertyName("@id")]
            public string Id { get; set; }

            [JsonPropertyName("version")]
            public string Version { get; set; }

            [JsonPropertyName("downloads")]
            public int Downloads { get; set; }
        }
    }
}

namespace System.Text.Json.Serialization.Tests
{
    public class TestPerfJson
    {
        private string _serialized;
        private JsonSerializerOptions _options;
        //private JsonObject _jsonObj;
        private byte[] _random;
        private SearchResults_Original _original;
        private SearchResults_OriginalWithoutAttributes _originalNoAttributes;
        private Newtonsoft.Json.JsonSerializer _serializer;
        private StreamReader _streamReader;
        private MemoryStream _memoryStream;
        //private string _testStringEscaped;
        //private string _testStringUnEscaped;
        //private TestObject _testObjectEscaped;
        //private TestObject _testObjectUnEscaped;

        [GlobalSetup]
        public void Setup()
        {
            _serialized = File.ReadAllText(@"E:\GitHub\Fork\Benchmarks\TestData\JsonParsingBenchmark\dotnet-core.json");
            _options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            //_jsonObj = (JsonObject)JsonNode.Parse(_serialized);
            var random = new Random(42);
            _random = new byte[100];
            random.NextBytes(_random);

            _original = JsonSerializer.Deserialize<SearchResults_Original>(_serialized);
            _originalNoAttributes = JsonSerializer.Deserialize<SearchResults_OriginalWithoutAttributes>(_serialized, _options);

            byte[] data = Encoding.UTF8.GetBytes(File.ReadAllText(@"E:\GitHub\Fork\Benchmarks\TestData\JsonParsingBenchmark\dotnet-core.json"));
            _memoryStream = new MemoryStream(data);

            _serializer = new Newtonsoft.Json.JsonSerializer();
            _streamReader = new StreamReader(_memoryStream, leaveOpen: true);

            _options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _options = new JsonSerializerOptions
            {
                DefaultBufferSize = 268_435_456
            };

            _options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            //_testStringEscaped = "{\"authors\":[\"Marcel K\u00F6rtgen\"]}";
            //_testStringUnEscaped = "{\"authors\":[\"Marcel Körtgen\"]}";
            //_testObjectEscaped = new TestObject();
            //_testObjectEscaped.authors = new List<string>();
            //_testObjectEscaped.authors.Add("Marcel K\u00F6rtgen");

            //_testObjectUnEscaped = new TestObject();
            //_testObjectUnEscaped.authors = new List<string>();
            //_testObjectUnEscaped.authors.Add("Marcel Körtgen");
        }

        public class TestObject
        {
            public List<string> authors { get; set; }
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public TestObject DeserializeFromStringEscaped_STJson()
        //{
        //    return JsonSerializer.Deserialize<TestObject>(_testStringEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public TestObject DeserializeFromStringUnescaped_STJson()
        //{
        //    return JsonSerializer.Deserialize<TestObject>(_testStringUnEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeFromStringEscaped_STJson()
        //{
        //    return JsonSerializer.Serialize<TestObject>(_testObjectEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeFromStringUnescaped_STJson()
        //{
        //    return JsonSerializer.Serialize<TestObject>(_testObjectUnEscaped);
        //}



        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public TestObject DeserializeFromStringEscaped_Newtonsoft()
        //{
        //    return JsonConvert.DeserializeObject<TestObject>(_testStringEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public TestObject DeserializeFromStringUnescaped_Newtonsoft()
        //{
        //    return JsonConvert.DeserializeObject<TestObject>(_testStringUnEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeFromStringEscaped_Newtonsoft()
        //{
        //    return JsonConvert.SerializeObject(_testObjectEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeFromStringUnescaped_Newtonsoft()
        //{
        //    return JsonConvert.SerializeObject(_testObjectUnEscaped);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public JsonNode TestGetPropertyValue()
        //{
        //    JsonNode result = null;
        //    for (int i = 0; i < _random.Length; i++)
        //    {
        //        try
        //        {
        //            if (_random[i] < 128)
        //            {
        //                result = _jsonObj.GetPropertyValue("lastReopen");
        //            }
        //            else
        //            {
        //                result = _jsonObj.GetPropertyValue("lastReopen1234");
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }

        //    return result;
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public SearchResults DeserializeFromString_STJson()
        //{
        //    return JsonSerializer.Deserialize<SearchResults>(_serialized, _options);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public SearchResults DeserializeFromString_Newtonsoft()
        //{
        //    return Newtonsoft.Json.JsonConvert.DeserializeObject<SearchResults>(_serialized);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public SearchResults_Original DeserializeFromString_Original_STJson()
        //{
        //    return JsonSerializer.Deserialize<SearchResults_Original>(_serialized);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeToStringCamelCase_Original_STJson()
        //{
        //    return JsonSerializer.Serialize<SearchResults_Original>(_original, _options);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeToStringCamelCase_OriginalWithoutAttributes_STJson()
        //{
        //    var options = new JsonSerializerOptions
        //    {
        //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        //    };
        //    return JsonSerializer.Serialize<SearchResults_OriginalWithoutAttributes>(_originalNoAttributes, options);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public SearchResults_OriginalWithoutAttributes DeserializeFromString_OriginalWithoutAttributes_STJson()
        //{
        //    var options = new JsonSerializerOptions
        //    {
        //        PropertyNameCaseInsensitive = true,
        //    };
        //    return JsonSerializer.Deserialize<SearchResults_OriginalWithoutAttributes>(_serialized, options);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public SearchResults_Original DeserializeFromString_Original_Newtonsoft()
        //{
        //    return Newtonsoft.Json.JsonConvert.DeserializeObject<SearchResults_Original>(_serialized);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public async Task<SearchResults_Original> SystemTextJson()
        //{
        //    _memoryStream.Seek(0, SeekOrigin.Begin);
        //    var obj = await JsonSerializer.DeserializeAsync<SearchResults_Original>(_memoryStream);
        //    return obj;
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public SearchResults_Original NewtonsoftJson()
        //{
        //    _memoryStream.Seek(0, SeekOrigin.Begin);
        //    var obj = (SearchResults_Original)_serializer.Deserialize(_streamReader, typeof(SearchResults_Original));
        //    return obj;
        //}

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public string SerializeToString_Original_STJson()
        {
            return JsonSerializer.Serialize<SearchResults_Original>(_original);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        public string SerializeToString_Original_CustomOptions_STJson()
        {
            return JsonSerializer.Serialize<SearchResults_Original>(_original, _options);
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public ReadOnlySpan<byte> SerializeToUtf8Bytes_Original_OverrideBufferSize_STJson()
        //{
        //    var output = new ArrayBufferWriter<byte>();
        //    using var writer = new Utf8JsonWriter(output);
        //    JsonSerializer.Serialize<SearchResults_Original>(writer, _original);
        //    return output.WrittenSpan.ToArray();
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public byte[] SerializeToUtf8Bytes_Original_STJson()
        //{
        //    return JsonSerializer.SerializeToUtf8Bytes<SearchResults_Original>(_original);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public byte[] SerializeToUtf8Bytes_Original_OverrideBufferSize_STJson()
        //{
        //    return JsonSerializer.SerializeToUtf8Bytes<SearchResults_Original>(_original, _options);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public string SerializeToString_Original_Newtonsoft()
        //{
        //    return JsonConvert.SerializeObject(_original);
        //}

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public string SerializeToString_Original_Jil()
        //{
        //    return Jil.JSON.Serialize<SearchResults_Original>(_original);
        //}
    }

    public class SearchResult
    {
        ////[JsonPropertyName("id")]
        public string id { get; set; }

        //[JsonPropertyName("version")]
        public string version { get; set; }

        //[JsonPropertyName("description")]
        public string description { get; set; }

        //[JsonPropertyName("versions")]
        public List<SearchResultVersion> versions { get; set; }

        //[JsonPropertyName("authors")]
        public List<string> authors { get; set; }

        //[JsonPropertyName("iconUrl")]
        public string iconUrl { get; set; }

        //[JsonPropertyName("licenseUrl")]
        public string licenseUrl { get; set; }

        //[JsonPropertyName("owners")]
        public List<string> owners { get; set; }

        //[JsonPropertyName("projectUrl")]
        public string projectUrl { get; set; }

        //[JsonPropertyName("registration")]
        public string registration { get; set; }

        //[JsonPropertyName("summary")]
        public string summary { get; set; }

        //[JsonPropertyName("tags")]
        public List<string> tags { get; set; }

        //[JsonPropertyName("title")]
        public string title { get; set; }

        //[JsonPropertyName("totalDownloads")]
        public int totalDownloads { get; set; }

        //[JsonPropertyName("verified")]
        public bool verified { get; set; }
    }

    public class SearchResults
    {
        //[JsonPropertyName("totalHits")]
        public int totalHits { get; set; }

        //[JsonPropertyName("data")]
        public List<SearchResult> data { get; set; }
    }

    public class SearchResultVersion
    {
        //[JsonPropertyName("@id")]
        public string id { get; set; }

        //[JsonPropertyName("version")]
        public string version { get; set; }

        //[JsonPropertyName("downloads")]
        public int downloads { get; set; }
    }

    public class SearchResult_OriginalWithoutAttributes
    {
        //[JsonPropertyName("id")]
        public string Id { get; set; }

        //[JsonPropertyName("version")]
        public string Version { get; set; }

        //[JsonPropertyName("description")]
        public string Description { get; set; }

        //[JsonPropertyName("versions")]
        public List<SearchResultVersion_OriginalWithoutAttributes> Versions { get; set; }

        //[JsonPropertyName("authors")]
        public List<string> Authors { get; set; }

        //[JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        //[JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        //[JsonPropertyName("owners")]
        public List<string> Owners { get; set; }

        //[JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        //[JsonPropertyName("registration")]
        public string Registration { get; set; }

        //[JsonPropertyName("summary")]
        public string Summary { get; set; }

        //[JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        //[JsonPropertyName("title")]
        public string Title { get; set; }

        //[JsonPropertyName("totalDownloads")]
        public int TotalDownloads { get; set; }

        //[JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }

    public class SearchResults_OriginalWithoutAttributes
    {
        //[JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }

        //[JsonPropertyName("data")]
        public List<SearchResult_OriginalWithoutAttributes> Data { get; set; }
    }

    public class SearchResultVersion_OriginalWithoutAttributes
    {
        //[JsonPropertyName("@id")]
        public string Id { get; set; }

        //[JsonPropertyName("version")]
        public string Version { get; set; }

        //[JsonPropertyName("downloads")]
        public int Downloads { get; set; }
    }

    public class SearchResult_Original
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("versions")]
        public List<SearchResultVersion_Original> Versions { get; set; }

        [JsonPropertyName("authors")]
        public List<string> Authors { get; set; }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonPropertyName("owners")]
        public List<string> Owners { get; set; }

        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonPropertyName("registration")]
        public string Registration { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("totalDownloads")]
        public int TotalDownloads { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }

    public class SearchResults_Original
    {
        [JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }

        [JsonPropertyName("data")]
        public List<SearchResult_Original> Data { get; set; }
    }

    public class SearchResultVersion_Original
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }
    }
}
