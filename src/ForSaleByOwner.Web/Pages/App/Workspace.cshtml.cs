using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ForSaleByOwner.Web.Pages.App;
public class WorkspaceModel(SaleService sales, DemoSession session) : PageModel
{
    public string Section {get;private set;} = "overview";
    public SaleWorkspace Workspace {get;private set;} = null!;
    public IReadOnlyList<DocumentRequirement> Documents {get;private set;} = [];
    public string? Notice {get;private set;}
    public string Heading => Section switch { "agent" => "A little guidance goes a long way.", "documents" => "Your paperwork, in one place.", "listing" => "Let's tell your home's story.", "offers" => "See the whole offer.", "closing" => "Your next chapter is getting closer.", _ => "Your home. Your next chapter." };
    public IActionResult OnGet(string? section) {
        if(section=="documents") return Redirect("/app/prepare/documents/records");
        if(section=="listing") return Redirect("/app/prepare/presentation/description");
        if(section=="agent") { var j=sales.Get(session.Id).Journey;return Redirect(ForSaleByOwner.BLL.JourneyDefinition.Path(j.LastSection,j.LastStep)); }
        if(!Load(section)) return NotFound(); return Page();
    }
    private bool Load(string? section) { Section=section??"overview"; if(!new[]{"overview","agent","documents","listing","offers","closing"}.Contains(Section)) return false; Workspace=sales.Get(session.Id); Documents=sales.Documents(session.Id); return true; }
    public IActionResult OnPostToggle() => Redirect("/app/prepare/documents/requirements");
    public IActionResult OnPostAsk() => Redirect("/start");
    public IActionResult OnPostSave() => Redirect("/app/prepare/presentation/description");
    public IActionResult OnPostPublish() => Redirect("/app/prepare/review/check");
    public IActionResult OnPostReset() { sales.Reset(session.Id); return Redirect("/app"); }
}
