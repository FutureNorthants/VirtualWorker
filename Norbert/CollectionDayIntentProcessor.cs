using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
//using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Norbert;

public class CollectionDayIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context)
    {
        IDictionary<String, String> requestAttributes = lexEvent.RequestAttributes ?? new Dictionary<String, String>();
        IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots = lexEvent.Interpretations[0].Intent.Slots;
        LexV2.LexIntentV2.LexSlotValueV2 slotValue = slots["Postcode"].Value;

        return Close(
                    "Default",
                    "Fulfilled",
                    getBinCollectionDetails(slotValue.InterpretedValue),
                    requestAttributes
                );
    }

    private String getBinCollectionDetails(String postCode)
    {
        //TODO parameterise!
        String collectionDetailsURL = "https://mycouncil-test.northampton.digital/BinRoundFinder?postcode=";
        try
        {
            HttpClient collectionClient = new HttpClient();
            HttpResponseMessage responseMessage = collectionClient.GetAsync(collectionDetailsURL+postCode).Result;
            responseMessage.EnsureSuccessStatusCode();
            String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
            JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
            if (jsonResponse["result"]!.GetValue<String>().ToLower().Equals("success")&&
                jsonResponse["rounds"]!.GetValue<String>().ToLower().Equals("single"))
            {
                return "Your collection day is " + jsonResponse["day"]!.GetValue<String>() +
                       ", please put out your " + jsonResponse["type"]!.GetValue<String>() + " bin by 6am";
            }
            else
            {
                return "Doh! : " + postCode;
            }
        }
        catch (Exception error)
        {
            return "Error : " + error.Message;
        }
    }
}