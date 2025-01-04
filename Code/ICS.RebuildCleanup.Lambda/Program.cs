using Amazon;
using Amazon.Lambda.Core;
using Amazon.Route53;
using Amazon.Route53.Model;

namespace ICS.RebuildCleanup.Lambda;

#pragma warning disable CA1052
public static class Program
#pragma warning restore CA1052
{
    public static void Main()
    {
        Handler(null!).GetAwaiter().GetResult();
    }

    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public static async Task Handler(ILambdaContext context)
    {
        using AmazonRoute53Client route53Client = new(RegionEndpoint.EUWest2);

        ListHostedZonesByNameResponse response = await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = "integration.michaelkillingbeck.com.",
        }).ConfigureAwait(false);

        HostedZone? hostedZone = response.HostedZones.FirstOrDefault();
        string hostedZoneId;

        if (hostedZone != null)
        {
            hostedZoneId = hostedZone.Id;
        }
        else
        {
            return;
        }

        ListResourceRecordSetsResponse resourceRecordSets = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
        {
            HostedZoneId = hostedZoneId,
        }).ConfigureAwait(false);

        ResourceRecordSet? recordSet = resourceRecordSets.ResourceRecordSets.Find(recordSet => recordSet.Type == RRType.A);

        Change deleteChange = new()
        {
            Action = ChangeAction.DELETE,
            ResourceRecordSet = recordSet,
        };

        ChangeBatch batchRequest = new()
        {
            Changes = [deleteChange],
        };

        ChangeResourceRecordSetsRequest recordsetRequest = new()
        {
            HostedZoneId = hostedZoneId,
            ChangeBatch = batchRequest,
        };

        ChangeResourceRecordSetsResponse recordsetResponse =
            await route53Client.ChangeResourceRecordSetsAsync(recordsetRequest).ConfigureAwait(false);

        GetChangeRequest changeRequest = new()
        {
            Id = recordsetResponse.ChangeInfo.Id,
        };

        while (true)
        {
            GetChangeResponse changeStatus = await route53Client.GetChangeAsync(changeRequest).ConfigureAwait(false);

            if (changeStatus.ChangeInfo.Status == ChangeStatus.PENDING)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            else
            {
                break;
            }
        }
    }
}
