using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ForSaleByOwner.Web.Pages.Homes;
public class DetailModel(SaleService sales) : PageModel
{
    public Listing Home {get;private set;} = null!;
    public IActionResult OnGet(string id) { var home=sales.Find(id); if(home is null) return NotFound(); Home=home; return Page(); }
}
