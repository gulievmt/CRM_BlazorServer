namespace CRMBlazorServerRBS.CustomCodes
{
    /// <summary>
    /// Represents contextual information about the current user and their connection.
    /// </summary>
    /// <remarks>
    /// This DTO is intended to carry minimal user/session metadata (for auditing, logging,
    /// or request-scoped services). In a Blazor Server app, <see cref="CircuitId"/> can be
    /// used to correlate actions to a specific Blazor circuit.
    /// </remarks>
    public class UserContext
    {
        /// <summary>
        /// The identifier of the authenticated user (for example, the application user id).
        /// </summary>
        /// <remarks>
        /// Typically populated from the authentication system (ClaimsPrincipal) or identity store.
        /// May be null or empty for anonymous/unauthenticated requests.
        /// </remarks>
        public string UserId { get; set; }

        /// <summary>
        /// The client's IP address as observed by the server.
        /// </summary>
        /// <remarks>
        /// May be the remote IP of a proxy/load balancer; consider using forwarded headers
        /// (e.g., __ForwardedHeaders__ middleware) if accurate client IP is required.
        /// </remarks>
        public string IP { get; set; }

        /// <summary>
        /// The client's user agent string (browser or client information).
        /// </summary>
        /// <remarks>
        /// Useful for diagnostics, analytics, or conditional behavior based on client capabilities.
        /// </remarks>
        public string UserAgent { get; set; }

        /// <summary>
        /// The Blazor circuit identifier for the current connection.
        /// </summary>
        /// <remarks>
        /// Present in Blazor Server scenarios; can be used to correlate UI circuit events
        /// to user actions or to scope connection-specific data.
        /// </remarks>
        public string CircuitId { get; set; }
    }
}
