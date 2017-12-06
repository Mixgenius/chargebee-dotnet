using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using ChargeBee.Api;
using ChargeBee.Models;
using ChargeBee.Models.Enums;
using System.Threading.Tasks;
using Xunit;

namespace ChargeBee.Test
{
    public class ApiTest
    {
        public ApiTest()
        {
            ApiConfig.Proto = "http";
            ApiConfig.DomainSuffix = "localcb.com:8080";
            ApiConfig.Configure("mannar-test", "__dev__FhJgi9KugVCv9yO8zosAFC11lYCEAufI");
        }

        /*[Fact]
        public async Task TestConfig()
        {
            Assert.Equal("https://guidebot-test.chargebee.com/api/v2", ApiConfig.Instance.ApiBaseUrl);
        }*/

        [Fact]
        public async Task TestStatusCode()
        {
            ListResult result = await Event.List().Request();
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public async Task TestListEvents()
        {
            ListResult result = await Event.List().Request();

            foreach (var item in result.List)
            {
                Event evnt = item.Event;

                Assert.NotNull(evnt);
                Assert.NotNull(evnt.Id);

                Subscription subs = evnt.Content.Subscription;

                Assert.NotNull(subs);
                Assert.NotNull(subs.Id);
            }
        }

        [Fact]
        public async Task TestListEventsOffset()
        {
            ListResult result1 = await Event.List().Limit(1).Request();
            Assert.NotEmpty(result1.List);

            ListResult result2 = await Event.List().Limit(1).Offset(result1.NextOffset).Request();
            Assert.NotEmpty(result2.List);

            Assert.NotEqual(result1.List[0].Event.Id, result2.List[0].Event.Id);
        }

        [Fact]
        public async Task TestRetrieveEvent()
        {
            ListResult list = await Event.List().Limit(1).Request();
            Assert.NotEmpty(list.List);

            Event eventFromList = list.List[0].Event;

            EventTypeEnum? type = eventFromList.EventType;
            Assert.NotNull(type);

            EntityResult result = await Event.Retrieve(eventFromList.Id).Request();
            Event retrievedEvent = result.Event;
            Assert.Equal(eventFromList.Id, retrievedEvent.Id);
            Assert.Equal(eventFromList.OccurredAt, retrievedEvent.OccurredAt);
            Assert.Equal(eventFromList.Content.ToString(), retrievedEvent.Content.ToString());
        }

        [Fact]
        public async Task TestRetrieveEventNotFound()
        {
            await Assert.ThrowsAsync<ApiException>(() => Event.Retrieve("not_existent_id").Request());
        }

        [Fact]
        public async Task TestCreateSubscription()
        {
            string planId = "enterprise_half_yearly";

            EntityResult result = await Subscription.Create()
                              .PlanId(planId)
                              .CustomerEmail("john@user.com")
                              .CustomerFirstName("John")
                              .CustomerLastName("Wayne")
                              .AddonId(1, "on_call_support").Request();

            Subscription subs = result.Subscription;
            Assert.NotNull(subs);
            Assert.Equal(planId, subs.PlanId);
        }

        [Fact]
        public async Task TestListSubscriptions()
        {
            ListResult result = await Subscription.List().Request();

            foreach (var item in result.List)
            {
                Subscription subs = item.Subscription;

                Assert.NotNull(subs);
                Assert.NotNull(subs.Id);
            }
        }

        [Fact]
        public async Task TestRetrieveSubscriptions()
        {
            EntityResult result = await Subscription.Create().PlanId("enterprise_half_yearly").Request();
            Subscription subs1 = result.Subscription;
            Assert.NotNull(subs1);

            result = await Subscription.Retrieve(subs1.Id).Request();
            Subscription subs2 = result.Subscription;
            Assert.NotNull(subs2);

            Assert.Equal(subs1.Status, subs2.Status);
        }

        [Fact]
        public async Task TestUpdateSubscription()
        {
            EntityResult result = await Subscription.Create().PlanId("enterprise_half_yearly").Request();
            Subscription subs1 = result.Subscription;
            Assert.NotNull(subs1);
            Assert.NotEqual("basic", subs1.PlanId);

            result = await Subscription.Update(subs1.Id)
                .PlanId("basic")
                .AddonId(1, "on_call_support")
                .CardGateway(GatewayEnum.PaypalPro)
                .Request();

            Subscription subs2 = result.Subscription;
            Assert.NotNull(subs2);
            Assert.Equal("basic", subs2.PlanId);

            List<Subscription.SubscriptionAddon> addons = subs2.Addons;
            Assert.NotNull(addons);
        }

        [Fact]
        public async Task TestCancelSubscription()
        {
            EntityResult result = await Subscription.Create().PlanId("enterprise_half_yearly").Request();
            Subscription subs1 = result.Subscription;
            Assert.NotNull(subs1);

            result = await Subscription.Cancel(subs1.Id).Request();

            Subscription subs2 = result.Subscription;
            Assert.NotNull(subs2);
            Assert.True(DateTime.Now.AddMinutes(-5) < subs2.CancelledAt &&
                DateTime.Now.AddMinutes(5) > subs2.CancelledAt);
        }

        [Fact]
        public async Task TestReactivateSubscriptionError()
        {
            EntityResult result = await Subscription.Create().PlanId("enterprise_half_yearly").Request();
            Subscription subs = result.Subscription;
            result = await Subscription.Cancel(subs.Id).Request();

            await Assert.ThrowsAsync<ApiException>(() =>
                Subscription.Reactivate(subs.Id).
                    TrialEnd((long)(DateTime.Now.AddDays(5) - new DateTime(1970, 1, 1)).TotalSeconds)
                        .Request());
        }

        [Fact]
        public void TestEventCtor()
        {
            string s = "{\"id\": \"ev_HwqE2wPNy5PuFEcd\",\"occurred_at\": 1361453444,\"webhook_status\": \"not_configured\",\"object\": \"event\",\"content\": {\"subscription\": {\"id\": \"HwqE2wPNy5PuEycb\",\"plan_id\": \"enterprise_half_yearly\",\"plan_quantity\": 1,\"status\": \"in_trial\",\"trial_start\": 1361453444,\"trial_end\": 1364045444,\"created_at\": 1361453444,\"due_invoices_count\": 0,\"object\": \"subscription\"},\"customer\": {\"id\": \"HwqE2wPNy5PuEycb\",\"created_at\": 1361453444,\"object\": \"customer\",\"card_status\": \"no_card\"}},\"event_type\": \"subscription_created\"}";

            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(s)))
            {
                Event evnt = new Event(ms);
                Assert.Equal("ev_HwqE2wPNy5PuFEcd", evnt.Id);
                Assert.Equal(
                    DateTime.ParseExact("2013-02-21 17:30:44", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    evnt.OccurredAt);
            }
        }

