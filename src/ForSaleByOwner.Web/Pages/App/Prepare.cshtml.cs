using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForSaleByOwner.Web.Pages.App;

public class PrepareModel(JourneyService journey, DemoSession session) : PageModel
{
    [BindProperty(SupportsGet=true)] public string Section {get;set;} = "property";
    [BindProperty(SupportsGet=true)] public string Step {get;set;} = "location";
    [BindProperty(SupportsGet=true)] public string? Document {get;set;}
    [BindProperty] public int Revision {get;set;}
    [BindProperty] public Dictionary<string,string> Answers {get;set;} = new();
    [TempData] public string? Notice {get;set;}
    public string? Error {get;set;}
    public SaleWorkspace Workspace {get;private set;} = null!;
    public JourneyState Journey => Workspace.Journey;
    public JourneySection CurrentSection => JourneyDefinition.Section(Section);
    public JourneyStep CurrentStep => JourneyDefinition.Step(Section,Step);
    public IReadOnlyList<SampleDocument> Samples => journey.Samples;
    public WorkspaceDocument? SelectedDocument => Journey.Documents.FirstOrDefault(d=>d.Id==Document);
    public SampleDocument? SelectedSample => SelectedDocument is {} d ? Samples.First(s=>s.Kind==d.Kind) : null;
    public string StepPath => JourneyDefinition.Path(Section,Step);
    public string GetValue(string key) => Answers.GetValueOrDefault(key,JourneyService.Value(Journey,key));
    private void Load(bool preserve=false) {
        Workspace=journey.Get(session.Id);if(!preserve)Revision=Journey.Revision;
        if(!preserve)Answers=Journey.Answers.ToDictionary(x=>x.Key,x=>x.Value.Value);
    }
    public IActionResult OnGet() {
        try { journey.Visit(session.Id,Section,Step);Load();return Page(); }
        catch(ArgumentException) {return NotFound();}
    }
    public IActionResult OnPostSave(string intent) => Execute(()=> {
        journey.Save(session.Id,Revision,Section,Step,Answers,intent=="later");
        if(intent=="exit") {journey.Visit(session.Id,Section,Step);Notice="Saved. Continue from this step whenever you're ready.";return Redirect("/app");}
        var next=JourneyDefinition.Neighbor(Section,Step,intent=="back"?-1:1);
        if(intent=="back"&&Section=="property"&&Step=="location")return Redirect("/app");
        journey.Visit(session.Id,next.Section,next.Step);Notice=intent=="later"?"Saved as unfinished. You can return to this section.":"Your answers are saved in this demo session.";
        return Redirect(JourneyDefinition.Path(next.Section,next.Step));
    });
    public IActionResult OnPostAddDocument(string kind,bool replace) => Execute(()=>{var id=journey.AddDocument(session.Id,Revision,kind,replace);return Redirect(StepPath+"?document="+id);});
    public IActionResult OnPostProcess() => Execute(()=>{journey.ProcessDocument(session.Id,Revision,Document??"");return Redirect(StepPath+"?document="+Document);});
    public IActionResult OnPostFinding(string finding,string decision,string? corrected) => Execute(()=>{journey.ReviewFinding(session.Id,Revision,Document??"",finding,decision,corrected);Notice=decision=="unsure"?"Kept as a follow-up; existing confirmed facts were preserved.":"Your review is saved with its document and page reference.";return Redirect(StepPath+"?document="+Document);});
    public IActionResult OnPostPhoto(string id,string operation,string? caption) => Execute(()=>{journey.Photo(session.Id,Revision,id,operation,caption);return Redirect(StepPath);});
    public IActionResult OnPostAsk(string prompt) {
        try {JourneyDefinition.Step(Section,Step);} catch(ArgumentException) {return NotFound();}
        try {journey.Ask(session.Id,Section,Step,prompt);}
        catch(ArgumentException e) {Error=e.Message;}
        Load();return Request.Headers["HX-Request"]=="true"?Partial("_JourneyAgent",this):Page();
    }
    public IActionResult OnPostAccept(string proposal) => Execute(()=>{journey.AcceptProposal(session.Id,Revision,proposal);Notice="Draft accepted. Review and approve the description before publication.";return Redirect(JourneyDefinition.Path("presentation","description"));});
    public IActionResult OnPostPublish(bool approved) => Execute(()=>{journey.Publish(session.Id,Revision,approved);Notice="Your demo listing snapshot is published. Nothing was sent externally. Sale preparation remains a separate track.";return Redirect("/app");});
    private IActionResult Execute(Func<IActionResult> action) {
        try {JourneyDefinition.Step(Section,Step);} catch(ArgumentException) {return NotFound();}
        try {return action();}
        catch(ArgumentException e) {Error=e.Message;Load(true);return Page();}
        catch(InvalidOperationException e) {Error=e.Message;Load(true);return Page();}
    }
}
