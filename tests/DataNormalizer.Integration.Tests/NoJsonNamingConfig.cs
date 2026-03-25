using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.NoJsonNaming;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class NoJsonNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.EmitJsonPropertyNames = false;
        });
        builder.NormalizeGraph<Contact>();
    }
}
