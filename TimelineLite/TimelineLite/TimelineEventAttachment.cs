using System;
using System.ComponentModel.DataAnnotations;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using TimelineLite.Requests.TimelineEventAttachments;
using TimelineLite.StorageModels;
using TimelineLite.StorageRepos;
using static TimelineLite.Requests.RequestHelper;
using static TimelineLite.Responses.ResponseHelper;

namespace TimelineLite
{
    public class TimelineEventAttachment : LambdaBase
    {
        public APIGatewayProxyResponse Create(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return Handle(() => CreateAttachment(request));
        }
        
        public APIGatewayProxyResponse GeneratePresignedUrl(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return Handle(() => GenerateAttachmentPresignedUrl(request));
        }
        
        public APIGatewayProxyResponse EditTitle(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return Handle(() => EditAttachmentTitle(request));
        }
        
        public APIGatewayProxyResponse Delete(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return Handle(() => DeleteAttachment(request));
        }
        
        private static APIGatewayProxyResponse CreateAttachment(APIGatewayProxyRequest request)
        {
            var timelineEventAttachmentRequest = ParsePutRequestBody<CreateTimelineEventAttachmentRequest>(request);
            ValidateTimelineEventAttachmentId(timelineEventAttachmentRequest.AttachmentId);
            ValidateTimelineEventAttachentTitle(timelineEventAttachmentRequest.Title);
            ValidateTimelineEventId(timelineEventAttachmentRequest.TimelineEventId);

            var timelineEventAttachment = new TimelineEventAttachmentModel
            {
                Id = timelineEventAttachmentRequest.AttachmentId,
                Title = timelineEventAttachmentRequest.Title,
                TimelineEventId = timelineEventAttachmentRequest.TimelineEventId
            };
            GetRepo(timelineEventAttachmentRequest.TenantId).CreateTimlineEventAttachment(timelineEventAttachment);

            return WrapResponse($"{JsonConvert.SerializeObject(timelineEventAttachment)}");
        }
        
        private static APIGatewayProxyResponse GenerateAttachmentPresignedUrl(APIGatewayProxyRequest request)
        {
            var tenantId = request.AuthoriseGetRequest();
            var attachmentId = request.Headers["AttachmentId"];
            ValidateTimelineEventAttachmentId(attachmentId);

            var s3Client = new AmazonS3Client(RegionEndpoint.EUWest1);
            var presignedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = "stewartw-test-bucket",
                Verb = HttpVerb.PUT,
                Key = $"{tenantId}/{attachmentId}",
                Expires = DateTime.Now.AddMinutes(15)
            });
            return WrapResponse(presignedUrl);
        }
        
        private static APIGatewayProxyResponse EditAttachmentTitle(APIGatewayProxyRequest request)
        {
            var timelineEventAttachmentRequest = ParsePutRequestBody<EditTimelineEventAttachmentTitleRequest>(request);

            ValidateTimelineEventAttachmentId(timelineEventAttachmentRequest.AttachmentId);
            ValidateTimelineEventAttachentTitle(timelineEventAttachmentRequest.Title);
            
            var repo = GetRepo(timelineEventAttachmentRequest.TenantId);
            var model = repo.GetModel(timelineEventAttachmentRequest.AttachmentId);
            model.Title = timelineEventAttachmentRequest.Title;
            repo.SaveModel(model);
            return WrapResponse($"{JsonConvert.SerializeObject(model)}");
        }
        
        private static APIGatewayProxyResponse DeleteAttachment(APIGatewayProxyRequest request)
        {
            var timelineEventAttachmentRequest = ParsePutRequestBody<DeleteTimelineEventAttachmentRequest>(request);

            ValidateTimelineEventAttachmentId(timelineEventAttachmentRequest.AttachmentId);
            
            var repo = GetRepo(timelineEventAttachmentRequest.TenantId);
            var model = repo.GetModel(timelineEventAttachmentRequest.AttachmentId);
            if(model.IsDeleted)
                return WrapResponse($"Cannot find attachment with Id {timelineEventAttachmentRequest.AttachmentId}", 404);
            model.IsDeleted = true;
            repo.SaveModel(model);
            return WrapResponse($"Successfully deleted Timeline event attachment: {timelineEventAttachmentRequest.AttachmentId}");
        }

        private static DynamoDbTimelineEventAttachmentRepository GetRepo(string tenantId)
        {
            return new DynamoDbTimelineEventAttachmentRepository(new AmazonDynamoDBClient(RegionEndpoint.EUWest1), tenantId);
        }
        
        private static void ValidateTimelineEventAttachmentId(string timelineEventAttachmentId)
        {
            if (string.IsNullOrWhiteSpace(timelineEventAttachmentId))
                throw new ValidationException("Invalid Timeline Id");
        }
        
        private static void ValidateTimelineEventId(string timelineEventId)
        {
            if (string.IsNullOrWhiteSpace(timelineEventId))
                throw new ValidationException("Invalid Timeline Id");
        }
        
        private static void ValidateTimelineEventAttachentTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidationException("Invalid Timeline Event Attachment Title");
        }
    }
}