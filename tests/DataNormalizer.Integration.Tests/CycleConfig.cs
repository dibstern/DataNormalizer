using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes;
using DataNormalizer.Integration.Tests.TestTypes.Cycles;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class CycleConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<TreeNode>();
        builder.NormalizeGraph<CyclePerson>();
        builder.NormalizeGraph<NodeA>();
        builder.NormalizeGraph<Org>();
        builder.NormalizeGraph<LocationTreeNode>();
    }
}
