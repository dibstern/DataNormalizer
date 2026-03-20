using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class IgnorePropertyConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Employee>();
        builder.ForType<Employee>(p =>
        {
            p.IgnoreProperty(x => x.Age);
        });
    }
}
