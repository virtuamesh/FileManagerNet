using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// Uniform JSON envelope returned by every file manager API call.
/// </summary>
/// <typeparam name="T">Type of the payload carried in <see cref="Data"/>.</typeparam>
public sealed class ApiResponse<T>
{
    /// <summary>True when the request succeeded.</summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; } = true;

    /// <summary>Human-readable failure reason; <c>null</c> on success.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>Response payload; <c>null</c> on failure.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>Creates a successful response carrying <paramref name="data"/>.</summary>
    /// <param name="data">Payload to return to the caller.</param>
    /// <returns>An envelope with <see cref="Ok"/> set to true.</returns>
    public static ApiResponse<T> Success(T data) => new() { Ok = true, Data = data };

    /// <summary>Creates a failed response carrying an error message.</summary>
    /// <param name="error">Reason shown to the client.</param>
    /// <returns>An envelope with <see cref="Ok"/> set to false.</returns>
    public static ApiResponse<T> Failure(string error) => new() { Ok = false, Error = error };
}
