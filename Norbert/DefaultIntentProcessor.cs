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
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context)
    {
        IDictionary<string, string> sessionAttributes = lexEvent.RequestAttributes ?? new Dictionary<string, string>();

        return Close(
                    "Default",
                    "Fulfilled",
                    getFAQResponseAsync(lexEvent.InputTranscript),
                    sessionAttributes
                );
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
                return "Beats me dude";
            }          
        }
        catch (Exception error)
        {
            return "Error : " + error.Message;
        }
    }
}