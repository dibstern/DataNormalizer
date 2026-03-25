using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.ContainerSuffix;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class ContainerSuffixNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.ContainerSuffix = "";
        });
        builder.NormalizeGraph<Contact>();
    }
}
