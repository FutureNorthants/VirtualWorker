// Imports I'm using
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Norbert;

// Put the following three classes into your project to use LexV2 in C#!
// WIP

public class LexV2
{
    public class LexIntentV2
    {
        [JsonPropertyName("slots")]
        public IDictionary<string, LexSlotV2> Slots { get; set; }

        public class LexSlotV2
        {
            [JsonPropertyName("value")]
            public LexSlotValueV2 Value { get; set; }
        }

        public class LexListSlotV2 : LexSlotV2
        {
            [JsonPropertyName("shape")]
            public string Shape
            {
                get
                {
                    return "List";
                }
            }

            [JsonPropertyName("values")]
            public List<LexListSlotValueV2> Values { get; set; }
        }

        public class LexSlotValueV2
        {
            [JsonPropertyName("interpretedValue")]
            public string InterpretedValue { get; set; }

            [JsonPropertyName("originalValue")]
            public string OriginalValue { get; set; }

            [JsonPropertyName("resolvedValues")]
            public List<string> ResolvedValues { get; set; }
        }

        public class LexListSlotValueV2
        {
            [JsonPropertyName("shape")]
            public string Shape
            {
                get
                {
                    return "Scalar";
                }
            }

            [JsonPropertyName("value")]
            public List<LexSlotValueV2> Value { get; set; }
        }

        [JsonPropertyName("confirmationState")]
        public string ConfirmationState { get; set; }
        // Confirmed | Denied | None

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }
        // InProgress | Waiting | Fulfilled | ReadyForFulfillment | Failed

        [JsonPropertyName("kendraResponse")]
        public object KendraResponse { get; set; }
    }

    public class LexSlotV2
    {
        [JsonPropertyName("value")]
        public LexSlotValueV2 Value { get; set; }
    }

    public class LexSlotValueV2
    {
        [JsonPropertyName("interpretedValue")]
        public string InterpretedValue { get; set; }

        [JsonPropertyName("originalValue")]
        public string OriginalValue { get; set; }

        [JsonPropertyName("resolvedValues")]
        public List<string> ResolvedValues { get; set; }
    }

    public class LexListSlotV2 : LexSlotV2
    {
        [JsonPropertyName("shape")]
        public string Shape
        {
            get
            {
                return "List";
            }
        }

        [JsonPropertyName("values")]
        public List<LexListSlotValueV2> Values { get; set; }
    }

    public class LexListSlotValueV2
    {
        [JsonPropertyName("shape")]
        public string Shape
        {
            get
            {
                return "Scalar";
            }
        }

        [JsonPropertyName("value")]
        public List<LexSlotValueV2> Value { get; set; }
    }
}

public class LexEventV2
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; }

    [JsonPropertyName("inputTranscript")]
    public string InputTranscript { get; set; }

    [JsonPropertyName("interpretations")]
    public List<LexInterpretationV2> Interpretations { get; set; }

    public class LexInterpretationV2
    {
        [JsonPropertyName("intent")]
        public LexV2.LexIntentV2 Intent { get; set; }

        [JsonPropertyName("nluConfidence")]
        public double NLUConfidence { get; set; }
    }

    [JsonPropertyName("messageVersion")]
    public string MessageVersion { get; set; }

    [JsonPropertyName("invocationSource")]
    public string InvocationSource { get; set; }

    [JsonPropertyName("inputMode")]
    public string InputMode { get; set; }

    [JsonPropertyName("responseContentType")]
    public string ResponseContentType { get; set; }

    [JsonPropertyName("bot")]
    public LexBotV2 Bot { get; set; }

    public class LexBotV2
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("aliasId")]
        public string AliasId { get; set; }

        [JsonPropertyName("localeId")]
        public string LocaleId { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    [JsonPropertyName("proposedNextState")]
    public LexProposedNextStateV2 ProposedNextState { get; set; }

    public class LexProposedNextStateV2
    {
        [JsonPropertyName("dialogAction")]
        public LexDialogActionV2 DialogAction { get; set; }

        [JsonPropertyName("intent")]
        public LexV2.LexIntentV2 Intent { get; set; }
    }

    public class LexDialogActionV2
    {
        [JsonPropertyName("slotToElicit")]
        public string SlotToElicit { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
        // Close | ConfirmIntent | Delegate | ElicitIntent | ElicitSlot
    }

    [JsonPropertyName("requestAttributes")]
    public IDictionary<string, string> RequestAttributes { get; set; }

    [JsonPropertyName("sessionState")]
    public LexSessionStateV2 SessionState { get; set; }

    public class LexSessionStateV2
    {
        [JsonPropertyName("activeContexts")]
        public List<LexActiveContextV2> ActiveContexts { get; set; }

        public class LexActiveContextV2
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("contextAttributes")]
            public IDictionary<string, string> ContextAttributes { get; set; }

            [JsonPropertyName("timeToLive")]
            public LexTimeToLiveV2 TimeToLive { get; set; }

            public class LexTimeToLiveV2
            {
                [JsonPropertyName("timeToLiveInSeconds")]
                public int TimeToLiveInSeconds { get; set; }

                [JsonPropertyName("turnsToLive")]
                public int TurnsToLive { get; set; }
            }
        }

        [JsonPropertyName("sessionAttributes")]
        public IDictionary<string, string> SessionAttributes { get; set; }

        [JsonPropertyName("runtimeHints")]
        public LexRuntimeHintsV2 RuntimeHints { get; set; }

        public class LexRuntimeHintsV2
        {
            [JsonPropertyName("slotHints")]
            public IDictionary<string, IDictionary<string, LexRuntimeHintValuesV2>> SlotHints { get; set; }

            public class LexRuntimeHintValuesV2
            {
                [JsonPropertyName("runtimeHintValues")]
                public List<LexRuntimeHintsValueV2> RuntimeHintValues { get; set; }

                public class LexRuntimeHintsValueV2
                {
                    [JsonPropertyName("phrase")]
                    public string Phrase { get; set; }
                }
            }
        }

        [JsonPropertyName("dialogAction")]
        public LexDialogActionV2 DialogAction { get; set; }

        [JsonPropertyName("intent")]
        public LexV2.LexIntentV2 Intent { get; set; }
    }

    [JsonPropertyName("transcriptions")]
    public List<LexTranscriptionV2> Transcriptions { get; set; }

    public class LexTranscriptionV2
    {
        [JsonPropertyName("transcription")]
        public string Transcription { get; set; }

        [JsonPropertyName("transcriptionConfidence")]
        public double TranscriptionConfidence { get; set; }

        [JsonPropertyName("resolvedContext")]
        public LexResolvedContextV2 ResolvedContext { get; set; }

        public class LexResolvedContextV2
        {
            [JsonPropertyName("intent")]
            public string Intent { get; set; }
        }

        [JsonPropertyName("resolvedSlots")]
        public IDictionary<string, LexV2.LexSlotV2> ResolvedSlots { get; set; }
    }
}
public class LexResponseV2
{
    [JsonPropertyName("sessionState")]
    public LexSessionStateV2 SessionState { get; set; }

