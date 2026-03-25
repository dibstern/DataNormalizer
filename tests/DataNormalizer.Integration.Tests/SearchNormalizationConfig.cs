using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using DataNormalizer.Integration.Tests.TestTypes.Search;

namespace DataNormalizer.Integration.Tests;

[NormalizeConfiguration]
public partial class SearchNormalizationConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "result";
                c.Collection<SearchRoute>("routes");
                c.Collection<SearchSegment>("segments");
                c.Collection<TestTypes.Search.SearchOption>("options");
                c.Collection<SearchHop>("hops");
                c.Collection<SearchLine>("lines");
                c.Collection<SearchPlace>("places");
                c.Collection<SearchCarrier>("carriers");
                c.Collection<SearchVehicle>("vehicles");
                c.Collection<SearchImage>("transitImages");
            });
        });

        builder.ForType<SearchHop>(x =>
        {
            x.Reference(p => p.Line).JsonName("line");
            x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
            x.Reference(p => p.Vehicle).JsonName("vehicle");
            x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
        });
    }
}
