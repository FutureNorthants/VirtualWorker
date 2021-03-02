using Amazon.S3;
using Amazon.S3.Model;
using MimeKit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Email2CXM.Helpers
{
    class ProcessAttachments
    {
        public int numOfAttachments;

        public Boolean Process(String caseRef, MimeMessage message, IAmazonS3 client, Boolean liveInstance)
        {
            numOfAttachments = 0;
            return ProcessAsync(caseRef, message, client, liveInstance).Result;
        }

        private async Task<Boolean> ProcessAsync(String caseRef, MimeMessage message, IAmazonS3 client, Boolean liveInstance)
        {
            int currentBodyPart = 1;
            foreach (MimeEntity bodyPart in message.BodyParts)
            {
                if (bodyPart.ContentType.IsMimeType("image", "*") ||
                    bodyPart.ContentType.IsMimeType("application", "pdf"))
                {
                    numOfAttachments++;
                    MimePart part = (MimePart)bodyPart;
                    await SaveAttachment(caseRef, message, currentBodyPart, part, client, liveInstance);
                }
            }

            return true;
        }

        private async Task<Boolean> SaveAttachment(String caseRef, MimeMessage message, int currentAttachment, MimePart part, IAmazonS3 client, Boolean liveInstance)
        {
            String fileName = caseRef + "-" + currentAttachment + "-" + part.FileName;
            try
            {
                currentAttachment++;
                Stream objectStream = new MemoryStream();
                part.Content.DecodeTo(objectStream);
                byte[] attachmentArray = new byte[objectStream.Length];
                long attachmentLength = objectStream.Length;
                String bucketName = "";
                if (liveInstance)
                {
                    bucketName = "nbc-email-attachments.live";
                }
                else
                {
                    bucketName = "nbc-email-attachments.test";
                }
                using (Stream imageStream = part.Content.Open())
                {
                    PutObjectRequest putRequest = new PutObjectRequest()
                    {
                        InputStream = imageStream,
                        BucketName = bucketName,
                        Key = fileName
                    };
                    putRequest.Headers.ContentLength = attachmentLength;
                    await client.PutObjectAsync(putRequest);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : Processing Image : '{0}' ", error.Message);
                Console.WriteLine(error.StackTrace);
            }
            return true;
        }
    }

}
