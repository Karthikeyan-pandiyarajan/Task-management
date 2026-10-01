namespace Api.Auth;

// Both APIs have a top-level-statement Program in the global namespace, so integration tests that
// reference both use this type with WebApplicationFactory<T> to pick the right entry assembly.
public sealed class AuthApiMarker;
