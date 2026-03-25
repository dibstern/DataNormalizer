using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.Transport;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class TransportNormalizationConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<TransportNetwork>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.Collection<TransportLine>("lines");
                c.Collection<TransportStation>("stations");
            });
        });
    }
}
