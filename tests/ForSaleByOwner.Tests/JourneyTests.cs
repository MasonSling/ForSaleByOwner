using ForSaleByOwner.BLL;
using ForSaleByOwner.DAL;
namespace ForSaleByOwner.Tests;

public class JourneyTests
{
    private readonly DemoWorkspaceStore store = new();
    private JourneyService Service => new(store,new SampleDocumentCatalog());
    private JourneyState State => Service.Get("a").Journey;
    private void Save(string section,string step,params (string Key,string Value)[] edits) {
        var values=State.Answers.ToDictionary(x=>x.Key,x=>x.Value.Value);
        foreach(var (key,value) in edits) values[key]=value;
        Service.Save("a",State.Revision,section,step,values);
    }
    private string Document(string kind) {
        var id=Service.AddDocument("a",State.Revision,kind,false);
        Assert.Equal("Processing",State.Documents.Single(d=>d.Id==id).Status);
        Service.ProcessDocument("a",State.Revision,id);
        return id;
    }
    private void Ready() {
        Save("property","location");Save("property","situation");Save("details","facts");
        Save("preferences","price",("priceNotes","PRIVATE NEGOTIATION FLOOR"));
        Save("preferences","contact",("visibility","I approve the selected public details"));
        Service.Photo("a",State.Revision,"front","cover",null);Save("presentation","photos");
        Save("presentation","description",("description","Seller approved public copy."),("approveDescription","yes"));
        Assert.Empty(JourneyService.Blockers(State));Save("review","check");
    }
    [Fact] public void IncrementalSavesPreserveOtherSectionsAndResumeCursor() {
        Save("property","location");Save("details","facts");
        Service.Visit("a","details","story");
        Save("preferences","price",("priceNotes","Private plan"));
        Service.Visit("a","details","story");
        Assert.Equal("story",State.LastStep);
        Assert.True(JourneyService.Confirmed(State,"year"));
        Save("property","location",("county","Hays"));
        Assert.Equal("Private plan",JourneyService.Value(State,"priceNotes"));
        Assert.True(JourneyService.Confirmed(State,"year"));
        Assert.Contains(State.History,h=>h.Subject=="county");
    }
    [Fact] public void UnknownAndDeferredAnswersDoNotEraseConfirmedWork() {
        Save("details","facts"); var revision=State.Revision;
        Service.Save("a",revision,"details","facts",new Dictionary<string,string>(),true);
        Assert.Equal("1972",JourneyService.Value(State,"year"));
        Save("details","story",("features",null!));
        Assert.Equal("Unknown",State.Answers["features"].Status);
        Assert.True(JourneyService.Confirmed(State,"year"));
    }
    [Fact] public void MaterialChangesRecalculateOnlyDependentPreparation() {
        Save("details","facts");Save("property","situation");
        Save("documents","requirements",("leadReviewed","yes"),("hoaReviewed","yes"),("titleReviewed","yes"),("disclosureReviewed","yes"),("leadExemption","No exemption identified"));
        Save("details","facts",("year","2000"));
        Assert.DoesNotContain("leadReviewed",JourneyService.PreparationKeys(State));
        Assert.True(JourneyService.Confirmed(State,"hoaReviewed"));
        Assert.True(JourneyService.Confirmed(State,"titleReviewed"));
        Save("property","situation",("hoa","No"));
        Assert.DoesNotContain("hoaReviewed",JourneyService.PreparationKeys(State));
        Save("details","facts",("year","1972"));
        Assert.Contains("leadReviewed",JourneyService.PreparationKeys(State));
        Assert.False(JourneyService.Confirmed(State,"leadReviewed"));
    }
    [Fact] public void ConflictRequiresDecisionAndPreservesSourceAndHistory() {
        Save("details","facts");var id=Document("conflict");
        Assert.Equal("1972",JourneyService.Value(State,"year"));
        Service.ReviewFinding("a",State.Revision,id,"year","unsure",null);
        Assert.Equal("1972",JourneyService.Value(State,"year"));
        Service.ReviewFinding("a",State.Revision,id,"year","edit","1973");
        Assert.Equal("1973",JourneyService.Value(State,"year"));
        Assert.Equal(id,State.Answers["year"].DocumentId);Assert.Equal(1,State.Answers["year"].Page);
        Save("details","facts");Assert.Equal(id,State.Answers["year"].DocumentId);
        var replacement=Service.AddDocument("a",State.Revision,"conflict",true);
        Assert.Equal("Needs confirmation",State.Answers["year"].Status);
        Assert.Equal("Superseded",State.Documents.Single(d=>d.Id==id).Status);
        Assert.Equal(2,State.Documents.Single(d=>d.Id==replacement).Version);
        Assert.Contains(State.History,h=>h.Reason.Contains("replaced"));
    }
    [Fact] public void FailuresDuplicatesWrongPropertyAndRecoveryAreExplicit() {
        var unreadable=Document("unreadable");Assert.Equal("Unreadable",State.Documents.Single(d=>d.Id==unreadable).Status);
        Assert.Throws<ArgumentException>(()=>Service.ReviewFinding("a",State.Revision,unreadable,"year","confirm",null));
        var wrong=Document("wrong-property");Assert.Equal("Wrong property",State.Documents.Single(d=>d.Id==wrong).Status);
        var good=Document("record");Assert.NotEmpty(State.Documents.Single(d=>d.Id==good).Findings);
        Assert.Throws<ArgumentException>(()=>Service.AddDocument("a",State.Revision,"record",false));
        Service.ReviewFinding("a",State.Revision,good,"year","confirm",null);
        Save("property","location"); // Confirming seeded address is not an address change.
        Assert.True(JourneyService.Confirmed(State,"year"));
        Save("property","location",("address","100 Different Lane"));
        Assert.Equal("Property changed — replace sample",State.Documents.Single(d=>d.Id==good).Status);
        Assert.False(JourneyService.Confirmed(State,"year"));
    }
    [Fact] public void AgentContextAndDraftProposalsRequireExplicitAcceptance() {
        Save("property","location");Save("details","facts");
        Service.Ask("a","documents","records","What do I need?");
        Assert.Contains("HOA",State.Conversations["documents"].Last().Text);
        Service.Ask("a","presentation","description","Draft my listing");
        Assert.False(State.Answers.ContainsKey("description"));
        var proposal=State.Proposals.Single();
        Service.AcceptProposal("a",State.Revision,proposal.Id);
        Assert.Contains("1842 Live Oak",JourneyService.Value(State,"description"));
        Assert.False(JourneyService.Confirmed(State,"descriptionApproved"));
        Assert.Equal(2,State.Conversations["documents"].Count);
        Service.Ask("a","presentation","description","Draft again");
        var stale=State.Proposals.Last();Save("preferences","price");
        Assert.Throws<InvalidOperationException>(()=>Service.AcceptProposal("a",State.Revision,stale.Id));
    }
    [Fact] public void PublicationIsExplicitPrivateAndImmutableUntilRepublished() {
        Ready();
        Assert.Contains(JourneyService.PreparationKeys(State),k=>!JourneyService.Confirmed(State,k));
        Assert.Throws<ArgumentException>(()=>Service.Publish("a",State.Revision,false));
        Service.Publish("a",State.Revision,true);
        var snapshot=State.Publication!;
        Assert.DoesNotContain("priceNotes",snapshot.Facts.Keys);
        Assert.DoesNotContain("condition",snapshot.Facts.Keys);
        Save("preferences","price",("price","499000"));
        Assert.Equal("485000",State.Publication!.Facts["price"]);
        Assert.False(JourneyService.Confirmed(State,"finalReview"));
        Assert.Throws<ArgumentException>(()=>Service.Publish("a",State.Revision,true));
        Save("presentation","description",("approveDescription","yes"));Save("review","check");
        Service.Publish("a",State.Revision,true);
        Assert.Equal("499000",State.Publication!.Facts["price"]);
        Assert.True(State.Publication.Revision>snapshot.Revision);
    }
    [Fact] public void StaleVersionsAtomicValidationIsolationAndReset() {
        var revision=State.Revision;Save("property","location");
        Assert.Throws<InvalidOperationException>(()=>Service.Save("a",revision,"details","facts",new Dictionary<string,string>()));
        var before=State.Revision;
        Assert.Throws<ArgumentException>(()=>Save("property","location",("address","Should not commit"),("type","Condominium")));
        Assert.Equal(before,State.Revision);Assert.Equal("1842 Live Oak Lane",JourneyService.Value(State,"address"));
        Assert.Empty(Service.Get("b").Journey.History);
        var detached=State;detached.Answers.Clear();Assert.NotEmpty(State.Answers);
        store.Reset("a");Assert.Equal(0,State.Revision);Assert.Empty(State.History);
    }
}