    // TODO: Refactor SessionState V2 to be shared with LexEventV2
    public class LexSessionStateV2
    {
        [JsonPropertyName("activeContexts")]
        public List<LexActiveContextV2> ActiveContexts { get; set; }

        public class LexActiveContextV2
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("contextAttributes")]
            public IDictionary<string, string> ContextAttributes { get; set; }

            [JsonPropertyName("timeToLive")]
            public LexTimeToLiveV2 TimeToLive { get; set; }

            public class LexTimeToLiveV2
            {
                [JsonPropertyName("timeToLiveInSeconds")]
                public int TimeToLiveInSeconds { get; set; }

                [JsonPropertyName("turnsToLive")]
                public int TurnsToLive { get; set; }
            }
        }

        [JsonPropertyName("sessionAttributes")]
        public IDictionary<string, string> SessionAttributes { get; set; }

        [JsonPropertyName("runtimeHints")]
        public LexRuntimeHintsV2 RuntimeHints { get; set; }

        public class LexRuntimeHintsV2
        {
            [JsonPropertyName("slotHints")]
            public IDictionary<string, IDictionary<string, LexRuntimeHintValuesV2>> SlotHints { get; set; }

            public class LexRuntimeHintValuesV2
            {
                [JsonPropertyName("runtimeHintValues")]
                public List<LexRuntimeHintsValueV2> RuntimeHintValues { get; set; }

                public class LexRuntimeHintsValueV2
                {
                    [JsonPropertyName("phrase")]
                    public string Phrase { get; set; }
                }
            }
        }

        [JsonPropertyName("dialogAction")]
        public LexDialogActionV2 DialogAction { get; set; }

        public class LexDialogActionV2
        {
            [JsonPropertyName("slotToElicit")]
            public string SlotToElicit { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
            // Close | ConfirmIntent | Delegate | ElicitIntent | ElicitSlot
        }

        [JsonPropertyName("intent")]
        public LexV2.LexIntentV2 Intent { get; set; }

        public class LexResponseCard
        {
            [JsonPropertyName("version")]
            public int? Version { get; set; }

            [JsonPropertyName("contentType")]
            public string ContentType { get; set; }

            [JsonPropertyName("genericAttachments")]
            public IList<LexGenericAttachments> GenericAttachments { get; set; }
        }

        public class LexGenericAttachments
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("subTitle")]
            public string SubTitle { get; set; }

            [JsonPropertyName("imageUrl")]
            public string ImageUrl { get; set; }

            [JsonPropertyName("attachmentLinkUrl")]
            public string AttachmentLinkUrl { get; set; }

            [JsonPropertyName("buttons")]
            public IList<LexButtonV2> Buttons { get; set; }
        }
    }

    [JsonPropertyName("messages")]
    public List<LexMessageV2> Messages { get; set; }

    public class LexMessageV2
    {
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; }
        // CustomPayload | ImageResponseCard | PlainText | SSML

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("imageResponseCard")]
        public LexImageResponseCardV2 ImageResponseCard { get; set; }

        public class LexImageResponseCardV2
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("subtitle")]
            public string Subtitle { get; set; }

            [JsonPropertyName("imageUrl")]
            public string ImageUrl { get; set; }

            [JsonPropertyName("buttons")]
            public List<LexButtonV2> Buttons { get; set; }
        }
    }

    public class LexButtonV2
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    [JsonPropertyName("requestAttributes")]
    public IDictionary<string, string> RequestAttributes { get; set; }
}