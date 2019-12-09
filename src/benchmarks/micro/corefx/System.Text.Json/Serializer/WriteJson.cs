// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using MicroBenchmarks.Serializers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization.Tests
{
    public class Models
    {
        private const int ITEMS = 1000;
        public class ThousandSmallClassList
        {
            public List<SmallClass> List { get; set; }
            public ThousandSmallClassList()
            {
                List = new List<SmallClass>(Enumerable.Range(0, ITEMS).Select(x => new SmallClass()));
            }
        }

        public class ThousandSmallClassArray
        {
            public SmallClass[] Arr { get; set; }
            public ThousandSmallClassArray()
            {
                Arr = new List<SmallClass>(Enumerable.Range(0, ITEMS).Select(x => new SmallClass())).ToArray();
            }
        }

        public class ThousandSmallClassDictionary
        {
            public Dictionary<string, SmallClass> Dict { get; set; }
            public ThousandSmallClassDictionary()
            {
                Dict = new Dictionary<string, SmallClass>(Enumerable.Range(0, ITEMS)
                    .Select(x => new { Num = x, Obj = new SmallClass() })
                    .Select(anon => new KeyValuePair<string, SmallClass>(anon.Obj.Name + anon.Num.ToString(), anon.Obj)));
            }
        }

        public class SmallClass
        {
            public string Name { get; set; }
            public int NumPurchases { get; set; }
            public bool IsVIP { get; set; }

            public SmallClass()
            {
                Name = "Bill Gates";
                NumPurchases = 1200;
                IsVIP = true;
            }
        }

        // BigClass
        public class BigClass
        {
            public BigClass()
            {
                Id = 1;
                ClubMembershipType = ClubMembershipTypes.Platinum;
                Gender = Gender.Female;
                FirstName = "Louise";
                MiddleInitial = "C";
                Surname = "Midgett";
                Address = new Address
                {
                    StreetAddress = "43 Compare Valley",
                    City = "Chambersburg",
                    State = "PA",
                    ZipCode = 17201,
                    Country = "US"
                };
                EmailAddress = "LouiseCMidgett@dodgit.com";
                TelephoneNumber = "717-261-7855";
                MothersMaiden = "Kelly";
                NextBirthday = Convert.ToDateTime("1-May-84");
                CCType = "Visa";
                CaseFileID = "192-80-6448";
                UPS = "1Z W64 924 70 0191 812 7";
                Occupation = "Traffic technician";
                Domain = "ScottsdaleSkincare.com";
                MembershipLevel = "A+";
                Kilograms = "64.2";
            }

            public string FullName { get; set; }

            public int Id { get; set; }
            public Gender Gender { get; set; }
            public string FirstName { get; set; }
            public string MiddleInitial { get; set; }
            public string Surname { get; set; }
            public string EmailAddress { get; set; }
            public string TelephoneNumber { get; set; }
            public string MothersMaiden { get; set; }

            public DateTime NextBirthday { get; set; }
            public string CCType { get; set; }
            public string CaseFileID { get; set; }
            public string UPS { get; set; }
            public string Occupation { get; set; }
            public string Domain { get; set; }
            public string MembershipLevel { get; set; }
            public string Kilograms { get; set; }

            private int _pendingInvoiceID;


            public int PendingInvoiceID
            {
                get { return _pendingInvoiceID; }
                set { _pendingInvoiceID = value; }
            }

            public Address Address { get; set; }
            public ClubMembershipTypes ClubMembershipType { get; set; }
            public int YearsAsCustomer { get { return 5; } }

            public double GetYearlyEarnings(int year)
            {
                return year * 2;
            }

            internal int GetTotalMoneySpentAtStoreInUSD()
            {
                return ((int)FirstName[0]) * 50;
            }
        }

        public enum Gender
        {
            Male, Female
        }

        public enum ClubMembershipTypes
        {
            Premium, Platinum
        }

        public class Address
        {
            public string StreetAddress { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public int ZipCode { get; set; }
            public string Country { get; set; }
        }
        // -- end of BigClass
    }


    [GenericTypeArguments(typeof(Models.SmallClass))]
    [GenericTypeArguments(typeof(Models.BigClass))]
    [GenericTypeArguments(typeof(Models.ThousandSmallClassList))]
    [GenericTypeArguments(typeof(Models.ThousandSmallClassArray))]
    [GenericTypeArguments(typeof(Models.ThousandSmallClassDictionary))]
    public class SerializeToStream<T> where T : new()
    {
        private Newtonsoft.Json.JsonSerializer _newtonSoftSerializer;

        private T _instance;
        private MemoryStream _memoryStream;
        private StreamWriter _streamWriter;
        private Utf8JsonWriter _utf8JsonWriter;

        [GlobalSetup]
        public void Setup()
        {
            _instance = new T();
            _newtonSoftSerializer = new Newtonsoft.Json.JsonSerializer();
            _memoryStream = new MemoryStream(capacity: short.MaxValue);
            _streamWriter = new StreamWriter(_memoryStream, Encoding.UTF8);
            _utf8JsonWriter = new Utf8JsonWriter(_memoryStream);
        }

        public MemoryStream GetMemoryStream()
        {
            return _memoryStream;
        }

        public StreamWriter GetStreamWriter()
        {
            return _streamWriter;
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public void RunSystemTextJson()
        {
            _utf8JsonWriter.Reset();
            JsonSerializer.Serialize(_utf8JsonWriter, _instance);
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark (Baseline = true)]
        public void RunNewtonsoft()
        {
            _memoryStream.Position = 0;
            _newtonSoftSerializer.Serialize(_streamWriter, _instance);
        }
    }

    [GenericTypeArguments(typeof(LoginViewModel))]
    [GenericTypeArguments(typeof(Location))]
    [GenericTypeArguments(typeof(IndexViewModel))]
    [GenericTypeArguments(typeof(MyEventsListerViewModel))]
    public class WriteJson<T>
    {
        private T _value;
        private JsonSerializerOptions _optionsDefault;
        private JsonSerializerOptions _optionsRelaxed;

        [GlobalSetup]
        public void Setup()
        {
            _value = DataGenerator.Generate<T>();
            _optionsDefault = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Default
            };
            _optionsRelaxed = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public string SerializeToString_NullEncoder() => JsonSerializer.Serialize(_value);

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public string SerializeNewtonsoft() => Newtonsoft.Json.JsonConvert.SerializeObject(_value);

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(_value);

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public byte[] SerializeToUtf8Bytes() => JsonSerializer.SerializeToUtf8Bytes(_value);

        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark]
        //public async Task SerializeToStream()
        //{
        //    _memoryStream.Position = 0;
        //    await JsonSerializer.SerializeAsync(_memoryStream, _value);
        //}
    }

    public class WriteJason_Perf
    {
        public struct AccessToken
        {
            public AccessToken(string accessToken, DateTimeOffset expiresOn)
            {
                Token = accessToken;
                ExpiresOn = expiresOn;
            }

            [JsonPropertyName("access_token")]
            public string Token { get; set; }

            [JsonPropertyName("expires_on")]
            public DateTimeOffset ExpiresOn { get; set; }
        }

        public class WrappedMemoryStream : Stream
        {
            MemoryStream wrapped;
            private bool _canWrite;
            private bool _canRead;
            private bool _canSeek;

            public WrappedMemoryStream(MemoryStream stream)
            {
                wrapped = stream;
                _canWrite = stream.CanWrite;
                _canRead = stream.CanRead;
                _canSeek = stream.CanSeek;
            }

            public WrappedMemoryStream(bool canRead, bool canWrite, bool canSeek) :
                this(canRead, canWrite, canSeek, null)
            {
            }

            public WrappedMemoryStream(bool canRead, bool canWrite, bool canSeek, byte[] data)
            {
                wrapped = data != null ? new MemoryStream(data) : new MemoryStream();
                _canWrite = canWrite;
                _canRead = canRead;
                _canSeek = canSeek;
            }

            public override bool CanRead
            {
                get
                {
                    return _canRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _canSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _canWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return wrapped.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return wrapped.Position;
                }

                set
                {
                    wrapped.Position = value;
                }
            }

            public override void Flush()
            {
                wrapped.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return wrapped.Read(buffer, offset, count);
            }

            //public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            //{
            //    int result = await wrapped.ReadAsync(buffer, offset, count, cancellationToken);
            //    await Task.Yield();
            //    return result;
            //}

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int result = await wrapped.ReadAsync(buffer, cancellationToken);
                //await Task.Yield();
                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return wrapped.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                wrapped.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                wrapped.Write(buffer, offset, count);
            }
        }


        private MemoryStream _memoryStream;
        private WrappedMemoryStream _wrappedStream;
        private string _json;

        [GlobalSetup]
        public async Task Setup()
        {
            _memoryStream = new MemoryStream(capacity: short.MaxValue);
            _wrappedStream = new WrappedMemoryStream(_memoryStream);
            await JsonSerializer.SerializeAsync(_memoryStream, new AccessToken { Token = "abcdefghijklmnop", ExpiresOn = DateTimeOffset.Now });
            _json = Encoding.UTF8.GetString(_memoryStream.ToArray());
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark]
        public async Task<AccessToken> DeserializeUsingJsonDocument()
        {
            _memoryStream.Seek(0, SeekOrigin.Begin);
            AccessToken token = await DeserializeAsync(_memoryStream, default);
            return token;

            //using (JsonDocument json = JsonDocument.Parse(_json))
            //{
            //    return Deserialize(json.RootElement);
            //}
        }

        [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        [Benchmark(Baseline = true)]
        public async Task<AccessToken> DeserializeUsingJsonSerializer()
        {
            _wrappedStream.Seek(0, SeekOrigin.Begin);
            AccessToken token = await JsonSerializer.DeserializeAsync<AccessToken>(_wrappedStream);
            if (token.Token == null)
            {
                throw new Exception();
            }
            if (token.ExpiresOn == default)
            {
                throw new Exception();
            }
            return token;
        }

        private static async Task<AccessToken> DeserializeAsync(Stream content, CancellationToken cancellationToken)
        {
            using (JsonDocument json = await JsonDocument.ParseAsync(content, default, cancellationToken).ConfigureAwait(false))
            {
                return Deserialize(json.RootElement);
            }
        }

        private static AccessToken Deserialize(JsonElement json)
        {
            //return default;

            if (!json.TryGetProperty("access_token", out JsonElement accessTokenProp))
            {
                throw new Exception();
            }

            string accessToken = accessTokenProp.GetString();
            if (!json.TryGetProperty("expires_on", out JsonElement expiresOnProp))
            {
                throw new Exception();
            }

            DateTimeOffset expiresOn;

            if (!expiresOnProp.TryGetDateTimeOffset(out expiresOn))
            {
                throw new Exception();
            }

            return new AccessToken(accessToken, expiresOn);
        }


        //[BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
        //[Benchmark(Baseline = true)]
        //public string SerializeToStringJsonNet() => Newtonsoft.Json.JsonConvert.SerializeObject(_valueObject);

        [GlobalCleanup]
        public void Cleanup() => _memoryStream.Dispose();
    }
}
