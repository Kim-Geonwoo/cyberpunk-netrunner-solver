namespace NetrunnerSolver.Core;

// int 배열을 원소로 비교한다. 패턴·토큰 묶음의 중복을 문자열로 바꾸지 않고 가린다.
internal sealed class IntSequenceComparer : IEqualityComparer<int[]>
{
    public static readonly IntSequenceComparer Instance = new();

    public bool Equals(int[]? x, int[]? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

    public int GetHashCode(int[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        var hash = new HashCode();
        foreach (var value in obj)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
