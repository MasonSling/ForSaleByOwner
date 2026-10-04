namespace ForSaleByOwner.BLL;

public record PropertyFacts(string Address, string City, int Year, bool Hoa, string Timing, string Type, string Occupancy, string Condition, bool HasBuyer);
public record SaleTask(string Id, string Title, string Detail, bool Complete = false);
public record ChatMessage(string Role, string Text);
public record DocumentRequirement(string Title, string Detail, string Recipient, string Status, string Version, string Url);
public record Listing(string Id, string Address, string City, int Price, int Beds, int Baths, int Area, string Image, string Description);
public record SaleWorkspace(PropertyFacts Property, List<SaleTask> Tasks, List<ChatMessage> Messages, string ListingCopy, bool Published = false)
{
    public JourneyState Journey { get; init; } = new();
}
public interface ISaleWorkspaceStore
{
    SaleWorkspace Read(string session);
    SaleWorkspace Update(string session, Func<SaleWorkspace, SaleWorkspace> update);
    void Reset(string session);
}
public interface IListingCatalog { IReadOnlyList<Listing> All { get; } }
public interface IDocumentCatalog { IReadOnlyList<DocumentRequirement> For(PropertyFacts facts); }

public sealed class SaleService(ISaleWorkspaceStore store, IDocumentCatalog documents, IListingCatalog listings)
{
    public SaleWorkspace Get(string session) => store.Read(session);
    public IReadOnlyList<DocumentRequirement> Documents(string session) => documents.For(Get(session).Property);
    public void Reset(string session) => store.Reset(session);
    public SaleWorkspace Start(string session, PropertyFacts facts)
    {
        if (facts.Type != "Single-family resale") throw new ArgumentException("This demo currently guides existing single-family homes only.");
        if (facts.Year < 1800 || facts.Year > DateTime.UtcNow.Year || string.IsNullOrWhiteSpace(facts.Address) || string.IsNullOrWhiteSpace(facts.City)) throw new ArgumentException("Enter a valid address, city, and year built.");
        return store.Update(session, w => w with { Property=facts, Tasks=BuildTasks(facts).Select(t => w.Tasks.FirstOrDefault(old=>old.Id==t.Id) is { } old ? t with { Complete=old.Complete } : t).ToList() });
    }
    public SaleWorkspace Toggle(string session, string id) => store.Update(session, w => w with { Tasks = w.Tasks.Select(t => t.Id == id ? t with { Complete = !t.Complete } : t).ToList() });
    public SaleWorkspace SaveListing(string session, string copy)
    {
        if (string.IsNullOrWhiteSpace(copy) || copy.Length > 4000) throw new ArgumentException("Enter listing copy between 1 and 4,000 characters.");
        return store.Update(session, w => w with { ListingCopy = copy.Trim(), Published = false });
    }
    public SaleWorkspace Publish(string session) => store.Update(session, w => string.IsNullOrWhiteSpace(w.ListingCopy) ? w : w with { Published = true });
    public SaleWorkspace Ask(string session, string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 1000) throw new ArgumentException("Enter a question of up to 1,000 characters.");
        return store.Update(session, w => {
            string reply; var copy = w.ListingCopy;
            if (prompt.Contains("listing", StringComparison.OrdinalIgnoreCase)) {
                copy = $"Discover this single-family home at {w.Property.Address} in {w.Property.City}, Texas. Built in {w.Property.Year}, this owner-listed property is ready for its next chapter. Contact the seller to learn more and arrange a viewing.";
                reply = "I've drafted your listing using the property facts you provided. Open My listing to review and edit it. Add verified features and photos before publishing.";
            } else if (prompt.Contains("lead", StringComparison.OrdinalIgnoreCase) || prompt.Contains("1978")) {
                reply = w.Property.Year < 1978 ? $"Your home was built in {w.Property.Year}. Federal law requires lead-paint disclosures for most pre-1978 home sales, throughout Texas and nationwide. Your next step is to check the specific exemptions in the official EPA guidance linked in Documents. If none applies, give the buyer the disclosure, EPA pamphlet, and any available lead-related records before they sign a contract. I've included this step in your checklist. Disclose what you know; don't guess about unknown conditions." : "Your recorded build year is 1978 or later, so the federal pre-1978 lead-disclosure requirement does not apply on that basis. If that year is incorrect, update your property details first.";
            } else if (prompt.Contains("disclosure", StringComparison.OrdinalIgnoreCase) || prompt.Contains("next", StringComparison.OrdinalIgnoreCase)) {
                reply = "Start by reviewing the Seller's Disclosure Notice. Gather what you know about the home's condition and repairs. Keep unknown answers as unknown, and review applicability with a qualified professional. Open Documents for the official source and recipient guidance.";
            } else reply = "This is a scripted demo, so I can't answer every question yet. Try ‘What do I do next?’, ‘Explain lead disclosure’, or ‘Draft my listing’. Live, source-grounded assistance is planned.";
            return w with { ListingCopy = copy, Published = copy == w.ListingCopy && w.Published, Messages = w.Messages.Append(new("seller", prompt.Trim())).Append(new ChatMessage("agent", reply)).TakeLast(40).ToList() };
        });
    }
    public IReadOnlyList<Listing> Search(string? city, int? maxPrice, int? beds) => listings.All.Where(x => (string.IsNullOrWhiteSpace(city) || x.City.Contains(city, StringComparison.OrdinalIgnoreCase)) && (!maxPrice.HasValue || x.Price <= maxPrice) && (!beds.HasValue || x.Beds >= beds)).ToList();
    public Listing? Find(string id) => listings.All.FirstOrDefault(x => x.Id == id);
    public static List<SaleTask> BuildTasks(PropertyFacts p)
    {
        List<SaleTask> tasks = [new("property", "Tell us about your home", "Your property details shape every next step.", true), new("disclosure", "Prepare your seller disclosure", "Gather condition and repair information."), new("photos", "Choose your listing photos", "Tell the story of your home."), new("listing", "Review your listing", "Turn confirmed facts into a clear description.")];
        if (p.Year < 1978) tasks.Insert(2, new("lead", "Review lead-based paint disclosure", "Your home was built before 1978."));
        if (p.Hoa) tasks.Insert(2, new("hoa", "Gather HOA information", "Request association documents and review the addendum."));
        if (p.HasBuyer) tasks.Add(new("buyer", "Coordinate with your buyer", "Discuss the next steps with your title or legal professional."));
        return tasks;
    }
}
