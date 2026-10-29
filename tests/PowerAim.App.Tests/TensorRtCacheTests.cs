using System.IO;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class TensorRtCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "poweraim-trt-test-" + Guid.NewGuid().ToString("N"));
    private readonly string _model;

    public TensorRtCacheTests()
    {
        Directory.CreateDirectory(_root);
        _model = Path.Combine(_root, "enemy.onnx");
        File.WriteAllBytes(_model, [1, 2, 3]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Same_model_reuses_its_cache_folder()
    {
        var cache = Path.Combine(_root, "trt-cache");
        string first = TensorRtCache.ResolveDir(cache, _model);
        File.WriteAllText(Path.Combine(first, "engine.bin"), "built");

        Assert.Equal(first, TensorRtCache.ResolveDir(cache, _model));
        Assert.True(File.Exists(Path.Combine(first, "engine.bin")));
    }

    [Fact]
    public void A_changed_model_gets_a_fresh_cache_and_the_stale_one_is_removed()
    {
        var cache = Path.Combine(_root, "trt-cache");
        string old = TensorRtCache.ResolveDir(cache, _model);
        File.WriteAllText(Path.Combine(old, "engine.bin"), "built for the old weights");

        File.WriteAllBytes(_model, [9, 9, 9]); // model updated in place
        string fresh = TensorRtCache.ResolveDir(cache, _model);

        Assert.NotEqual(old, fresh);
        Assert.False(Directory.Exists(old));
        Assert.Empty(Directory.GetFiles(fresh, "*.bin"));
    }

    [Fact]
    public void Other_models_keep_their_caches()
    {
        var cache = Path.Combine(_root, "trt-cache");
        var other = Path.Combine(_root, "other.onnx");
        File.WriteAllBytes(other, [4, 5, 6]);
        string otherDir = TensorRtCache.ResolveDir(cache, other);

        TensorRtCache.ResolveDir(cache, _model);
        File.WriteAllBytes(_model, [7]);
        TensorRtCache.ResolveDir(cache, _model);

        Assert.True(Directory.Exists(otherDir));
    }
}