        [Fact]
        public async Task TestHostedPageCheckoutNew()
        {
            EntityResult result = await HostedPage.CheckoutNew()
                              .CustomerEmail("john@user.com")
                              .CustomerFirstName("John")
                              .CustomerLastName("Wayne")
                              .SubscriptionPlanId("enterprise_half_yearly")
                              .AddonId(1, "on_call_support").Request();

            HostedPage hostedPage = result.HostedPage;
            Assert.NotNull(hostedPage);
        }

        [Fact]
        public async Task TestHostedPageCheckoutExisting()
        {
            EntityResult result = await HostedPage.CheckoutExisting()
                  .SubscriptionId("HoR7OsYNy5YBOlyn")
                  .SubscriptionPlanId("enterprise_half_yearly")
                  .AddonId(1, "on_call_support").Request();

            HostedPage hostedPage = result.HostedPage;
            Assert.NotNull(hostedPage);
        }

        [Fact]
        public async Task TestRetrieveHostedPage()
        {
            EntityResult result = await HostedPage.CheckoutNew()
                              .CustomerEmail("john@user.com")
                              .CustomerFirstName("John")
                              .CustomerLastName("Wayne")
                              .SubscriptionPlanId("enterprise_half_yearly")
                              .AddonId(1, "on_call_support").Request();

            HostedPage hostedPage1 = result.HostedPage;
            Assert.NotNull(hostedPage1);

            result = await HostedPage.Retrieve(hostedPage1.Id).Request();

            HostedPage hostedPage2 = result.HostedPage;
            Assert.NotNull(hostedPage2);

            Assert.Equal(hostedPage2.Url, hostedPage2.Url);
        }
    }
}
