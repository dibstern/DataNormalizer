using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.GraphNaming;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class GraphNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.DtoSuffix = "Record";
        });
        builder.NormalizeGraph<Contact>(graph =>
        {
            graph.UseNaming(n =>
            {
                n.DtoSuffix = "View";
            });
        });
    }
}
