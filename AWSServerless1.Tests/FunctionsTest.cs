using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;


namespace CheckForLocation.Tests;

public class FunctionTest
{
    public FunctionTest(){}

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
}