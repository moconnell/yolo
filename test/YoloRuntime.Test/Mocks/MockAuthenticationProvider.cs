using System;
using System.Collections.Generic;
using System.Net.Http;
using CryptoExchange.Net;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;

namespace YoloRuntime.Test.Mocks;

internal class MockAuthenticationProvider : AuthenticationProvider
{
    public MockAuthenticationProvider(ApiCredentials credentials) : base(credentials)
    {
    }

    public override void AuthenticateRequest(
        RestApiClient apiClient,
        Uri uri,
        HttpMethod method,
        Dictionary<string, object> providedParameters,
        bool auth,
        ArrayParametersSerialization arraySerialization,
        HttpMethodParameterPosition parameterPosition,
        out SortedDictionary<string, object> uriParameters,
        out SortedDictionary<string, object> bodyParameters,
        out Dictionary<string, string> headers)
    {
        uriParameters = new SortedDictionary<string, object>();
        bodyParameters = new SortedDictionary<string, object>();
        headers = new Dictionary<string, string>();
    }
}