using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class DeepNestingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Universe>();
    }
}
