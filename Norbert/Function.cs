using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Norbert;

public class Function
{
    private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
    private static readonly String secretName = "ChatBot";
    private static readonly String secretAlias = "AWSCURRENT";
    //private Secrets? secrets = null;

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public LexV2Response FunctionHandler(LexEventV2 lexEvent, ILambdaContext context)
    {
       
        IIntentProcessor process;
        IDictionary<String, String>? requestAttributes = null;
        IDictionary<String, String>? sessionAttributes = null;
        IDictionary<String, LexV2.LexIntentV2.LexSlotV2>? slots = null;

        try
        {
            Secrets secrets = GetSecrets().Result;
            try
            {
                Console.WriteLine("------------");
                Console.WriteLine("Secrets     : ");
                Console.WriteLine("Secret URL    : " + secrets.qna_url_test);
                Console.WriteLine("Bot           : " + lexEvent.Bot.Name);
                Console.WriteLine("Alias         : " + lexEvent.Bot.AliasId);
                Console.WriteLine("Version       : " + lexEvent.Bot.Version);
                Console.WriteLine("Intent        :  " + lexEvent.Interpretations[0].Intent.Name);
                Console.WriteLine("Transcription : " + lexEvent.InputTranscript);
                Console.WriteLine("Source        : " + lexEvent.InvocationSource);
                Console.WriteLine("Next Type     : " + lexEvent.ProposedNextState.DialogAction.Type);
            }
            catch (Exception) { }

            IDictionary<String, String> requestAttributes = lexEvent.RequestAttributes ?? new Dictionary<String, String>();
            IDictionary<String, String> sessionAttributes = lexEvent.SessionState.SessionAttributes ?? new Dictionary<String, String>();
            IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots = lexEvent.Interpretations[0].Intent.Slots;
            try
            {
                switch (lexEvent.Interpretations[0].Intent.Name.ToLower())
                {
                    case "debug":
                        process = new DebugIntentProcessor();
                        break;
                    case "collectionday":
                        process = new CollectionDayIntentProcessor();
                        break;
                    default:
                        process = new DefaultIntentProcessor();
                        break;
                }
            }
            catch (Exception)
            {
                process = new DefaultIntentProcessor();
            }
            return process.Process(lexEvent, context, sessionAttributes, requestAttributes, slots);
        }
        catch(Exception error) 
        {
        }     
    }

    private static async Task<Secrets> GetSecrets()
    {
        IAmazonSecretsManager client = new AmazonSecretsManagerClient(primaryRegion);

<<<<<<< Updated upstream
        try
        {
            requestAttributes = lexEvent.RequestAttributes ?? new Dictionary<String, String>();
            sessionAttributes = lexEvent.SessionState.SessionAttributes ?? new Dictionary<String, String>();
            slots = lexEvent.Interpretations[0].Intent.Slots;
        }
        catch (Exception) { }
=======
        GetSecretValueRequest request = new GetSecretValueRequest();
        request.SecretId = secretName;
        request.VersionStage = secretAlias;
>>>>>>> Stashed changes

        try
        {
            GetSecretValueResponse response = await client.GetSecretValueAsync(request);
            Secrets? secrets = JsonSerializer.Deserialize<Secrets>(response.SecretString);
            if(secrets is not null)
            {
                return secrets;
            }
            else
            {
                throw new ApplicationException("Error Deserializing Secrets");
            }
        }
        catch (Exception error)
        {
            throw new ApplicationException("Error Getting Secrets", error);
        }
    }
}
public class Secrets
{
    public String? qna_auth_live { get; set; }
    public String? qna_url_live { get; set; }
    public String? qna_auth_test { get; set; }
    public String? qna_url_test { get; set; }
}
