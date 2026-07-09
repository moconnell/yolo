using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Infrastructure;

public class RawJsonPersistenceHandlerTest
{
    [Fact]
    public async Task SendAsync_WhenResponseAlreadyCaptured_ShouldStillUpsertIndexAndReturnResponse()
    {
        BinaryData? capturedBlob = null;
        ITableEntity? capturedIndexEntity = null;

        var blobClient = new Mock<BlobClient>();
        blobClient
            .Setup(x => x.UploadAsync(
                It.IsAny<BinaryData>(),
                false,
                It.IsAny<CancellationToken>()))
            .Callback<BinaryData, bool, CancellationToken>((content, _, _) => capturedBlob = content)
            .ThrowsAsync(new RequestFailedException(409, "already exists", "BlobAlreadyExists", null));

        var containerClient = new Mock<BlobContainerClient>();
        containerClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<Azure.Storage.Blobs.Models.PublicAccessType>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<Azure.Storage.Blobs.Models.BlobContainerEncryptionScopeOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(Mock.Of<BlobContainerInfo>(), Mock.Of<Response>()));
        containerClient
            .Setup(x => x.GetBlobClient(It.Is<string>(name =>
                name.StartsWith("example.com/api/factors/", StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))))
            .Returns(blobClient.Object);

        var blobServiceClient = new Mock<BlobServiceClient>();
        blobServiceClient
            .Setup(x => x.GetBlobContainerClient("http-requests"))
            .Returns(containerClient.Object);

        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Response.FromValue(new TableItem("httprequestsindex"), Mock.Of<Response>())));
        tableClient
            .Setup(x => x.UpsertEntityAsync(
                It.IsAny<ITableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, TableUpdateMode, CancellationToken>((entity, _, _) => capturedIndexEntity = entity)
            .ReturnsAsync(Mock.Of<Response>());

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient("httprequestsindex"))
            .Returns(tableClient.Object);

        using var handler = new RawJsonPersistenceHandler(blobServiceClient.Object, tableServiceClient.Object)
        {
            InnerHandler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            })
        };
        using var client = new HttpClient(handler);

        var response = await client.PostAsync(
            "https://example.com/api/factors?symbol=BTC",
            new StringContent("{\"request\":true}"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("{\"ok\":true}");
        capturedBlob.ShouldNotBeNull();
        capturedBlob.ToString().ShouldContain("\"responseBody\":\"{\\u0022ok\\u0022:true}\"");
        capturedIndexEntity.ShouldNotBeNull();
        capturedIndexEntity.PartitionKey.ShouldBe("example.com");
        capturedIndexEntity.RowKey.ShouldStartWith("api|factors|");
        capturedIndexEntity.GetType().GetProperty("BlobContainer")?.GetValue(capturedIndexEntity).ShouldBe("http-requests");
        capturedIndexEntity.GetType().GetProperty("ContentHash")?.GetValue(capturedIndexEntity)?.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
