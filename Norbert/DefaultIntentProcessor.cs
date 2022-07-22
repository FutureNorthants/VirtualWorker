using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
//using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Norbert;

public class DefaultIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        String[] responseMessages = {
            getFAQResponseAsync(lexEvent.InputTranscript)
        };

        try
        {
            return Close(
             "Default",
             "Fulfilled",
             responseMessages,
             requestAttributes,
             sessionAttributes
             );
        }
        catch(Exception error)
        {
            responseMessages[0] = "Please wait whilst we connect you to a member of staff to help with this query";
            if (error is not ApplicationException)
            {
                Console.WriteLine("Error : " + error.Message);
                Console.WriteLine(error.StackTrace);
            }
            return Close(lexEvent.Interpretations[0].Intent.Name, "Failed", responseMessages, requestAttributes, sessionAttributes);
        }
 
    }

    private String getFAQResponseAsync(String query)
    {
        //TODO Move to secrets or params
        String qnaAuthorization = "EndpointKey d77cb979-073f-4427-8d10-c672fec9e5cd";
        String qnaURL = "https://nbcwebservice.azurewebsites.net/qnamaker/knowledgebases/9e0449aa-e598-4c5f-a848-de61d123f91e/generateAnswer";
        try
        {
            HttpClient qnaClient = new HttpClient();
            qnaClient.DefaultRequestHeaders.Add("Authorization", qnaAuthorization);
            HttpResponseMessage responseMessage = qnaClient.PostAsync(qnaURL,new StringContent("{'question':'" + HttpUtility.UrlEncode(query) + "'}", Encoding.UTF8, "application/json")).Result;
            responseMessage.EnsureSuccessStatusCode();
            String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
            JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
            JsonArray answers = jsonResponse!["answers"]!.AsArray()!;
            float score = answers[0]!["score"]!.GetValue<float>();
            //TODO parameterise!
            if(score > 50)
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