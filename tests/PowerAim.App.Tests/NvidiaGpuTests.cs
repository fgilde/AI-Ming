using PowerAim.AI;

namespace PowerAim.App.Tests;

public class NvidiaGpuTests
{
    [Fact]
    public void Capabilities_are_parsed_in_index_order()
        => Assert.Equal([8.9, 6.1], NvidiaGpu.Parse("0, 8.9\r\n1, 6.1\r\n"));

    [Theory]
    [InlineData(6.1, true, true)]   // Pascal + fused NMS → CUDA
    [InlineData(7.5, true, false)]
    [InlineData(6.1, false, false)] // plain heads are fine on Pascal
    public void Pascal_skips_TensorRT_only_for_end_to_end_models(double cap, bool endToEnd, bool unsuitable)
        => Assert.Equal(unsuitable, NvidiaGpu.TensorRtUnsuitable(cap, endToEnd ? ModelOutputKind.EndToEnd : ModelOutputKind.YoloV8Raw));

    [Fact]
    public void Unknown_capability_is_not_judged()
        => Assert.False(NvidiaGpu.TensorRtUnsuitable(null, ModelOutputKind.EndToEnd));
}

public class NvidiaGpuLiveTests
{
    [Fact]
    public void The_installed_gpu_reports_a_capability_when_an_nvidia_driver_is_present()
    {
        var cap = NvidiaGpu.ComputeCapability(0);
        if (cap == null) return; // no NVIDIA driver on this machine
        Assert.InRange(cap.Value, 3.0, 20.0);
    }
}
