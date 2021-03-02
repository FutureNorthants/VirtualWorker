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

        public Boolean Process(String caseRef, MimeMessage message, IAmazonS3 client, String bucketName)
        {
            numOfAttachments = 0;
            return ProcessAsync(caseRef, message, client, bucketName).Result;
        }

        private async Task<Boolean> ProcessAsync(String caseRef, MimeMessage message, IAmazonS3 client, String bucketName)
        {
            int currentBodyPart = 1;
            foreach (MimeEntity bodyPart in message.BodyParts)
            {
                if (bodyPart.ContentType.IsMimeType("image", "*") ||
                    bodyPart.ContentType.IsMimeType("application", "pdf"))
                {
                    numOfAttachments++;
                    MimePart part = (MimePart)bodyPart;
                    Console.WriteLine(caseRef + " : Processing Attachment " + numOfAttachments);
                    await SaveAttachment(caseRef, message, currentBodyPart, part, client, bucketName);
                }
            }

            return true;
        }

        private async Task<Boolean> SaveAttachment(String caseRef, MimeMessage message, int currentAttachment, MimePart part, IAmazonS3 client, String bucketName)
        {
            String fileName = caseRef + "-" + currentAttachment + "-" + part.FileName;
            try
            {
                Console.WriteLine(caseRef + " : Writing Attachment : " + fileName + " : to : "  + bucketName);
                currentAttachment++;
                Stream objectStream = new MemoryStream();
                part.Content.DecodeTo(objectStream);
                byte[] attachmentArray = new byte[objectStream.Length];
                long attachmentLength = objectStream.Length;
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
