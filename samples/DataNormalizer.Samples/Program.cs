using DataNormalizer.Samples;
using DataNormalizer.Samples.Models;

// === Create a realistic object graph with shared references ===

var sharedAddress = new Address
{
    Street = "123 Main St",
    City = "Springfield",
    ZipCode = "62701",
};

var widget = new Product { Name = "Widget", Price = 9.99m };

var order = new Order
{
    OrderId = 1001,
    Customer = new Customer
    {
        Name = "Alice Smith",
        Email = "alice@example.com",
        BillingAddress = sharedAddress, // Same address as shipping
    },
    ShippingAddress = sharedAddress, // Shared reference
    Lines = new()
    {
        new OrderLine { Product = widget, Quantity = 3 },
        new OrderLine { Product = widget, Quantity = 1 }, // Same product, different line
        new OrderLine
        {
            Product = new Product { Name = "Gadget", Price = 24.99m },
            Quantity = 2,
        },
    },
};

// === Normalize ===

Console.WriteLine("=== DataNormalizer Sample ===");
Console.WriteLine();

var result = SampleNormalization.Normalize(order);

var root = result.OrderList[0];

Console.WriteLine("--- Normalized Structure ---");
Console.WriteLine($"Root OrderId: {root.OrderId}");
Console.WriteLine($"Root CustomerIndex: {root.CustomerIndex}");
Console.WriteLine($"Root ShippingAddressIndex: {root.ShippingAddressIndex}");
Console.WriteLine($"Root OrderLine indices: [{string.Join(", ", root.LinesIndices)}]");
Console.WriteLine();

Console.WriteLine("--- Collections ---");
Console.WriteLine($"  OrderList: {result.OrderList.Length}");
Console.WriteLine($"  CustomerList: {result.CustomerList.Length}");
Console.WriteLine($"  AddressList: {result.AddressList.Length}");
Console.WriteLine($"  OrderLineList: {result.OrderLineList.Length}");
Console.WriteLine($"  ProductList: {result.ProductList.Length}");
Console.WriteLine();

// Demonstrate deduplication
var addresses = result.AddressList;
Console.WriteLine($"Addresses in collection: {addresses.Length}");
Console.WriteLine($"  (Billing and Shipping share the same address -> deduplicated to 1 entry)");
Console.WriteLine();

var products = result.ProductList;
Console.WriteLine($"Products in collection: {products.Length}");
Console.WriteLine($"  Widget appears in 2 order lines but is stored once (dedup)");
Console.WriteLine($"  Gadget is a separate product");
foreach (var p in products)
{
    Console.WriteLine($"    - {p.Name}: ${p.Price}");
}
Console.WriteLine();

Console.WriteLine();

// === Denormalize ===

var restored = SampleNormalization.Denormalize(result);

Console.WriteLine("--- Roundtrip Verification ---");

var assertions = 0;
var passed = 0;

void Assert(bool condition, string description)
{
    assertions++;
    if (condition)
    {
        passed++;
        Console.WriteLine($"  PASS: {description}");
    }
    else
    {
        Console.WriteLine($"  FAIL: {description}");
    }
}

Assert(restored.OrderId == 1001, "OrderId preserved");
Assert(restored.Customer.Name == "Alice Smith", "Customer.Name preserved");
Assert(restored.Customer.Email == "alice@example.com", "Customer.Email preserved");
Assert(restored.ShippingAddress.Street == "123 Main St", "ShippingAddress preserved");
Assert(restored.ShippingAddress.City == "Springfield", "ShippingAddress.City preserved");
Assert(restored.Customer.BillingAddress.Street == "123 Main St", "BillingAddress preserved");

// Shared address -> same object after denormalization
Assert(
    ReferenceEquals(restored.ShippingAddress, restored.Customer.BillingAddress),
    "Shared address is same reference after roundtrip"
);

Assert(restored.Lines.Count == 3, "3 order lines preserved");
Assert(restored.Lines[0].Product.Name == "Widget", "First line product is Widget");
Assert(restored.Lines[0].Quantity == 3, "First line quantity is 3");
Assert(restored.Lines[1].Product.Name == "Widget", "Second line product is Widget");
Assert(restored.Lines[1].Quantity == 1, "Second line quantity is 1");

// Same product -> same object after denormalization
Assert(
    ReferenceEquals(restored.Lines[0].Product, restored.Lines[1].Product),
    "Shared product is same reference after roundtrip"
);

Assert(restored.Lines[2].Product.Name == "Gadget", "Third line product is Gadget");
Assert(restored.Lines[2].Quantity == 2, "Third line quantity is 2");

Console.WriteLine();

// === Complex Corporate Structure: 7 Levels with Cycles ===

var dotnetSkill = new Skill { Name = ".NET", Level = 5 };
var cloudSkill = new Skill { Name = "Cloud", Level = 4 };

var basicCert = new Certification
{
    Name = "Azure Fundamentals",
    IssuedBy = "Microsoft",
    RequiredSkill = cloudSkill,
};
var advancedCert = new Certification
{
    Name = "Azure Solutions Architect",
    IssuedBy = "Microsoft",
    Prerequisite = basicCert, // Level 7 cycle: Certification -> Certification
    RequiredSkill = cloudSkill,
};

