using Amazon;
using Amazon.CodePipeline;
using Amazon.CodePipeline.Model;
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
    }

    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public static async Task Handler(Job job, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Log("Starting execution");

        using AmazonCodePipelineClient codePipelineClient = new();
        string jobId = job.Id;
        context.Log(job.Id);
        context.Log(job.AccountId);
        context.Log(job.Data.ToString());

        using AmazonRoute53Client route53Client = new(RegionEndpoint.EUWest2);
        context.Log("Route53 client created");

        ListHostedZonesByNameResponse response = await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = "integration.michaelkillingbeck.com.",
        }).ConfigureAwait(false);

        HostedZone? hostedZone = response.HostedZones.FirstOrDefault();
        string hostedZoneId;

        if (hostedZone != null)
        {
            context.Log("Found hosted zone");
            hostedZoneId = hostedZone.Id;
        }
        else
        {
            context.Log("No hosted zone found, stopping execution");
            _ = await codePipelineClient.PutJobSuccessResultAsync(new PutJobSuccessResultRequest
            {
                JobId = jobId,
            }).ConfigureAwait(false);
            return;
        }

        ListResourceRecordSetsResponse resourceRecordSets = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
        {
            HostedZoneId = hostedZoneId,
        }).ConfigureAwait(false);

        ResourceRecordSet? recordSet = resourceRecordSets.ResourceRecordSets.Find(recordSet => recordSet.Type == RRType.A);

        if (recordSet != null)
        {
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

            context.Log("Change set created; sending request");
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
                    context.Log("Change is still pending");
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
                else
                {
                    context.Log("Change is complete");
                    _ = await codePipelineClient.PutJobSuccessResultAsync(new PutJobSuccessResultRequest
                    {
                        JobId = jobId,
                    }).ConfigureAwait(false);
                    return;
                }
            }
        }

        context.Log("No record set found, nothing to do");
        _ = await codePipelineClient.PutJobSuccessResultAsync(new PutJobSuccessResultRequest
        {
            JobId = jobId,
        }).ConfigureAwait(false);
    }

    private static void Log(this ILambdaContext context, string message)
    {
        context.Logger.LogInformation(message);
    }
}
