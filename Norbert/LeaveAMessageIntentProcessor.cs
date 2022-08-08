using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Norbert.LexV2.LexIntentV2;

namespace Norbert;

public class LeaveAMessageIntentProcessor : AbstractIntentProcessor
{
    readonly String? cxmEndPoint;
    readonly String? cxmAPIKey;
    readonly String? cxmAPIName;
    readonly String? cxmAPICaseType;
    public LeaveAMessageIntentProcessor(String? cxmEndPoint, String? cxmAPIKey, String? cxmAPIName, String? cxmAPICaseType)
    {
        this.cxmEndPoint = cxmEndPoint;
        this.cxmAPIKey = cxmAPIKey;
        this.cxmAPICaseType = cxmAPICaseType;
        this.cxmAPIName = cxmAPIName;
    }
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        Console.WriteLine(" ");
        Console.WriteLine("LeaveAMessageIntentProcessor Started");

        switch (lexEvent.InvocationSource)
        {
            case "DialogCodeHook":
                Console.WriteLine("DialogCodeHook");
                lexEvent.SessionState.Intent.Slots.TryGetValue("CustomerEmail", out LexSlotV2? customerEmail);
                try
                {
                    if (lexEvent.ProposedNextState.DialogAction.SlotToElicit.ToLower().Equals("message") && 
                        !customerEmail.Value.ResolvedValues[0].Equals(lexEvent.InputTranscript))
                    {
                        return CloseIntentWithCase(customerEmail.Value.ResolvedValues[0], lexEvent.InputTranscript, requestAttributes, sessionAttributes);    
                    }
                    else
                    {                       
                        return Delegate2(lexEvent);
                    }
                }
                catch (Exception)
                {
                    //TODO back to handover with message (possibly supress option)
                    return CloseIntentWithCase(customerEmail.Value.ToString(), lexEvent.InputTranscript, requestAttributes, sessionAttributes);
                }
                
                
            case "FulfillmentCodeHook":
                lexEvent.SessionState.Intent.Slots.TryGetValue("CustomerEmail", out customerEmail);
                lexEvent.SessionState.Intent.Slots.TryGetValue("Message", out LexSlotV2? message);
                return CloseIntentWithCase(customerEmail.Value.ResolvedValues[0], message.Value.ToString(), requestAttributes, sessionAttributes);
            default:
                Console.WriteLine("ERROR Unknown InvocationSource : " + lexEvent.InvocationSource);
                return Delegate("LeaveAMessage",
                                requestAttributes,
                                sessionAttributes
                                );
        }
        Console.WriteLine("LeaveAMessageIntentProcessor Ended");
    }

    private LexV2Response CloseIntentWithCase(String? emailAddress, String message, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
    {
        String? personRef = GetPersonReferenceAsync(emailAddress, cxmEndPoint, cxmAPIKey);

        String? caseReference = CreateCXMCase(emailAddress,message,personRef,cxmEndPoint,cxmAPIKey,cxmAPIName, cxmAPICaseType);

        String[] responseMessages1 = { caseReference };
        return Close("LeaveAMessage",
             "Fulfilled",
             responseMessages1,
             requestAttributes,
             sessionAttributes
             );
    }

    private static String? GetPersonReferenceAsync(String EmailFrom, String cxmEndPoint, String cxmAPIKey)
    {
        using (var client = new HttpClient())
        {
            try
            {
                HttpResponseMessage responseMessage = client.GetAsync(cxmEndPoint + "/api/service-api/norbert/user/" + EmailFrom + "?key=" + cxmAPIKey).Result;
                responseMessage.EnsureSuccessStatusCode();
                String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
                JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
                JsonArray answers = jsonResponse!["answers"]!.AsArray()!;
                String reference  = jsonResponse!["person"]!["reference"]!.ToString();
                return jsonResponse!["person"]!["reference"]!.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("Person NOT found : " + error.Message);
                return null;
            }
        }
    }

    private String CreateCXMCase(String email, String message, String person, String cxmEndPoint, String cxmAPIKey, String cxmAPIName, String cxmAPICaseType)
    {
        Dictionary<String, Object> values = new Dictionary<String, Object>
                                    {
                                        { "first-name", "" },
                                        { "surname", "" },
                                        { "email", email },
                                        { "subject", "Your online chat query" },
                                        { "enquiry-details", message },
                                        { "customer-has-updated", false },
                                        { "unitary", true },
                                        { "contact-us", false },
                                        { "district", false },
                                        {"merge-into-pdf", "no" },
                                        { "email-id", "x"}
                                    };

        try
        {
            HttpClient client = new HttpClient();
            if (person is not null)
            {
                values.Add("person", person);
            }
            String jsonContent = System.Text.Json.JsonSerializer.Serialize(values);
            HttpContent content = new StringContent(jsonContent);
            Console.WriteLine($"cxmEndPoint  : {cxmEndPoint}");
            Console.WriteLine($"cxmAPIName  : {cxmAPIName}");
            Console.WriteLine($"cxmAPICaseType  : {cxmAPICaseType}");
            Console.WriteLine($"cxmAPIKey  : {cxmAPIKey}");
            Console.WriteLine($"Form Data2  : {jsonContent}");
            HttpResponseMessage responseFromJadu = client.PostAsync(cxmEndPoint + "/api/service-api/" + cxmAPIName + "/" + cxmAPICaseType + "/case/create?key=" + cxmAPIKey, content).Result;
            String responseString = responseFromJadu.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"Response from Jadu {responseString}");

            if (responseFromJadu.IsSuccessStatusCode)
            {
                JsonNode jsonResponse = JsonNode.Parse(responseString)!;
                String reference = jsonResponse!["reference"]!.AsValue().ToString();
                return reference;
            }
            else
            {
                Console.WriteLine($"ERROR - No case created : " + responseFromJadu.StatusCode);
                return "Oops";
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Error Response from Jadu {error.ToString()}");
            return "Oops2";
        }
    }
}