using System.Text;
using Murmur;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Computes a deterministic 32-bit numeric ID from a string definition ID using MurmurHash3.
/// The result is never 0 (reserved for "none/null" sentinel values).
/// </summary>
public static class DefinitionIdHash
{
    public static int Compute(string id)
    {
        using var murmur = MurmurHash.Create32();
        var hash = murmur.ComputeHash(Encoding.UTF8.GetBytes(id));
        var value = unchecked((int)BitConverter.ToUInt32(hash, 0));
        // 0 is reserved as "none/null"; rehash with a suffix to avoid it
        return value != 0 ? value : unchecked((int)BitConverter.ToUInt32(
            murmur.ComputeHash(Encoding.UTF8.GetBytes(id + "\0")), 0));
    }
}
