using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

// 한 화면에서 상자별 글자 모양 특징을 한 번만 계산해 둔다. 특징은 화면과 상자만으로 정해지므로 결과는 같다.
// 격자 넓히기·칸 학습·시퀀스 보완이 같은 표본 칸을 수십~수백 번 비교하므로, 판독 한 번 동안 하나를 같이 쓴다.
// 한 스레드에서만 쓴다.
internal sealed class GlyphFeatures(BgraImage image)
{
    private readonly Dictionary<Box, double[]?> _cache = [];

    public BgraImage Image { get; } = image ?? throw new ArgumentNullException(nameof(image));

    public int Count => _cache.Count;

    public double[]? Get(Box box)
    {
        if (!_cache.TryGetValue(box, out var features))
        {
            features = GlyphMatcher.Features(Image, box);
            _cache.Add(box, features);
        }

        return features;
    }
}
