namespace ChuckDeviceController.Http.Proxy;

using Microsoft.AspNetCore.Http;

/// <summary>
///     Exposes a handler which supports forwarding a request to an upstream host.
/// </summary>
public interface IProxyHandler
{
    /// <summary>
    ///     Represents a delegate that handles a proxy request.
    /// </summary>
    /// <param name="context">
    ///     An HttpContext that represents the incoming proxy request.
    /// </param>
    /// <returns>
    ///     A <see cref="HttpResponseMessage"/> that represents
    ///    the result of handling the proxy request.
    /// </returns>
    Task<HttpResponseMessage> HandleProxyRequest(HttpContext context);
}

/// <summary>
///     Represents a delegate that handles a proxy request.
/// </summary>
/// <param name="httpContext">
///     An HttpContext that represents the incoming proxy request.
/// </param>
/// <returns>
///     A <see cref="HttpResponseMessage"/> that represents
///    the result of handling the proxy request.
/// </returns>
public delegate Task<HttpResponseMessage> HandleProxyRequest(HttpContext httpContext);