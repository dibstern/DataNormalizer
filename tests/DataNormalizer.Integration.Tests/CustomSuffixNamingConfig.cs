using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.CustomSuffix;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class CustomSuffixNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.DtoSuffix = "Model";
            n.DtoPrefix = "";
        });
        builder.NormalizeGraph<Contact>();
    }
}
