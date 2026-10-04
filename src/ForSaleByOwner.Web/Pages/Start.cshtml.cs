using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ForSaleByOwner.Web.Pages;
public class StartModel(JourneyService journey,DemoSession session) : PageModel
{
    public IActionResult OnGet() { var j=journey.Get(session.Id).Journey;return Redirect(JourneyDefinition.Path(j.LastSection,j.LastStep)); }
}
