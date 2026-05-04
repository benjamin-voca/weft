using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Weft.Api.Versioning;

/// <summary>
/// Deterministic canonical JSON: object keys sorted, no whitespace.
/// Two configs are identical iff their canonical forms are identical, which
/// is what content hashing and "dirty" detection rely on.
/// </summary>
public static class CanonicalJson
{
    public static string Serialize(JsonNode? node)
    {
        var canonical = Sort(node);
        return canonical?.ToJsonString() ?? "null";
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new KeyValuePair<string, JsonNode?>(p.Key, Sort(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(Sort).ToArray()),
        _ => node?.DeepClone()
    };
}

public static class ContentHash
{
    /// <summary>Short, human-friendly content hash (8 hex chars) — Weft's answer to git's SHA.</summary>
    public static string Of(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
