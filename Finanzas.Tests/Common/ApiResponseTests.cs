using Finanzas.Application.Common;

namespace Finanzas.Tests.Common;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_CreatesSuccessfulEnvelope()
    {
        var response = ApiResponse<int>.Ok(7);

        Assert.True(response.Success);
        Assert.Equal(7, response.Data);
        Assert.Empty(response.Errors);
    }
}

