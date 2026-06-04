using System.Net;
using FluentAssertions;
using Moq;
using Refit;

namespace FamilySplit.Client.UnitTests.Services;

public class ErrorHelperTests
{
    [Fact]
    public void GetMessage_NonApiException_ReturnsGenericMessage()
    {
        var ex = new InvalidOperationException("boom");

        var result = FamilySplit.Client.Services.ErrorHelper.GetMessage(ex);

        result.Should().Be("Something went wrong. Please try again.");
    }

    [Fact]
    public async Task GetMessage_ApiException_ReturnsApiMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        response.Content = new StringContent("");
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "http://test");
        var apiEx = await ApiException.Create(requestMessage, HttpMethod.Get, response, new RefitSettings());

        var result = FamilySplit.Client.Services.ErrorHelper.GetMessage(apiEx);

        result.Should().NotBeNullOrWhiteSpace();
    }
}
