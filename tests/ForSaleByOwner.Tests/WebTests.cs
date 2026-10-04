using System.Net;
using System.Text.RegularExpressions;
using ForSaleByOwner.BLL;
using Microsoft.AspNetCore.Mvc.Testing;
namespace ForSaleByOwner.Tests;
public class WebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static string Input(string html,string name) => WebUtility.HtmlDecode(Regex.Match(html,"name=\""+name+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static FormUrlEncodedContent Form(string html,params (string,string)[] values) {
        var data=new Dictionary<string,string>{["__RequestVerificationToken"]=Input(html,"__RequestVerificationToken"),["Revision"]=Input(html,"Revision")};
        foreach(var (key,value) in values)data[key]=value;
        return new(data);
    }
    [Theory]
    [InlineData("/")][InlineData("/start")][InlineData("/app")][InlineData("/app/agent")][InlineData("/app/documents")][InlineData("/app/listing")][InlineData("/app/offers")][InlineData("/app/closing")][InlineData("/homes")][InlineData("/homes/live-oak")][InlineData("/developers")]
    public async Task PagesRender(string route) { using var client=factory.CreateClient(); var r=await client.GetAsync(route); Assert.Equal(HttpStatusCode.OK,r.StatusCode); Assert.Contains("<main",await r.Content.ReadAsStringAsync()); }
    [Fact] public async Task EveryGuidedStepRendersAndUnknownRouteIs404() {
        using var client=factory.CreateClient();
        foreach(var section in JourneyDefinition.Sections) foreach(var step in section.Steps) {
            var html=await client.GetStringAsync(JourneyDefinition.Path(section.Id,step.Id));
            Assert.Contains("Save & continue",WebUtility.HtmlDecode(html));Assert.Contains("Save & exit",WebUtility.HtmlDecode(html));
            Assert.Contains("Helping with:",html);Assert.Contains("hx-history=\"false\"",html);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/app/prepare/missing/step")).StatusCode);
    }
    [Fact] public async Task JourneySavesResumesEncodesIsolatesAndResets() {
        using var a=factory.CreateClient();using var b=factory.CreateClient();
        var route="/app/prepare/property/location";var html=await a.GetStringAsync(route);
        var r=await a.PostAsync(route+"?handler=Save",Form(html,("Answers[address]","100 Test Avenue"),("Answers[city]","Dallas"),("Answers[state]","Texas"),("Answers[county]","Dallas"),("Answers[type]","Single-family resale"),("intent","exit")));
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        Assert.Contains("100 Test Avenue",await a.GetStringAsync("/app"));
        Assert.DoesNotContain("100 Test Avenue",await b.GetStringAsync("/app"));
        var resume=await a.GetAsync("/start");Assert.Equal(route,resume.RequestMessage!.RequestUri!.AbsolutePath);
        route="/app/prepare/presentation/description";html=await a.GetStringAsync(route);
        r=await a.PostAsync(route+"?handler=Save",Form(html,("Answers[description]","A bright <script>alert(1)</script> home"),("intent","exit")));
        html=await a.GetStringAsync(route);Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>alert",html);
        await a.PostAsync("/app?handler=Reset",Form(html));
        Assert.DoesNotContain("100 Test Avenue",await a.GetStringAsync("/app"));
    }
    [Fact] public async Task StaleSubmissionDoesNotCommitAndPreservesOldRevision() {
        using var client=factory.CreateClient();const string route="/app/prepare/details/facts";
        var html=await client.GetStringAsync(route);
        await client.PostAsync(route+"?handler=Save",Form(html,("Answers[year]","2001"),("intent","exit")));
        var stale=await client.PostAsync(route+"?handler=Save",Form(html,("Answers[year]","1999"),("intent","exit")));
        var error=await stale.Content.ReadAsStringAsync();Assert.Contains("workspace changed",error);
        Assert.Equal(Input(html,"Revision"),Input(error,"Revision"));
        Assert.Contains("2001",await client.GetStringAsync(route));
    }
    [Fact] public async Task DocumentsProcessingFindingsAndSourcePagesRender() {
        using var client=factory.CreateClient();const string route="/app/prepare/documents/records";
        var html=await client.GetStringAsync(route);
        var added=await client.PostAsync(route+"?handler=AddDocument",Form(html,("kind","hoa")));
        html=await added.Content.ReadAsStringAsync();Assert.Contains("Run sample analysis",html);
        var id=Input(html,"Document");Assert.NotEmpty(id);
        var processed=await client.PostAsync(route+"?handler=Process",Form(html,("Document",id)));
        html=await processed.Content.ReadAsStringAsync();Assert.Contains("840",html);Assert.Contains("PAGE 3",html);
        var confirmed=await client.PostAsync(route+"?handler=Finding",Form(html,("Document",id),("finding","hoaFee"),("decision","confirm")));
        Assert.Contains("Reviewed",await confirmed.Content.ReadAsStringAsync());
        Assert.Contains("Annual regular assessment: $840",await client.GetStringAsync("/samples/hoa-statement.html"));
    }
    [Fact] public async Task AntiforgeryPrivateCachingAndContextualPartials() {
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/app?handler=Reset",new FormUrlEncodedContent([]))).StatusCode);
        var page=await client.GetAsync("/app/prepare/documents/records");Assert.True(page.Headers.CacheControl?.NoStore);
        var html=await page.Content.ReadAsStringAsync();
        using var request=new HttpRequestMessage(HttpMethod.Post,"/app/prepare/documents/records?handler=Ask");
        request.Headers.Add("HX-Request","true");request.Content=Form(html,("prompt","What should I prepare?"));
        var partial=await (await client.SendAsync(request)).Content.ReadAsStringAsync();
        Assert.Contains("HOA",partial);Assert.DoesNotContain("<html",partial);
        client.DefaultRequestHeaders.Add("HX-Request","true");var results=await client.GetStringAsync("/homes?City=Nowhere");
        Assert.Contains("No homes match",results);Assert.DoesNotContain("<html",results);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/homes/missing")).StatusCode);
    }
}

