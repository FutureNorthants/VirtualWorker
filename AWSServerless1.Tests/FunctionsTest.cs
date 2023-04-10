using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;

namespace CheckForLocation.Tests;

public class FunctionTest
{
    public FunctionTest() { }

    [Fact]
    public async void TestLocations()
    {
        Function function = new Function();
        Location location = await function.CheckForLocationAsync("i live in northampton, bro");
        Assert.True(location.Success);
        Assert.Equal("Northampton", location.SovereignCouncilName);
        Assert.True(location.sovereignWest);
        Assert.False(location.PostcodeFound);
    }

    [Fact]
    public async Task TestKendraAsync() 
    {
        Function function = new Function();
        await function.GetSecrets();
        KendraResponse response = await function.GetResponseFromKendraAsync(function.secrets?.WNCProdAccessKey, function.secrets?.WNCProdSecretAccessKey, function.secrets?.KendraIndex, "how long will it take for my benefit claim to be processed");
        Assert.Contains("We aim to have claims",response.response);
        Assert.Equal("VERY_HIGH", response.confidence);
    }

}