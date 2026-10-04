using ForSaleByOwner.BLL;
using ForSaleByOwner.DAL;
namespace ForSaleByOwner.Tests;
public class SaleTests
{
    private static SaleService Create() => new(new DemoWorkspaceStore(),new DemoDocumentCatalog(),new DemoListingCatalog());
    [Fact] public void IntakeRegeneratesConditionalRequirements()
    {
        var service=Create(); var p=service.Get("a").Property;
        service.Start("a",p with { Year=2001,Hoa=false });
        Assert.DoesNotContain(service.Get("a").Tasks,t=>t.Id is "lead" or "hoa");
        Assert.Equal(2,service.Documents("a").Count);
        service.Start("a",p with { Year=1977,Hoa=true });
        Assert.Contains(service.Get("a").Tasks,t=>t.Id=="lead");
        Assert.Equal(4,service.Documents("a").Count);
        service.Start("a",p with { Year=1978,Hoa=false });
        Assert.DoesNotContain(service.Get("a").Tasks,t=>t.Id=="lead");
    }
    [Fact] public void SessionsResetAndDraftPublicationAreIsolated()
    {
        var s=Create(); s.SaveListing("a","My own description"); s.Publish("a");
        Assert.True(s.Get("a").Published); Assert.Empty(s.Get("b").ListingCopy);
        s.SaveListing("a","Edited description"); Assert.False(s.Get("a").Published);
        s.Reset("a"); Assert.Empty(s.Get("a").ListingCopy);
        Assert.Equal(3,s.Search(null,null,null).Count);
    }
    [Fact] public void AgentCreatesDraftAndPreservesFacts()
    {
        var s=Create(); var result=s.Ask("a","Draft my listing");
        Assert.Contains(result.Property.Address,result.ListingCopy);
        Assert.Contains(result.Property.Year.ToString(),result.ListingCopy);
        Assert.Equal(3,result.Messages.Count);
        Assert.Contains("scripted demo",s.Ask("a","Negotiate a contract").Messages.Last().Text);
    }
    [Fact] public void FiltersAndUnsupportedPropertiesBehavePredictably()
    {
        var s=Create(); Assert.Single(s.Search("austin",500000,3)); Assert.Empty(s.Search("Austin",350000,4));
        Assert.Null(s.Find("missing"));
        Assert.Throws<ArgumentException>(()=>s.Start("a",s.Get("a").Property with {Type="Condominium"}));
    }
    [Fact] public void ReadSnapshotsCannotMutateStore()
    {
        var s=Create(); var snapshot=s.Get("a"); snapshot.Tasks.Clear(); Assert.NotEmpty(s.Get("a").Tasks);
    }
}
