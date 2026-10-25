namespace TheBoys.Application.Common.Interfaces;

public interface IConfigurationHasher
{
    (byte[] Hash, string CanonicalJson) ComputeHash(object configuration);
    (byte[] Hash, string CanonicalJson) ComputeHashFromJson(string rawJson);
}

