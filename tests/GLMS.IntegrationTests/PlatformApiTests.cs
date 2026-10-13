using System.Net;
using System.Net.Http.Json;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.IntegrationTests;

/// <summary>Auth, clients, exchange rates, health and Swagger.</summary>
[Collection(ApiCollection.Name)]
public class PlatformApiTests : ApiTestBase
{
    public PlatformApiTests(ApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var anonymous = Fixture.CreateAnonymousClient();

        var response = await anonymous.PostAsJsonAsync("api/auth/login", new LoginRequestDto { Username = "admin", Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        using var anonymous = Fixture.CreateAnonymousClient();

        var response = await anonymous.PostAsJsonAsync("api/auth/login", new LoginRequestDto());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateClientThenRead_Returns201_ThenTheClient()
    {
        var create = await PostAsync("api/clients", new ClientInputDto
        {
            Name = "Integration Freight", ContactPerson = "Sam Dlamini", ContactEmail = "sam@integration.example",
            ContactPhone = "+27 11 555 0199", Region = "Africa"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var read = await ReadAsync<ClientDto>(await Client.GetAsync(create.Headers.Location));
        Assert.Equal("Integration Freight", read.Name);
    }

    [Fact]
    public async Task CreateClient_WithInvalidEmail_Returns400()
    {
        var response = await PostAsync("api/clients", new ClientInputDto
        {
            Name = "Bad Email Ltd", ContactPerson = "Jo", ContactEmail = "not-an-email", ContactPhone = "+27 11 555 0199", Region = "Africa"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ContactEmail", (await ReadAsync<ValidationProblemDetails>(response)).Errors.Keys);
    }

    [Fact]
    public async Task GetClients_Returns200_WithSeededClients()
    {
        var clients = await ReadAsync<List<ClientDto>>(await Client.GetAsync("api/clients"));

        Assert.Contains(clients, c => c.Name == "Cape Freight Holdings");
    }

    [Fact]
    public async Task GetClient_Unknown_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("api/clients/999999")).StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_Returns200_WithPositiveRate()
    {
        var response = await Client.GetAsync("api/exchange-rates/usd-zar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rate = await ReadAsync<ExchangeRateDto>(response);
        Assert.Equal("ZAR", rate.TargetCurrency);
        Assert.True(rate.Rate > 0);
    }

    [Fact]
    public async Task Health_Returns200_Healthy()
    {
        using var anonymous = Fixture.CreateAnonymousClient();

        var response = await anonymous.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SwaggerDocument_IsServed_AndListsContractEndpoints()
    {
        using var anonymous = Fixture.CreateAnonymousClient();

        var json = await anonymous.GetStringAsync("swagger/v1/swagger.json");

        Assert.Contains("/api/contracts", json);
        Assert.Contains("/api/contracts/{id}/status", json);
    }
}
