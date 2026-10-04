using System.Collections.Concurrent;
using ForSaleByOwner.BLL;

namespace ForSaleByOwner.DAL;

public sealed class DemoWorkspaceStore : ISaleWorkspaceStore
{
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    private sealed class Entry { public SaleWorkspace Value = Seed(); public DateTime LastAccess = DateTime.UtcNow; }
    private static SaleWorkspace Seed()
    {
        var p = new PropertyFacts("1842 Live Oak Lane", "Austin", 1972, true, "Within 3 months", "Single-family resale", "Owner occupied", "Good", false);
        var initial = new Dictionary<string,string> { ["address"]=p.Address,["city"]=p.City,["state"]="Texas",["county"]="Travis",["type"]=p.Type,["ownership"]="Sole owner",["occupancy"]=p.Occupancy,["timing"]=p.Timing,["hoa"]="Yes",["buyer"]="No",["year"]="1972",["beds"]="3",["baths"]="2",["size"]="1840",["sizeSource"]="Sample property record",["features"]="Covered patio and fenced yard",["improvements"]="Not yet reviewed",["condition"]="Not yet reviewed",["price"]="485000",["showings"]="By appointment",["contactMethod"]="Contact through the portal" };
        return new(p, SaleService.BuildTasks(p), [new("agent", "Hi, I'm your selling guide. Your Austin home has a story to tell. Let's get the paperwork and your listing ready, one step at a time.")], "") { Journey = new() { Answers=initial.ToDictionary(x=>x.Key,x=>new Answer(x.Value,"Suggested","Demo seed — seller confirmation needed")) } };
    }
    private Entry Get(string session)
    {
        if (entries.Count > 100) foreach (var item in entries) if (item.Value.LastAccess < DateTime.UtcNow.AddHours(-8)) entries.TryRemove(item.Key, out _);
        return entries.GetOrAdd(session, _ => new Entry());
    }
    private static SaleWorkspace Copy(SaleWorkspace w) => System.Text.Json.JsonSerializer.Deserialize<SaleWorkspace>(System.Text.Json.JsonSerializer.Serialize(w))!;
    public SaleWorkspace Read(string session) { var e = Get(session); lock(e) { e.LastAccess = DateTime.UtcNow; return Copy(e.Value); } }
    public SaleWorkspace Update(string session, Func<SaleWorkspace, SaleWorkspace> update) { var e = Get(session); lock(e) { e.Value = Copy(update(Copy(e.Value))); e.LastAccess = DateTime.UtcNow; return Copy(e.Value); } }
    public void Reset(string session) => entries.TryRemove(session, out _);
}
public sealed class DemoDocumentCatalog : IDocumentCatalog
{
    public IReadOnlyList<DocumentRequirement> For(PropertyFacts p)
    {
        List<DocumentRequirement> docs = [new("Seller's Disclosure Notice", "Review known property conditions. Applicability and exemptions need review; this demo does not determine legal eligibility.", "Buyer, before the appropriate contract milestone", "Needs review", "TREC 55-1 · source checked October 2026", "https://www.trec.texas.gov/forms/sellers-disclosure-notice")];
        if (p.Year < 1978) docs.Add(new("Lead-based paint disclosure", "Federal law requires lead-paint disclosures for most pre-1978 home sales. Check the EPA exemptions first; if none applies, prepare the disclosure, EPA pamphlet, and available lead-related records. This is a federal rule, not a county-specific filing.", "Buyer, before becoming obligated under a contract", "Check exemptions first", "EPA source · reviewed October 2026", "https://www.epa.gov/lead/real-estate-disclosures-about-potential-lead-hazards"));
        if (p.Hoa) docs.Add(new("HOA information & addendum", "Gather association information and have the applicable addendum reviewed.", "Buyer and title / legal professional", "Waiting on information", "TREC 36-11 · effective July 1, 2026", "https://www.trec.texas.gov/forms/addendum-property-subject-mandatory-membership-property-owners-association"));
        docs.Add(new("Residential resale contract", "Understand the terms with a qualified professional. No contract is prepared or signed in this demo.", "Buyer, seller, and title / legal professional", "Later in your journey", "TREC 20-19 · effective July 1, 2026", "https://www.trec.texas.gov/forms/one-four-family-residential-contract-resale"));
        return docs;
    }
}
public sealed class DemoListingCatalog : IListingCatalog
{
    public IReadOnlyList<Listing> All { get; } = [
        new("live-oak", "1842 Live Oak Lane", "Austin", 485000, 3, 2, 1840, "/images/home-1.jpg", "An inviting single-family home with a welcoming front porch and room to make your own. Fictional listing for demonstration."),
        new("cedar", "620 Cedar Hollow Drive", "San Antonio", 329000, 4, 2, 2150, "/images/home-2.jpg", "A spacious retreat with easy indoor-outdoor living. Fictional listing for demonstration."),
        new("sage", "93 Sage Meadow Court", "Dallas", 559000, 3, 3, 2420, "/images/home-3.jpg", "Thoughtfully proportioned spaces and a comfortable place to call home. Fictional listing for demonstration.")];
}

