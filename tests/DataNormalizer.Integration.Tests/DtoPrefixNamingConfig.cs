using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.DtoPrefix;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class DtoPrefixNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.DtoPrefix = "Flat";
            n.DtoSuffix = "";
        });
        builder.NormalizeGraph<Contact>();
    }
}
