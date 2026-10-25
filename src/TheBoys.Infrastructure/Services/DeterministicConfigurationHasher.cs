using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class DeterministicConfigurationHasher : IConfigurationHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public (byte[] Hash, string CanonicalJson) ComputeHash(object configuration)
    {
        var rawJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return ComputeHashFromJson(rawJson);
    }

    public (byte[] Hash, string CanonicalJson) ComputeHashFromJson(string rawJson)
    {
        var node = JsonNode.Parse(rawJson);
        var canonicalNode = NormalizeNode(node);
        var canonicalJson = canonicalNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "{}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return (hash, canonicalJson);
    }

    private static JsonNode? NormalizeNode(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonObject obj)
        {
            var sortedObj = new JsonObject();
            var sortedProperties = obj
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var prop in sortedProperties)
            {
                sortedObj[prop.Key] = NormalizeNode(prop.Value?.DeepClone());
            }

            return sortedObj;
        }

        if (node is JsonArray arr)
        {
            var sortedArr = new JsonArray();
            foreach (var item in arr)
            {
                sortedArr.Add(NormalizeNode(item?.DeepClone()));
            }
            return sortedArr;
        }

        if (node is JsonValue val)
        {
            if (val.TryGetValue<string>(out var str))
            {
                return JsonValue.Create(str.Trim());
            }

            if (val.TryGetValue<decimal>(out var dec))
            {
                return JsonValue.Create(decimal.Round(dec, 4, MidpointRounding.AwayFromZero));
            }

            if (val.TryGetValue<double>(out var dbl))
            {
                return JsonValue.Create(Math.Round(dbl, 4, MidpointRounding.AwayFromZero));
            }

            return val.DeepClone();
        }

        return node.DeepClone();
    }
}

