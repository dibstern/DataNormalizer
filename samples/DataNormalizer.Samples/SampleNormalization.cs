using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Samples.Models;

namespace DataNormalizer.Samples;

[NormalizeConfiguration]
public partial class SampleNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Order>();
    }
}