var alice = new Employee
{
    Name = "Alice",
    Title = "Senior Engineer",
    Certifications = new() { advancedCert, basicCert },
};
var bob = new Employee
{
    Name = "Bob",
    Title = "Junior Engineer",
    Mentor = alice, // Level 5 cycle: Employee -> Employee
    Certifications = new() { basicCert }, // Shared cert with Alice (dedup!)
};

var alphaTeam = new Team
{
    Name = "Alpha",
    Specialty = "Backend",
    Members = new() { alice, bob },
};
var betaTeam = new Team
{
    Name = "Beta",
    Specialty = "Frontend",
    Members = new() { alice }, // Alice shared!
};

var engineering = new Department
{
    Name = "Engineering",
    Budget = 1_000_000m,
    Teams = new() { alphaTeam, betaTeam },
};
var research = new Department
{
    Name = "R&D",
    Budget = 500_000m,
    Teams = new() { alphaTeam }, // Shared team!
};

var headDiv = new Division
{
    Name = "Technology",
    Departments = new() { engineering, research },
};
var subDiv = new Division
{
    Name = "Cloud Services",
    ParentDivision = headDiv, // Level 2 cycle: Division -> Division
    Departments = new() { engineering },
};
headDiv.Departments.Add(
    new Department
    {
        Name = "Infrastructure",
        Budget = 750_000m,
        Teams = new(),
    }
); // Extra dept

var corp = new Corporation { Name = "MegaCorp", HeadDivision = headDiv };

Console.WriteLine("=== Corporate Structure: 7-Level Deep with Cycles ===");
Console.WriteLine();

var corpResult = CorporateNormalization.Normalize(corp);

Console.WriteLine("--- Collections ---");
Console.WriteLine($"  CorporationList: {corpResult.CorporationList.Length}");
Console.WriteLine($"  DivisionList: {corpResult.DivisionList.Length}");
Console.WriteLine($"  DepartmentList: {corpResult.DepartmentList.Length}");
Console.WriteLine($"  TeamList: {corpResult.TeamList.Length}");
Console.WriteLine($"  EmployeeList: {corpResult.EmployeeList.Length}");
Console.WriteLine($"  CertificationList: {corpResult.CertificationList.Length}");
Console.WriteLine($"  SkillList: {corpResult.SkillList.Length}");
Console.WriteLine();

// Show deduplication
Console.WriteLine("--- Deduplication ---");
var employees = corpResult.EmployeeList;
Console.WriteLine($"  Employees: {employees.Length} (Alice appears once despite being in Alpha + Beta teams)");
var certifications = corpResult.CertificationList;
Console.WriteLine(
    $"  Certifications: {certifications.Length} (basicCert appears once despite Alice + Bob both having it)"
);
var teams = corpResult.TeamList;
Console.WriteLine($"  Teams: {teams.Length} (alphaTeam appears once despite Engineering + R&D sharing it)");
Console.WriteLine();

// === Denormalize and verify roundtrip ===

var restoredCorp = CorporateNormalization.Denormalize(corpResult);

Console.WriteLine("--- Corporate Roundtrip Verification ---");

Assert(restoredCorp.Name == "MegaCorp", "Corporation name preserved");
Assert(restoredCorp.HeadDivision.Name == "Technology", "Level 1: Division name preserved");
Assert(restoredCorp.HeadDivision.Departments.Count >= 2, "Level 2: Departments preserved");
Assert(restoredCorp.HeadDivision.Departments[0].Name == "Engineering", "Level 3: Department name preserved");
Assert(restoredCorp.HeadDivision.Departments[0].Teams.Count >= 1, "Level 3: Teams preserved");
Assert(restoredCorp.HeadDivision.Departments[0].Teams[0].Name == "Alpha", "Level 4: Team name preserved");
Assert(restoredCorp.HeadDivision.Departments[0].Teams[0].Members.Count >= 1, "Level 4: Members preserved");

var restoredAlice = restoredCorp.HeadDivision.Departments[0].Teams[0].Members[0];
Assert(restoredAlice.Name == "Alice", "Level 5: Employee name preserved");
Assert(restoredAlice.Certifications.Count >= 1, "Level 5: Certifications preserved");
Assert(restoredAlice.Certifications[0].Name == "Azure Solutions Architect", "Level 6: Certification name preserved");
Assert(restoredAlice.Certifications[0].RequiredSkill.Name == "Cloud", "Level 7: Skill name preserved");

// Verify circular references survived
var restoredBob = restoredCorp.HeadDivision.Departments[0].Teams[0].Members[1];
Assert(restoredBob.Mentor != null, "Level 5 cycle: Bob's mentor exists");
Assert(restoredBob.Mentor!.Name == "Alice", "Level 5 cycle: Bob's mentor is Alice");

Assert(restoredAlice.Certifications[0].Prerequisite != null, "Level 7 cycle: Prerequisite exists");
Assert(
    restoredAlice.Certifications[0].Prerequisite!.Name == "Azure Fundamentals",
    "Level 7 cycle: Prerequisite correct"
);

Console.WriteLine();
Console.WriteLine($"=== {passed}/{assertions} assertions passed ===");

if (passed != assertions)
{
    Console.Error.WriteLine("SAMPLE FAILED: Not all assertions passed.");
    return 1;
}

return 0;
