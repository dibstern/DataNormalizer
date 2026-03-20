using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Samples.Models;

namespace DataNormalizer.Samples;

[NormalizeConfiguration]
public partial class CorporateNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Corporation>();
    }
}
