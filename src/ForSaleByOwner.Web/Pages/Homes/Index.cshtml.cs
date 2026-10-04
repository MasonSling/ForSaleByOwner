using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ForSaleByOwner.Web.Pages.Homes;
public class IndexModel(SaleService sales) : PageModel
{
    [BindProperty(SupportsGet=true)] public string? City {get;set;}
    [BindProperty(SupportsGet=true)] public int? MaxPrice {get;set;}
    [BindProperty(SupportsGet=true)] public int? Beds {get;set;}
    public IReadOnlyList<Listing> Listings {get;private set;} = [];
    public IActionResult OnGet() { Listings=sales.Search(City,MaxPrice,Beds); return Request.Headers["HX-Request"] == "true" ? Partial("_Homes",Listings) : Page(); }
}
