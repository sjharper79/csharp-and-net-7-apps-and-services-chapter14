using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Northwind.AzureFunctions.Service;

[StorageAccount("AzureWebJobsStorage")]
public static class CheckGeneratorFunction
{
    [FunctionName(nameof(CheckGeneratorFunction))]
    public static async Task Run(
        [QueueTrigger("checksQueue")] QueueMessage message,
        [Blob("checks-blob-container")] BlobContainerClient blobContainerClient,
        ILogger log)
    {
        log.LogInformation("C# Queue trigger function executed.");
        log.LogInformation($"MessageId: {message.MessageId}");
        log.LogInformation($"InsertedOn: {message.InsertedOn}");
        log.LogInformation($"ExpiresOn: {message.ExpiresOn}");
        log.LogInformation($"Body: {message.Body}");

        using (Image<Rgba32> image = new(width: 1200, height: 600, backgroundColor: new Rgba32(r: 255, g: 255, b: 255, a: 100)))
        {
            FontCollection collection = new();
            string pathToFont = System.IO.Path.Combine("fonts", "Caveat", "static", "Caveat-Regular.ttf");
            FontFamily family = collection.Add(pathToFont);
            Font font = family.CreateFont(72);

            string amount = message.Body.ToString();

            DrawingOptions options = new()
            {
                GraphicsOptions = new()
                {
                    ColorBlendingMode = PixelColorBlendingMode.Multiply
                }
            };

            Pen blackPen = Pens.Solid(Color.Black, 2);
            Pen blackThickPen = Pens.Solid(Color.Black, 8);
            Pen greenPen = Pens.Solid(Color.Green, 3);
            Brush redBrush = Brushes.Solid(Color.Red);
            Brush blueBrush = Brushes.Solid(Color.Blue);

            IPath border = new RectangularPolygon(x: 50, y: 50, width: 1100, height: 500);
            image.Mutate(x => x.Draw(options, blackPen, border));

            IPath star = new Star(x: 150.0f, y: 150.0f, prongs: 5, innerRadii: 20.0f, outerRadii: 30.0f);
            image.Mutate(x => x.Fill(options, redBrush, star).Draw(options, greenPen, star));

            IPath line1 = new Polygon(new LinearLineSegment(new PointF(100, 275), new PointF(1050, 275)));
            image.Mutate(x => x.Draw(options, blackPen, line1));

            IPath line2 = new Polygon(new LinearLineSegment(new PointF(x: 100, y: 365), new PointF(1050, 365)));
            image.Mutate(x => x.Draw(options, blackPen, line2));

            RichTextOptions textOptions = new(font)
            {
                Origin = new PointF(100, 200),
                WrappingLength = 1000,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            image.Mutate(x => x.DrawText(textOptions, amount, blueBrush, blackPen));

            string blobName = $"{System.DateTime.UtcNow:yyyy-MM-dd-hh-mm-ss}.png";
            log.LogInformation($"Blob name: {blobName}");

            try
            {
                if (System.Environment.GetEnvironmentVariable("IS_LOCAL") == "true")
                {
                    string folder = System.IO.Path.Combine(System.Environment.CurrentDirectory, "blobs");
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    log.LogInformation($"Blobs folder: {folder}");
                    string blobPath = System.IO.Path.Combine(folder, blobName);
                    await image.SaveAsPngAsync(blobPath);
                }

                Stream stream = new MemoryStream();
                await image.SaveAsPngAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);

                blobContainerClient.CreateIfNotExists();
                BlobContentInfo info = await blobContainerClient.UploadBlobAsync(blobName, stream);
                log.LogInformation($"Blob sequence number: {info.BlobSequenceNumber}");
            }
            catch (System.Exception ex)
            {
                log.LogError(ex.Message);
            }
        }

    }
}