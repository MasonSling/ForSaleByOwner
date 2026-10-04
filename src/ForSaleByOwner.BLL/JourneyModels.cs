namespace ForSaleByOwner.BLL;

public record JourneyField(string Key, string Label, string Hint = "", string Kind = "text", string[]? Options = null, bool Public = false);
public record JourneyStep(string Id, string Title, string Intro, JourneyField[] Fields);
public record JourneySection(string Id, string Title, string Description, JourneyStep[] Steps);
public record Answer(string Value, string Status, string Source = "Seller", string? DocumentId = null, int? Page = null);
public record Decision(DateTime At, string Subject, string Before, string After, string Reason);
public record Finding(string Id, string Field, string Label, string Value, int Page, string Status = "Needs confirmation");
public record WorkspaceDocument(string Id, string Kind, string Filename, int Version, string Status, string SourcePath, string[] Sections, List<Finding> Findings);
public record ListingPhoto(string Id, string Path, string Caption, bool Included = true);
public record AgentProposal(string Id, string Section, string Field, string Value, string Explanation, int BasedOnRevision, string Status = "Needs confirmation");
public record PublicationSnapshot(DateTime PublishedAt, int Revision, Dictionary<string,string> Facts, string Description, List<ListingPhoto> Photos, string CoverId);
public sealed record JourneyState
{
    public int Revision { get; set; }
    public string LastSection { get; set; } = "property";
    public string LastStep { get; set; } = "location";
    public Dictionary<string, Answer> Answers { get; set; } = new();
    public HashSet<string> Visited { get; set; } = [];
    public List<WorkspaceDocument> Documents { get; set; } = [];
    public List<Decision> History { get; set; } = [];
    public Dictionary<string,List<ChatMessage>> Conversations { get; set; } = new();
    public List<AgentProposal> Proposals { get; set; } = [];
    public List<ListingPhoto> Photos { get; set; } = [new("front", "/images/home-1.jpg", "Sample exterior"), new("garden", "/images/home-2.jpg", "Sample outdoor space"), new("living", "/images/home-3.jpg", "Sample home exterior")];
    public PublicationSnapshot? Publication { get; set; }
}

public static class JourneyDefinition
{
    public static readonly JourneySection[] Sections = [
        new("property", "Your property", "The place, the people, and your plans.", [
            new("location", "First, let's find your place.", "Confirm the basics. These details shape the rest of your plan. Use fictional details in this demo.", [new("address","Street address",Public:true),new("city","Texas city",Public:true),new("state","State","This demo supports Texas homes only.",Kind:"select",Options:["Texas","Outside Texas"]),new("county","County","Enter the county; we don't infer or verify it from the city."),new("type","Property type",Kind:"select",Options:["Single-family resale","Condominium","Farm or ranch","New construction"],Public:true)]),
            new("situation", "What does your next chapter look like?", "A few details help us tailor your preparation. Your ownership situation stays private.", [new("ownership","Ownership situation",Kind:"select",Options:["Sole owner","Co-owners","Estate or trust","Not sure"]),new("occupancy","Who lives here?",Kind:"select",Options:["Owner occupied","Vacant","Tenant occupied"]),new("timing","When would you like to sell?",Kind:"select",Options:["Within 3 months","Within 6 months","Just exploring"]),new("hoa","Mandatory HOA?",Kind:"select",Options:["Yes","No","Not sure"]),new("buyer","Do you already have a buyer?",Kind:"select",Options:["Yes","No","Not sure"])])]),
        new("details", "Home details", "Facts you know. Details buyers will love.", [
            new("facts", "Let's get the home facts right.", "You can use a sample property record later to check these values. An unknown is better than a guess.", [new("year","Year built",Kind:"number",Public:true),new("beds","Bedrooms",Kind:"number",Public:true),new("baths","Bathrooms",Kind:"number",Public:true),new("size","Living area (sq ft)",Kind:"number",Public:true),new("sizeSource","Source of living area","For example: county record, appraisal, or builder's plan.")]),
            new("story", "What makes this home yours?", "Only confirmed features will be used for listing suggestions. Private condition notes stay out of public copy.", [new("features","Features to highlight","Only include details you can confirm.","textarea",Public:true),new("improvements","Improvements and repairs","Dates, work completed, and available receipts.","textarea"),new("condition","Known condition or questions","These private notes are not a completed seller disclosure.","textarea")])]),
        new("documents", "Documents & disclosures", "Gather, review, and keep track.", [
            new("records", "A few documents can do a lot of the work.", "Choose a bundled sample, review its findings, and confirm what belongs in your workspace. Your own files are previewed only.", []),
            new("requirements", "Here's what your sale needs next.", "Keep preparation, legal obligations, and professional handoffs distinct. Reviewing this screen does not complete or sign a disclosure.", [])]),
        new("presentation", "Photos & presentation", "Help buyers picture their next chapter.", [
            new("photos", "Choose a welcoming first impression.", "Organize the bundled sample images and choose a cover. Your own images can be previewed locally, but are not saved.", []),
            new("description", "Let's tell your home's story.", "Ask your guide for a draft, then review every detail before approving the description.", [new("description","Listing description","Your guide proposes; you decide what to keep.","textarea",Public:true)])]),
        new("preferences", "Price & preferences", "Your price. Your boundaries.", [
            new("price", "What asking price feels right to you?", "You set the price. We don't invent comparable sales or estimate a valuation. Consider current local evidence and professional input.", [new("price","Asking price ($)",Kind:"number",Public:true),new("priceNotes","Your pricing notes (private)","Optional context for your own planning.","textarea")]),
            new("contact", "How should buyers reach you?", "Choose what buyers will see. No messages or viewing requests are sent in this demo.", [new("showings","Showing preference",Kind:"select",Options:["By appointment","Open house details to follow","Contact seller to discuss"],Public:true),new("contactMethod","Public contact method",Kind:"select",Options:["Contact through the portal","Display a sample email"],Public:true),new("contactEmail","Sample public email","Only used if you choose to display an email. Use a fictional address.","email",Public:true),new("visibility","Public information review",Kind:"select",Options:["I approve the selected public details","Not reviewed yet"])])]),
        new("review", "Review & publish", "One final look. Then your next chapter.", [
            new("check", "Let's give your listing a final look.", "This preview includes only public listing fields. Your documents, conversations, and private notes stay in your workspace.", []),
            new("publish", "Ready when you are.", "Approve the exact draft shown here. Publishing creates a demo snapshot; it does not syndicate or send anything externally.", [])])
    ];
    public static JourneySection Section(string id) => Sections.FirstOrDefault(s=>s.Id==id) ?? throw new ArgumentException("Unknown section.");
    public static JourneyStep Step(string section,string id) => Section(section).Steps.FirstOrDefault(s=>s.Id==id) ?? throw new ArgumentException("Unknown step.");
    public static string Path(string section,string step) => $"/app/prepare/{section}/{step}";
    public static (string Section,string Step) Neighbor(string section,string step,int offset)
    {
        var all=Sections.SelectMany(s=>s.Steps.Select(t=>(Section:s.Id,Step:t.Id))).ToList();
        var index=all.FindIndex(x=>x.Section==section&&x.Step==step);
        return all[Math.Clamp(index+offset,0,all.Count-1)];
    }
}

public record SampleDocument(string Kind,string Title,string Filename,string Path,string[] Sections,Finding[] Findings,string[] Pages,string? Failure = null);
public interface ISampleDocumentCatalog { IReadOnlyList<SampleDocument> All {get;} }

