using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Amazon.S3.Transfer;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace hello_bedrock_image;

public class Function
{
    IAmazonS3 S3Client { get; set; }

    const string modelId = "stability.stable-diffusion-xl-v0";
    const string modelRequestBody = "{{'text_prompts':[{{'text':'{0}'}}],'cfg_scale':10,'seed':0,'steps':50}}";

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }

            try
            {
                // Only process .txt files. If the output .png file already exists, do not re-process.

                if (!s3Event.Object.Key.EndsWith(".txt")) continue;

                var textKey = s3Event.Object.Key;
                var imageKey = textKey.Replace(".txt", ".png");
                context.Logger.LogInformation($"10 Processing {textKey}");

                // check whether output .png file alredy exists

                bool alreadyProcessed = true;
                try
                {
                    await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, imageKey);
                    context.Logger.LogInformation($"15 Output file {imageKey} already exists, skipping processing");
                }
                catch (AmazonS3Exception)
                {
                    alreadyProcessed = false;
                }

                if (alreadyProcessed) continue;

                // Read the image description from the .txt file S3 object.

                context.Logger.LogInformation($"20 Reading figure caption from .txt file S3 object");

                var S3response = await S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                StreamReader reader = new StreamReader(S3response.ResponseStream);
                string prompt = reader.ReadToEnd();

                context.Logger.LogInformation(prompt);

                // Generate a Bedrock image for the image description.

                context.Logger.LogInformation("50 Creating Bedrock request");
                var bedrockClient = new AmazonBedrockRuntimeClient(RegionEndpoint.USWest2);

                JObject json = JObject.Parse(String.Format(modelRequestBody, prompt));
                byte[] byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json));
                MemoryStream stream = new MemoryStream(byteArray);

                var bedrockRequest = new InvokeModelRequest()
                {
                    ModelId = "stability.stable-diffusion-xl-v0",
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = stream
                };

                // Invoke model and capture base64-encoded image from response.

                context.Logger.LogInformation("60 Invoking model");

                var bedrockResponse = await bedrockClient.InvokeModelAsync(bedrockRequest);
                string responseBody = new StreamReader(bedrockResponse.Body).ReadToEnd();

                dynamic parseJson = JsonConvert.DeserializeObject(responseBody);
                string base64 = parseJson!.artifacts[0].base64;

                // Convert base64 to image stream and save .png file to to S3.

                context.Logger.LogInformation("70 Converting image to a .png image stream");

                var bytes = Convert.FromBase64String(base64!);
                var image = Image.Load(bytes);

                Console.WriteLine($"90 Saving image as S3 object {imageKey} in bucket {s3Event.Bucket.Name}");

                using (var S3utility = new TransferUtility())
                using (MemoryStream msImage = new MemoryStream())
                {
                    await image.SaveAsPngAsync(msImage);
                    await S3utility.UploadAsync(msImage, s3Event.Bucket.Name, imageKey);
                }
            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogError(e.Message);
                context.Logger.LogError(e.StackTrace);
                throw;
            }

            context.Logger.LogInformation("99 end");
        }
    }
}