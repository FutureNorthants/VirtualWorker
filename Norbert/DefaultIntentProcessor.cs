using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace Norbert;

public class DefaultIntentProcessor : AbstractIntentProcessor
{
    readonly String qnaAuth;
    readonly String qnaURL;
    readonly long MinConfidenceLevel;
    public DefaultIntentProcessor(String qnaAuth, String qnaURL, long MinConfidenceLevel)
    {
        this.qnaAuth = qnaAuth;
        this.qnaURL = qnaURL;   
        this.MinConfidenceLevel = MinConfidenceLevel;
    }
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        try
        {
            String[] responseMessages = {
                 GetFAQResponseAsync(lexEvent.InputTranscript)
            };
            return Close(
             "Default",
             "Fulfilled",
             responseMessages,
             requestAttributes,
             sessionAttributes
             );
        }
        catch (ApplicationException error)
        {
            Console.WriteLine("ERROR : Getting FAQ Response");
            Console.WriteLine("ERROR : " + error.Message);
            Console.WriteLine("ERROR : " + error.StackTrace);
            return Handover(requestAttributes, sessionAttributes);
        }
        catch (Exception error)
        {
            Console.WriteLine("ERROR : Getting FAQ Response");
            Console.WriteLine("ERROR : " + error.Message);
            Console.WriteLine("ERROR : " + error.StackTrace);
            return Handover(requestAttributes, sessionAttributes);
        }
    }
    private String GetFAQResponseAsync(String query)
    {
        try
        {
            HttpClient qnaClient = new();
            qnaClient.DefaultRequestHeaders.Add("Authorization", qnaAuth);
            HttpResponseMessage responseMessage = qnaClient.PostAsync(qnaURL,new StringContent("{'question':'" + HttpUtility.UrlEncode(query) + "'}", Encoding.UTF8, "application/json")).Result;
            responseMessage.EnsureSuccessStatusCode();
            String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
            JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
            JsonArray answers = jsonResponse!["answers"]!.AsArray()!;
            float score = answers[0]!["score"]!.GetValue<float>();
            if (score > MinConfidenceLevel)
            {
                return answers[0]!["answer"]!.GetValue<String>();
            }
            else
            {
                throw new ApplicationException("");
            }          
        }
        catch (Exception error)
        {
            throw new Exception(error.Message,error);
        }
    }
}