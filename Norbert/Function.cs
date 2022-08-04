using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Norbert;

public class Function : AbstractIntentProcessor
{
    private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
    private static readonly String secretName = "ChatBot";
    private static readonly String secretAlias = "AWSCURRENT";
    public LexV2Response FunctionHandler(LexEventV2 lexEvent, ILambdaContext context)
    {
       
        IIntentProcessor process;
        IDictionary<String, String>? requestAttributes = null;
        IDictionary<String, String>? sessionAttributes = null;
        IDictionary<String, LexV2.LexIntentV2.LexSlotV2>? slots = null;
       

        try
        {
            Secrets secrets = GetSecrets().Result;
            String qnaAuth = secrets.QnaAuthTest;
            String qnaURL = secrets.QnaUrlTest;
            String MinConfidenceLevel = secrets.MinConfidenceTest;
            long MinConfidence = 0;

            try
            {
                if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                {
                    qnaAuth = secrets.QnaAuthLive;
                    qnaURL = secrets.QnaUrlLive;
                    MinConfidenceLevel = secrets.MinConfidenceLive;
                }
            }
            catch (Exception) { }

            try
            {
                if(long.TryParse(MinConfidenceLevel, out long tempConfidence))
                {
                    MinConfidence = tempConfidence;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR converting MinConfidenceLevel   : " + MinConfidenceLevel);
                Console.WriteLine("ERROR : " + error.Message);
                Console.WriteLine("ERROR : " + error.StackTrace);
            }

            try
            {
                Console.WriteLine("------------");
                Console.WriteLine("Secrets     : ");
                Console.WriteLine("Secret URL    : " + secrets.QnaUrlTest);
                Console.WriteLine("Bot           : " + lexEvent.Bot.Name);
                Console.WriteLine("Alias         : " + lexEvent.Bot.AliasId);
                Console.WriteLine("Version       : " + lexEvent.Bot.Version);
                Console.WriteLine("Intent        :  " + lexEvent.Interpretations[0].Intent.Name);
                Console.WriteLine("Transcription : " + lexEvent.InputTranscript);
                Console.WriteLine("Source        : " + lexEvent.InvocationSource);
                Console.WriteLine("Next Type     : " + lexEvent.ProposedNextState.DialogAction.Type);
                Console.WriteLine("slotToElicit  : " + lexEvent.ProposedNextState.DialogAction.SlotToElicit);
            }
            catch (Exception) { }

            requestAttributes = lexEvent.RequestAttributes ?? new Dictionary<String, String>();
            sessionAttributes = lexEvent.SessionState.SessionAttributes ?? new Dictionary<String, String>();
            slots = lexEvent.Interpretations[0].Intent.Slots;

            try
            {
                requestAttributes = lexEvent.RequestAttributes ?? new Dictionary<String, String>();
                sessionAttributes = lexEvent.SessionState.SessionAttributes ?? new Dictionary<String, String>();
                slots = lexEvent.Interpretations[0].Intent.Slots;
            }
            catch (Exception) { }

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
                    case "handover":
                        process = new HandoverIntentProcessor();
                        break;
                    case "leaveamessage":
                        process = new LeaveAMessageIntentProcessor();
                        break;
                    case "stop":
                        process = new StopIntentProcessor();
                        break;
                    default:
                        process = new DefaultIntentProcessor(qnaAuth,qnaURL,MinConfidence);
                        break;
                }
            }
            catch (Exception)
            {
                process = new DefaultIntentProcessor(qnaAuth, qnaURL, MinConfidence);
            }
            return process.Process(lexEvent, context, sessionAttributes, requestAttributes, slots);
        }
        catch(Exception error) 
        {
            return Handover(requestAttributes, sessionAttributes);
        }     
    }

    private static async Task<Secrets> GetSecrets()
    {
        IAmazonSecretsManager client = new AmazonSecretsManagerClient(primaryRegion);

        GetSecretValueRequest request = new GetSecretValueRequest();
        request.SecretId = secretName;
        request.VersionStage = secretAlias;

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

    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<string, string> sessionAttributes, IDictionary<string, string> requestAttributes, IDictionary<string, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        throw new NotImplementedException();
    }
}
public class Secrets
{
    public String? QnaAuthLive { get; set; }
    public String? QnaUrlLive { get; set; }
    public String? QnaAuthTest { get; set; }
    public String? QnaUrlTest { get; set; }
    public String MinConfidenceTest { get; set; } = string.Empty;
    public String MinConfidenceLive { get; set; } = string.Empty;
}
