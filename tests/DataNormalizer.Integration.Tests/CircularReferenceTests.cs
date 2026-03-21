using DataNormalizer.Integration.Tests.TestTypes;
using DataNormalizer.Integration.Tests.TestTypes.Cycles;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class CircularReferenceTests
{
    // --- 1-hop: Self-referential TreeNode ---

    [Test]
    [CancelAfter(5000)]
    public void SelfRef_ParentChild_DoesNotInfiniteLoop()
    {
        var root = new TreeNode { Label = "Root" };
        var child = new TreeNode { Label = "Child", Parent = root };
        root.Children.Add(child);

        var result = CycleConfig.Normalize(root);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.TreeNodeList.Length, Is.GreaterThan(result.RootIndex));
    }

    [Test]
    [CancelAfter(5000)]
    public void SelfRef_ParentChild_Roundtrip()
    {
        var root = new TreeNode { Label = "Root" };
        var child = new TreeNode { Label = "Child", Parent = root };
        root.Children.Add(child);

        var result = CycleConfig.Normalize(root);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Label, Is.EqualTo("Root"));
        Assert.That(restored.Children, Has.Count.EqualTo(1));
        Assert.That(restored.Children[0].Label, Is.EqualTo("Child"));
        // Child's parent should reference the root
        Assert.That(restored.Children[0].Parent, Is.Not.Null);
        Assert.That(restored.Children[0].Parent!.Label, Is.EqualTo("Root"));
    }

    [Test]
    [CancelAfter(5000)]
    public void SelfRef_ThreeLevelTree_Roundtrip()
    {
        var root = new TreeNode { Label = "Root" };
        var child1 = new TreeNode { Label = "Child1", Parent = root };
        var child2 = new TreeNode { Label = "Child2", Parent = root };
        var grandchild = new TreeNode { Label = "Grandchild", Parent = child1 };
        child1.Children.Add(grandchild);
        root.Children.Add(child1);
        root.Children.Add(child2);

        var result = CycleConfig.Normalize(root);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Label, Is.EqualTo("Root"));
        Assert.That(restored.Children, Has.Count.EqualTo(2));
        Assert.That(restored.Children[0].Children, Has.Count.EqualTo(1));
        Assert.That(restored.Children[0].Children[0].Label, Is.EqualTo("Grandchild"));
    }

    // --- 2-hop: Mutual reference Person <-> Company ---

    [Test]
    [CancelAfter(5000)]
    public void MutualRef_PersonCompany_DoesNotInfiniteLoop()
    {
        var person = new CyclePerson { Name = "Alice" };
        var company = new Company { Title = "Acme Corp" };
        person.Employer = company;
        company.Ceo = person;

        var result = CycleConfig.Normalize(person);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CyclePersonList.Length, Is.GreaterThan(result.RootIndex));
    }

    [Test]
    [CancelAfter(5000)]
    public void MutualRef_PersonCompany_Roundtrip()
    {
        var person = new CyclePerson { Name = "Alice" };
        var company = new Company { Title = "Acme Corp" };
        person.Employer = company;
        company.Ceo = person;

        var result = CycleConfig.Normalize(person);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Name, Is.EqualTo("Alice"));
        Assert.That(restored.Employer, Is.Not.Null);
        Assert.That(restored.Employer!.Title, Is.EqualTo("Acme Corp"));
        Assert.That(restored.Employer.Ceo, Is.Not.Null);
        Assert.That(restored.Employer.Ceo!.Name, Is.EqualTo("Alice"));
    }

    // --- 3-hop: Triangle A -> B -> C -> A ---

    [Test]
    [CancelAfter(5000)]
    public void Triangle_ABC_DoesNotInfiniteLoop()
    {
        var a = new NodeA { Value = "Alpha" };
        var b = new NodeB { Value = "Beta" };
        var c = new NodeC { Value = "Gamma" };
        a.RefB = b;
        b.RefC = c;
        c.RefA = a;

        var result = CycleConfig.Normalize(a);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NodeAList.Length, Is.GreaterThan(result.RootIndex));
    }

    [Test]
    [CancelAfter(5000)]
    public void Triangle_ABC_Roundtrip()
    {
        var a = new NodeA { Value = "Alpha" };
        var b = new NodeB { Value = "Beta" };
        var c = new NodeC { Value = "Gamma" };
        a.RefB = b;
        b.RefC = c;
        c.RefA = a;

        var result = CycleConfig.Normalize(a);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Value, Is.EqualTo("Alpha"));
        Assert.That(restored.RefB, Is.Not.Null);
        Assert.That(restored.RefB!.Value, Is.EqualTo("Beta"));
        Assert.That(restored.RefB.RefC, Is.Not.Null);
        Assert.That(restored.RefB.RefC!.Value, Is.EqualTo("Gamma"));
        Assert.That(restored.RefB.RefC.RefA, Is.Not.Null);
        Assert.That(restored.RefB.RefC.RefA!.Value, Is.EqualTo("Alpha"));
    }

    // --- 4-hop: Org -> Project -> Team -> Member -> Org ---

    [Test]
    [CancelAfter(5000)]
    public void DeepCycle_4Hop_DoesNotInfiniteLoop()
    {
        var org = new Org { Name = "MegaCorp" };
        var project = new Project { Name = "Skunkworks" };
        var team = new Team { Name = "Alpha Team" };
        var member = new Member { Name = "Alice" };
        org.MainProject = project;
        project.CoreTeam = team;
        team.Lead = member;
        member.Organization = org;

        var result = CycleConfig.Normalize(org);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.OrgList.Length, Is.GreaterThan(result.RootIndex));
    }

    [Test]
    [CancelAfter(5000)]
    public void DeepCycle_4Hop_Roundtrip()
    {
        var org = new Org { Name = "MegaCorp" };
        var project = new Project { Name = "Skunkworks" };
        var team = new Team { Name = "Alpha Team" };
        var member = new Member { Name = "Alice" };
        org.MainProject = project;
        project.CoreTeam = team;
        team.Lead = member;
        member.Organization = org;

        var result = CycleConfig.Normalize(org);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Name, Is.EqualTo("MegaCorp"));
        Assert.That(restored.MainProject!.Name, Is.EqualTo("Skunkworks"));
        Assert.That(restored.MainProject.CoreTeam!.Name, Is.EqualTo("Alpha Team"));
        Assert.That(restored.MainProject.CoreTeam.Lead!.Name, Is.EqualTo("Alice"));
        Assert.That(restored.MainProject.CoreTeam.Lead.Organization!.Name, Is.EqualTo("MegaCorp"));
    }

    // --- Cycle + non-cyclic branch: LocationTreeNode ---

    [Test]
    [CancelAfter(5000)]
    public void CyclePlusBranch_LocationTreeNode_Roundtrip()
    {
        var root = new LocationTreeNode { Label = "HQ", LocationName = "New York" };
        var child = new LocationTreeNode
        {
            Label = "Branch",
            Parent = root,
            LocationName = "Boston",
        };
        root.Children.Add(child);

        var result = CycleConfig.Normalize(root);
        var restored = CycleConfig.Denormalize(result);

        Assert.That(restored.Label, Is.EqualTo("HQ"));
        Assert.That(restored.LocationName, Is.EqualTo("New York"));
        Assert.That(restored.Children, Has.Count.EqualTo(1));
        Assert.That(restored.Children[0].Label, Is.EqualTo("Branch"));
        Assert.That(restored.Children[0].LocationName, Is.EqualTo("Boston"));
        Assert.That(restored.Children[0].Parent!.Label, Is.EqualTo("HQ"));
    }
}
