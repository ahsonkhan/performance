// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using MicroBenchmarks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace System.Text.Json.Tests
{
    [BenchmarkCategory(Categories.CoreFX, Categories.JSON)]
    public class Perf_Serializer
    {
        private object _value;
        public Type _type;

        [Params(1, 2, 3, 4)]
        public int TypeNum;

        [GlobalSetup]
        public void Setup()
        {
            switch (TypeNum)
            {
                case 1:
                    _value = DataGenerator.Generate<LoginViewModel>();
                    _type = typeof(LoginViewModel);
                    break;
                case 2:
                    _value = DataGenerator.Generate<Location>();
                    _type = typeof(Location);
                    break;
                case 3:
                    _value = DataGenerator.Generate<IndexViewModel>();
                    _type = typeof(IndexViewModel);
                    break;
                case 4:
                    _value = DataGenerator.Generate<MyEventsListerViewModel>();
                    _type = typeof(MyEventsListerViewModel);
                    break;
            }
        }

        [Benchmark]
        public byte[] Serialize()
        {
            return Serialization.JsonSerializer.ToBytes(_value, _type);
        }

        //[Benchmark(Baseline = true)]
        public string SerializeNewtonsoft()
        {
            return JsonConvert.SerializeObject(_value);
        }
    }

    internal static class DataGenerator
    {
        internal static object Generate<T>()
        {
            if (typeof(T) == typeof(LoginViewModel))
                return (object)CreateLoginViewModel();
            if (typeof(T) == typeof(Location))
                return (object)CreateLocation();
            if (typeof(T) == typeof(IndexViewModel))
                return (object)CreateIndexViewModel();
            if (typeof(T) == typeof(MyEventsListerViewModel))
                return (object)CreateMyEventsListerViewModel();

            throw new NotImplementedException();
        }

        private static LoginViewModel CreateLoginViewModel()
            => new LoginViewModel
            {
                Email = "name.familyname@not.com",
                Password = "abcdefgh123456!@",
                RememberMe = true
            };

        private static Location CreateLocation()
            => new Location
            {
                Id = 1234,
                Address1 = "The Street Name",
                Address2 = "20/11",
                City = "The City",
                State = "The State",
                PostalCode = "abc-12",
                Name = "Nonexisting",
                PhoneNumber = "+0 11 222 333 44",
                Country = "The Greatest"
            };

        private static IndexViewModel CreateIndexViewModel()
        {
            var events = new List<ActiveOrUpcomingEvent>();
            for (int i = 0; i < 20; i++)
            {
                events.Add(new ActiveOrUpcomingEvent
                {
                    Id = 10,
                    CampaignManagedOrganizerName = "Name FamiltyName",
                    CampaignName = "The very new campaing",
                    Description = "The .NET Foundation works with Microsoft and the broader industry to increase the exposure of open source projects in the .NET community and the .NET Foundation. The .NET Foundation provides access to these resources to projects and looks to promote the activities of our communities.",
                    EndDate = DateTime.UtcNow.AddYears(1),
                    Name = "Just a name",
                    ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                    StartDate = DateTime.UtcNow
                });
            }

            return new IndexViewModel
            {
                IsNewAccount = false,
                FeaturedCampaign = new CampaignSummaryViewModel
                {
                    Description = "Very nice campaing",
                    Headline = "The Headline",
                    Id = 234235,
                    OrganizationName = "The Company XYZ",
                    ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                    Title = "Promoting Open Source"
                },
                ActiveOrUpcomingEvents = events
            };
        }

        private static MyEventsListerViewModel CreateMyEventsListerViewModel()
        {
            var current = new List<MyEventsListerItem>();

            for (int i = 0; i < 3; i++)
            {
                current.Add(CreateMyEventsListerItem());
            }

            var future = new List<MyEventsListerItem>();

            for (int i = 0; i < 9; i++)
            {
                future.Add(CreateMyEventsListerItem());
            }

            var past = new List<MyEventsListerItem>();

            // usually  there is a lot of historical data
            for (int i = 0; i < 60; i++)
            {
                past.Add(CreateMyEventsListerItem());
            }

            return new MyEventsListerViewModel
            {
                CurrentEvents = current,
                FutureEvents = future,
                PastEvents = past
            };
        }

        private static MyEventsListerItem CreateMyEventsListerItem()
        {
            var tasks = new List<MyEventsListerItemTask>();
            for (int i = 0; i < 4; i++)
            {
                tasks.Add(new MyEventsListerItemTask
                {
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    Name = "A very nice task to have"
                });
            }

            return new MyEventsListerItem
            {
                Campaign = "A very nice campaing",
                EndDate = DateTime.UtcNow.AddDays(7),
                EventId = 321,
                EventName = "wonderful name",
                Organization = "Local Animal Shelter",
                StartDate = DateTime.UtcNow.AddDays(-7),
                TimeZone = TimeZoneInfo.Utc.DisplayName,
                VolunteerCount = 15,
                Tasks = tasks
            };
        }
    }

    // the view models come from a real world app called "AllReady"
    [Serializable]
    public class LoginViewModel
    {
        public virtual string Email { get; set; }
        public virtual string Password { get; set; }
        public virtual bool RememberMe { get; set; }
    }

    [Serializable]
    public class Location
    {
        public virtual int Id { get; set; }
        public virtual string Address1 { get; set; }
        public virtual string Address2 { get; set; }
        public virtual string City { get; set; }
        public virtual string State { get; set; }
        public virtual string PostalCode { get; set; }
        public virtual string Name { get; set; }
        public virtual string PhoneNumber { get; set; }
        public virtual string Country { get; set; }
    }

    [Serializable]
    public class ActiveOrUpcomingCampaign
    {
        public virtual int Id { get; set; }
        public virtual string ImageUrl { get; set; }
        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public virtual DateTimeOffset StartDate { get; set; }
        public virtual DateTimeOffset EndDate { get; set; }
    }

    [Serializable]
    public class ActiveOrUpcomingEvent
    {
        public virtual int Id { get; set; }
        public virtual string ImageUrl { get; set; }
        public virtual string Name { get; set; }
        public virtual string CampaignName { get; set; }
        public virtual string CampaignManagedOrganizerName { get; set; }
        public virtual string Description { get; set; }
        public virtual DateTimeOffset StartDate { get; set; }
        public virtual DateTimeOffset EndDate { get; set; }
    }

    [Serializable]
    public class CampaignSummaryViewModel
    {
        public virtual int Id { get; set; }
        public virtual string Title { get; set; }
        public virtual string Description { get; set; }
        public virtual string ImageUrl { get; set; }
        public virtual string OrganizationName { get; set; }
        public virtual string Headline { get; set; }
    }

    [Serializable]
    public class IndexViewModel
    {
        public virtual List<ActiveOrUpcomingEvent> ActiveOrUpcomingEvents { get; set; }
        public virtual CampaignSummaryViewModel FeaturedCampaign { get; set; }
        public virtual bool IsNewAccount { get; set; }
        public bool HasFeaturedCampaign => FeaturedCampaign != null;
    }

    [Serializable]
    public class MyEventsListerViewModel
    {
        // the orginal type defined these fields as IEnumerable,
        // but XmlSerializer failed to serialize them with "cannot serialize member because it is an interface" error
        public virtual List<MyEventsListerItem> CurrentEvents { get; set; } = new List<MyEventsListerItem>();
        public virtual List<MyEventsListerItem> FutureEvents { get; set; } = new List<MyEventsListerItem>();
        public virtual List<MyEventsListerItem> PastEvents { get; set; } = new List<MyEventsListerItem>();
    }

    [Serializable]
    public class MyEventsListerItem
    {
        public virtual int EventId { get; set; }
        public virtual string EventName { get; set; }
        public virtual DateTimeOffset StartDate { get; set; }
        public virtual DateTimeOffset EndDate { get; set; }
        public virtual string TimeZone { get; set; }
        public virtual string Campaign { get; set; }
        public virtual string Organization { get; set; }
        public virtual int VolunteerCount { get; set; }

        public virtual List<MyEventsListerItemTask> Tasks { get; set; } = new List<MyEventsListerItemTask>();
    }

    [Serializable]
    public class MyEventsListerItemTask
    {
        public virtual string Name { get; set; }
        public virtual DateTimeOffset? StartDate { get; set; }
        public virtual DateTimeOffset? EndDate { get; set; }

        public string FormattedDate
        {
            get
            {
                if (!StartDate.HasValue || !EndDate.HasValue)
                {
                    return null;
                }

                var startDateString = string.Format("{0:g}", StartDate.Value);
                var endDateString = string.Format("{0:g}", EndDate.Value);

                return string.Format($"From {startDateString} to {endDateString}");
            }
        }
    }
}
