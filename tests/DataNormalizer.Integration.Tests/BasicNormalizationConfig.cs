using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class BasicNormalizationConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Person>();
        builder.NormalizeGraph<Order>();
    }
}
